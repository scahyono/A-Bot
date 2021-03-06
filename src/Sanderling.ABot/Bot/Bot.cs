﻿using Bib3;
using Sanderling.Parse;
using BotEngine.Interface;
using System.Linq;
using System.Collections.Generic;
using System;
using Sanderling.Motor;
using Sanderling.ABot.Bot.Task;
using Sanderling.ABot.Bot.Memory;
using Sanderling.ABot.Serialization;
using Bib3.Geometrik;
using WindowsInput.Native;

namespace Sanderling.ABot.Bot
{
	public class Bot
	{
		static public readonly Func<Int64> GetTimeMilli = Bib3.Glob.StopwatchZaitMiliSictInt;
        static public bool cargoFull = false;

		public BotStepInput StepLastInput { private set; get; }

		public PropertyGenTimespanInt64<BotStepResult> StepLastResult { private set; get; }

		int motionId;

		int stepIndex;

		public FromProcessMeasurement<IMemoryMeasurement> MemoryMeasurementAtTime { private set; get; }

		readonly public Accumulator.MemoryMeasurementAccumulator MemoryMeasurementAccu = new Accumulator.MemoryMeasurementAccumulator();

		readonly public OverviewMemory OverviewMemory = new OverviewMemory();

		readonly IDictionary<Int64, int> MouseClickLastStepIndexFromUIElementId = new Dictionary<Int64, int>();

		readonly IDictionary<Accumulation.IShipUiModule, int> ToggleLastStepIndexFromModule = new Dictionary<Accumulation.IShipUiModule, int>();

		public KeyValuePair<Deserialization, Config> ConfigSerialAndStruct { private set; get; }

		public Int64? MouseClickLastAgeStepCountFromUIElement(Interface.MemoryStruct.IUIElement uiElement)
		{
			if (null == uiElement)
				return null;

			var interactionLastStepIndex = MouseClickLastStepIndexFromUIElementId?.TryGetValueNullable(uiElement.Id);

			return stepIndex - interactionLastStepIndex;
		}

		public Int64? ToggleLastAgeStepCountFromModule(Accumulation.IShipUiModule module) =>
			stepIndex - ToggleLastStepIndexFromModule?.TryGetValueNullable(module);

		IEnumerable<IBotTask[]> StepOutputListTaskPath() =>
			((IBotTask)new BotTask { Component = RootTaskListComponent() })
			?.EnumeratePathToNodeFromTreeDFirst(node => node?.Component)
			?.Where(taskPath => (taskPath?.LastOrDefault()).ShouldBeIncludedInStepOutput())
			?.TakeSubsequenceWhileUnwantedInferenceRuledOut();

		void MemorizeStepInput(BotStepInput input)
		{
			ConfigSerialAndStruct = input?.ConfigSerial?.String?.DeserializeIfDifferent(ConfigSerialAndStruct) ?? ConfigSerialAndStruct;

			MemoryMeasurementAtTime = input?.FromProcessMemoryMeasurement?.MapValue(measurement => measurement?.Parse());

			MemoryMeasurementAccu.Accumulate(MemoryMeasurementAtTime);

			OverviewMemory.Aggregate(MemoryMeasurementAtTime);
		}

		void MemorizeStepResult(BotStepResult stepResult)
		{
			var setMotionMouseWaypointUIElement =
				stepResult?.ListMotion
				?.Select(motion => motion?.MotionParam)
				?.Where(motionParam => 0 < motionParam?.MouseButton?.Count())
				?.Select(motionParam => motionParam?.MouseListWaypoint)
				?.ConcatNullable()?.Select(mouseWaypoint => mouseWaypoint?.UIElement)?.WhereNotDefault();

			foreach (var mouseWaypointUIElement in setMotionMouseWaypointUIElement.EmptyIfNull())
				MouseClickLastStepIndexFromUIElementId[mouseWaypointUIElement.Id] = stepIndex;
		}

