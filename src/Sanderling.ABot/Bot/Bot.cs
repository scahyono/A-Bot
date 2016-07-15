﻿using Bib3;
using Sanderling.Parse;
using BotEngine.Interface;
using System.Linq;
using System.Collections.Generic;
using System;
using Sanderling.Motor;
using BotEngine.Motor;
using Sanderling.ABot.Bot.Task;
using BotEngine.Common;

namespace Sanderling.ABot.Bot
{
	public class Bot
	{
		public BotStepInput StepLastInput { private set; get; }

		BotStepResult stepLastResult;

		int motionId;

		int stepIndex;

		FromProcessMeasurement<IMemoryMeasurement> memoryMeasurementAtTime;

		readonly Accumulator.MemoryMeasurementAccumulator MemoryMeasurementAccu = new Accumulator.MemoryMeasurementAccumulator();

		IDictionary<Int64, int> MouseClickLastStepIndexFromUIElementId = new Dictionary<Int64, int>();

		public Int64? MouseClickLastAgeStepCountFromUIElement(Interface.MemoryStruct.IUIElement uiElement)
		{
			if (null == uiElement)
				return null;

			var interactionLastStepIndex = MouseClickLastStepIndexFromUIElementId?.TryGetValueNullable(uiElement.Id);

			return stepIndex - interactionLastStepIndex;
		}

		public BotStepResult Step(BotStepInput input)
		{
			StepLastInput = input;

			var listMotion = new List<MotionRecommendation>();

			BotStepResult stepResult = null;

			try
			{
				var addMotion = new Action<MotionParam>(motionParam => listMotion.Add(new MotionRecommendation
				{
					Id = ++motionId,
					MotionParam = motionParam,
				}));

				memoryMeasurementAtTime = input?.FromProcessMemoryMeasurement?.MapValue(measurement => measurement?.Parse());

				var memoryMeasurement = memoryMeasurementAtTime?.Value;

				MemoryMeasurementAccu.Accumulate(memoryMeasurementAtTime);

				var sequenceTask =
					((IBotTask)new BotTask { Component = SequenceRootTask() })?.EnumerateNodeFromTreeDFirst(node => node?.Component)?.WhereNotDefault();

				var sequenceTaskLeaf = sequenceTask?.Where(task => null != task?.Motion);

				var taskNext = sequenceTaskLeaf?.FirstOrDefault();

				var motion = taskNext?.Motion;

				if (null != motion)
					addMotion(motion);
			}
			finally
			{
				var setMotionMOuseWaypointUIElement =
					listMotion
					?.Select(motion => motion?.MotionParam)
					?.Where(motionParam => 0 < motionParam?.MouseButton?.Count())
					?.Select(motionParam => motionParam?.MouseListWaypoint)
					?.ConcatNullable()?.Select(mouseWaypoint => mouseWaypoint?.UIElement)?.WhereNotDefault();

				foreach (var mouseWaypointUIElement in setMotionMOuseWaypointUIElement.EmptyIfNull())
					MouseClickLastStepIndexFromUIElementId[mouseWaypointUIElement.Id] = stepIndex;

				stepLastResult = stepResult = new BotStepResult
				{
					ListMotion = listMotion?.ToArrayIfNotEmpty(),
				};

				++stepIndex;
			}

			return stepResult;
		}

		IEnumerable<IBotTask> SequenceRootTask()
		{
			yield return this.EnsureActivated(MemoryMeasurementAccu?.ShipUiModule?.Where(module => module?.TooltipLast?.Value?.IsHardener ?? false));

			var moduleUnknown = MemoryMeasurementAccu?.ShipUiModule?.FirstOrDefault(module => null == module?.TooltipLast?.Value);

			yield return new BotTask { Motion = moduleUnknown?.MouseMove() };

			yield return new BotTask { Component = CombatSequenceTask() };
		}

		IEnumerable<IBotTask> CombatSequenceTask()
		{
			var memoryMeasurement = memoryMeasurementAtTime?.Value;

			var listOverviewEntryToAttack =
				memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => entry?.MainIcon?.Color?.IsRed() ?? false)
				?.OrderBy(entry => entry?.DistanceMax ?? int.MaxValue)
				?.ToArray();

			var targetSelected =
				memoryMeasurement?.Target?.FirstOrDefault(target => target?.IsSelected ?? false);

			var shouldAttackTarget =
				listOverviewEntryToAttack?.Any(entry => entry?.MeActiveTarget ?? false) ?? false;

			var setModuleWeapon =
				MemoryMeasurementAccu?.ShipUiModule?.Where(module => module?.TooltipLast?.Value?.IsWeapon ?? false);

			if (null != targetSelected)
				if (shouldAttackTarget)
					yield return this.EnsureActivated(setModuleWeapon);

			var overviewEntryLockTarget =
				listOverviewEntryToAttack?.FirstOrDefault(entry => !((entry?.MeTargeted ?? false) || (entry?.MeTargeting ?? false)));

			if (null == overviewEntryLockTarget)
				yield break;

			var menu = memoryMeasurement?.Menu?.FirstOrDefault();

			var menuEntryLockTarget =
				menu?.Entry?.FirstOrDefault(menuEntry => menuEntry?.Text?.RegexMatchSuccessIgnoreCase(@"lock\s*target") ?? false);

			var menuIsOpenedForOverviewEntry =
				MouseClickLastAgeStepCountFromUIElement(overviewEntryLockTarget) <= 1 &&
				(menu?.Entry?.Any(menuEntry => menuEntry?.Text?.RegexMatchSuccessIgnoreCase(@"remove.*overview") ?? false) ?? false);

			if (menuIsOpenedForOverviewEntry && null != menuEntryLockTarget)
				yield return new BotTask { Motion = menuEntryLockTarget.MouseClick(MouseButtonIdEnum.Left) };
			else
				yield return new BotTask { Motion = overviewEntryLockTarget.MouseClick(MouseButtonIdEnum.Right) };
		}
	}
}