		public BotStepResult Step(BotStepInput input)
		{
			var beginTimeMilli = GetTimeMilli();

			StepLastInput = input;

			Exception exception = null;

			var listMotion = new List<MotionRecommendation>();

			IBotTask[][] outputListTaskPath = null;

			try
			{
				MemorizeStepInput(input);

				outputListTaskPath = StepOutputListTaskPath()?.ToArray();

				foreach (var moduleToggle in outputListTaskPath.ConcatNullable().OfType<ModuleToggleTask>().Select(moduleToggleTask => moduleToggleTask?.module).WhereNotDefault())
					ToggleLastStepIndexFromModule[moduleToggle] = stepIndex;

				foreach (var taskPath in outputListTaskPath.EmptyIfNull())
				{
					var taskMotionParam = taskPath?.LastOrDefault()?.Motion;

					if (null == taskMotionParam)
						continue;

					listMotion.Add(new MotionRecommendation
					{
						Id = motionId++,
						MotionParam = taskMotionParam,
					});
				}
			}
			catch (Exception e)
			{
				exception = e;
			}

			var stepResult = new BotStepResult
			{
				Exception = exception,
				ListMotion = listMotion?.ToArrayIfNotEmpty(),
				OutputListTaskPath = outputListTaskPath,
			};

			MemorizeStepResult(stepResult);

			StepLastResult = new PropertyGenTimespanInt64<BotStepResult>(stepResult, beginTimeMilli, GetTimeMilli());

			++stepIndex;

			return stepResult;
		}

		IEnumerable<IBotTask> RootTaskListComponent()
		{
            /*  Overview settings:
                1. 50km optimal gun. I use medium beam laser with microwave crystal for sustainability.
                2. Default approach at 50km to outrange NPC guns (High Sec anoms and DED level 4/10 or below).
                3. Add Loot tab (default + NPC Mission + NPC Pirate)
                4. General Tab: default - wrecks - Large Colidable Structure
                5. Columns: default + corporation + size
                6. Hide passive modules
                7. Drones in combat and salvage group + focus fire
            */
            var memoryMeasurement = MemoryMeasurementAtTime?.Value;

            var modalWindow = (Interface.MemoryStruct.MessageBox) memoryMeasurement?.WindowOther?.FirstOrDefault();
            if (null != modalWindow)
            {
                if (modalWindow?.TopCaptionText == "Not Enough Cargo Space")
                    cargoFull = true;
                yield return new CloseOtherWindow(memoryMeasurement);
            }

            var aboveMainMessage = memoryMeasurement?.AbovemainMessage?.FirstOrDefault();
            if (null != aboveMainMessage && aboveMainMessage.Text.Contains("Interference from the cloaking"))
                yield return new MoveShip();

            var telecomWindow = memoryMeasurement?.WindowTelecom?.FirstOrDefault();
            if (null != telecomWindow)
                yield return new CloseTelecomWindow(memoryMeasurement);

            yield return new EnableInfoPanelCurrentSystem { MemoryMeasurement = MemoryMeasurementAtTime?.Value };

			//var saveShipTask = new SaveShipTask { Bot = this };

			//yield return saveShipTask;

			yield return this.EnsureIsActive(MemoryMeasurementAccu?.ShipUiModule?.Where(module => (module?.TooltipLast?.Value?.IsHardener ?? false)
                || (module?.TooltipLast?.Value?.IsArmorRepairer ?? false))); // start armor repairer as well

			var moduleUnknown = MemoryMeasurementAccu?.ShipUiModule?.FirstOrDefault(module => null == module?.TooltipLast?.Value);

            var weaponCount = MemoryMeasurementAccu?.ShipUiModule?.Count(module => module?.TooltipLast?.Value?.IsWeapon ?? false);
            if (weaponCount == 0)
            {
                var weaponModules = MemoryMeasurementAccu?.ShipUiModule?.Where(module => module?.TooltipLast?.Value?.LabelText?.Any(text => text.Text?.Contains("Beam") ?? false) ?? false);
                weaponModules.ForEach(module => module.TooltipLast.Value = new ModuleButtonTooltipWeapon(module.TooltipLast.Value));
            }

            if (null == memoryMeasurement?.WindowDroneView?.First())
                yield break; // are you in escape pod?

			yield return new BotTask { Motion = moduleUnknown?.MouseMove() };

			//if (!saveShipTask.AllowRoam)
			//	yield break;

			var combatTask = new CombatTask { bot = this };

			yield return combatTask;

            var cleanUpTask = new CleanUpTask { bot = this };

            yield return cleanUpTask;

            //if (!saveShipTask.AllowAnomalyEnter)
            //	yield break;

            yield return new UndockTask { MemoryMeasurement = MemoryMeasurementAtTime?.Value };

			if (combatTask.Completed && cleanUpTask.Completed)
				yield return new AnomalyEnter { bot = this };
		}

        private static IModuleButtonTooltip RepairWeapon(Accumulation.IShipUiModule module)
        {
            return module.TooltipLast.Value.LabelText.Any(text => text.Text.Contains("Beam")) ?
                                    new ModuleButtonTooltipWeapon(module.TooltipLast.Value) :
                                    module.TooltipLast.Value;
        }
    }

    internal class ModuleButtonTooltipWeapon : IModuleButtonTooltip
    {
        private IModuleButtonTooltip original;

        public ModuleButtonTooltipWeapon(IModuleButtonTooltip original)
        {
            this.original = original;
        }

        public IEnumerable<Interface.MemoryStruct.IUIElementText> ButtonText
        {
            get
            {
                return original.ButtonText;
            }
        }

        public int? ChildLastInTreeIndex
        {
            get
            {
                return original.ChildLastInTreeIndex;
            }
        }

        public long Id
        {
            get
            {
                return original.Id;
            }
        }

        public IEnumerable<Interface.MemoryStruct.IUIElementInputText> InputText
        {
            get
            {
                return original.InputText;
            }
        }

        public int? InTreeIndex
        {
            get
            {
                return original.InTreeIndex;
            }
        }

        public bool? IsAfterburner
        {
            get
            {
                return original.IsAfterburner;
            }
        }

        public bool? IsArmorRepairer
        {
            get
            {
                return original.IsArmorRepairer;
            }
        }

        public bool? IsHardener
        {
            get
            {
                return original.IsHardener;
            }
        }

        public bool? IsIceHarvester
        {
            get
            {
                return original.IsIceHarvester;
            }
        }

        public bool? IsMicroJumpDrive
        {
            get
            {
                return original.IsMicroJumpDrive;
            }
        }

        public bool? IsMicroWarpDrive
        {
            get
            {
                return original.IsMicroWarpDrive;
            }
        }

        public bool? IsMiner
        {
            get
            {
                return original.IsMiner;
            }
        }

        public bool? IsShieldBooster
        {
            get
            {
                return original.IsShieldBooster;
            }
        }

        public bool? IsSurveyScanner
        {
            get
            {
                return original.IsSurveyScanner;
            }
        }

        public bool? IsTargetPainter
        {
            get
            {
                return original.IsTargetPainter;
            }
        }

        public bool? IsWeapon
        {
            get
            {
                return true;
            }
        }

        public IEnumerable<Interface.MemoryStruct.IUIElementText> LabelText
        {
            get
            {
                return original.LabelText;
            }
        }

        public int? RangeFalloff
        {
            get
            {
                return original.RangeFalloff;
            }
        }

        public int? RangeMax
        {
            get
            {
                return original.RangeMax;
            }
        }

        public int? RangeOptimal
        {
            get
            {
                return original.RangeOptimal;
            }
        }

        public int? RangeWithin
        {
            get
            {
                return original.RangeWithin;
            }
        }

        public RectInt Region
        {
            get
            {
                return original.Region;
            }
        }

        public Interface.MemoryStruct.IUIElement RegionInteraction
        {
            get
            {
                return original.RegionInteraction;
            }
        }

        public int? SignatureRadiusModifierMilli
        {
            get
            {
                return original.SignatureRadiusModifierMilli;
            }
        }

        public IEnumerable<Interface.MemoryStruct.ISprite> Sprite
        {
            get
            {
                return original.Sprite;
            }
        }

        public VirtualKeyCode[] ToggleKey
        {
            get
            {
                return original.ToggleKey;
            }
        }

        public Interface.MemoryStruct.IUIElementText ToggleKeyTextLabel
        {
            get
            {
                return original.ToggleKeyTextLabel;
            }
        }
    }
}
