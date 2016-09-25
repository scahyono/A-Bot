using BotEngine.Common;
using System.Collections.Generic;
using System.Linq;
using Sanderling.Motor;
using Sanderling.Parse;
using System;
using Sanderling.Interface.MemoryStruct;
using Sanderling.ABot.Parse;
using Bib3;
using WindowsInput.Native;
using BotEngine.Motor;
using BotEngine.Interface;

namespace Sanderling.ABot.Bot.Task
{
	public class CombatTask : IBotTask
	{
		const int TargetCountMax = 4;

        // counter to detect reinforcement
        static int listOverviewEntryToAttackLastLength = 0;

        public Bot bot;

		public bool Completed { private set; get; }

		public IEnumerable<IBotTask> Component
		{
			get
			{
                var memoryMeasurementAtTime = bot?.MemoryMeasurementAtTime;
                var memoryMeasurementAccu = bot?.MemoryMeasurementAccu;

                var memoryMeasurement = memoryMeasurementAtTime?.Value;

                if (!memoryMeasurement.ManeuverStartPossible())
                    yield break;

                string overviewCaption = memoryMeasurement?.WindowOverview?.FirstOrDefault()?.Caption;

                var listOverviewEntryToAttack =
                    memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => entry?.MainIcon?.Color?.IsRed() ?? false)
                    ?.OrderBy(entry => bot.AttackPriorityIndex(entry))
                    ?.ThenBy(entry => entry?.DistanceMax ?? int.MaxValue)
                    ?.ToArray();

                var listOverviewEntryToSalvage =
                    memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => IsWhite(entry?.MainIcon?.Color) && (entry?.Type.EndsWith("Wreck") ?? false))
                    ?.OrderBy(entry => entry?.DistanceMax ?? int.MaxValue)
                    ?.ToArray();

                var listOverviewEntryToAvoid =
                    memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => !entry.MainIcon.Color.IsRed() && !entry.Type.StartsWith("Amarr") && !entry.Type.StartsWith("Caldari") && !entry.Type.StartsWith("Minmatar") && !entry.Type.StartsWith("Gallente") && !entry.Type.StartsWith("Stargate") && !entry.Type.StartsWith("Celestial") && entry.Type!= "Astrahus" && entry.Type != "Fortizar")
                    ?.OrderBy(entry => entry?.DistanceMax ?? int.MaxValue)
                    ?.ToArray();

                var targetSelected =
                    memoryMeasurement?.Target?.FirstOrDefault(target => target?.IsSelected ?? false);

                var shouldAttackTarget =
                    listOverviewEntryToAttack?.Any(entry => entry?.MeActiveTarget ?? false) ?? false;

                var setModuleWeapon =
                    memoryMeasurementAccu?.ShipUiModule?.Where(module => module?.TooltipLast?.Value?.IsWeapon ?? false);

                try
                {

                    if (null != targetSelected)
                        if (shouldAttackTarget)
                            yield return bot.EnsureIsActive(setModuleWeapon);
                        else
                            yield return targetSelected.ClickMenuEntryByRegexPattern(bot, "unlock");

                    var droneListView = memoryMeasurement?.WindowDroneView?.FirstOrDefault()?.ListView;

                    var droneGroupWithNameMatchingPattern = new Func<string, DroneViewEntryGroup>(namePattern =>
                            droneListView?.Entry?.OfType<DroneViewEntryGroup>()?.FirstOrDefault(group => group?.LabelTextLargest()?.Text?.RegexMatchSuccessIgnoreCase(namePattern) ?? false));

                    var droneGroupInBay = droneGroupWithNameMatchingPattern("bay");
                    var droneGroupInLocalSpace = droneGroupWithNameMatchingPattern("local space");

                    var droneInBayCount = droneGroupInBay?.Caption?.Text?.CountFromDroneGroupCaption();
                    var droneInLocalSpaceCount = droneGroupInLocalSpace?.Caption?.Text?.CountFromDroneGroupCaption();

                    //	assuming that local space is bottommost group.
                    var setDroneInLocalSpace =
                        droneListView?.Entry?.OfType<DroneViewEntryItem>()
                        ?.Where(drone => droneGroupInLocalSpace?.RegionCenter()?.B < drone?.RegionCenter()?.B)
                        ?.ToArray();

                    var droneInLocalSpaceSetStatus =
                        setDroneInLocalSpace?.Select(drone => drone?.LabelText?.Select(label => label?.Text?.StatusStringFromDroneEntryText()))?.ConcatNullable()?.WhereNotDefault()?.Distinct()?.ToArray();

                    var droneInLocalSpaceIdle =
                        droneInLocalSpaceSetStatus?.Any(droneStatus => droneStatus.RegexMatchSuccessIgnoreCase("idle")) ?? false;

                    if (listOverviewEntryToAvoid.Length > 0 && droneInLocalSpaceCount == 0 && overviewCaption == "Overview (General)") // restrain and jump to the next system when a pilot is already in the plex
                        yield return AnomalyEnter.JumpToNextSystem(memoryMeasurement, bot);

                    if (listOverviewEntryToAttack?.Length > listOverviewEntryToAttackLastLength) // reinforment detected
                        if (0 < droneInLocalSpaceCount)
                            yield return new ReturnDroneTask(); // prevent drone from being targetted

                    if (shouldAttackTarget)
                    {
                        if (0 < droneInBayCount && droneInLocalSpaceCount < 5)
                            yield return droneGroupInBay.ClickMenuEntryByRegexPattern(bot, @"launch");

                        if (droneInLocalSpaceIdle)
                            yield return new EngageDroneTask();
                    }

                    var overviewEntryLockTarget =
                        listOverviewEntryToAttack?.FirstOrDefault(entry => !((entry?.MeTargeted ?? false) || (entry?.MeTargeting ?? false)));

                    if (null != overviewEntryLockTarget && !(TargetCountMax <= memoryMeasurement?.Target?.Length))
                        yield return overviewEntryLockTarget.ClickMenuEntryByRegexPattern(bot, @"^lock\s*target");

                    if (!(0 < listOverviewEntryToAttack?.Length))
                        if (0 < droneInLocalSpaceCount)
                        {
                            if (overviewCaption == "Overview (Loot)")
                            {
                                if (0 < listOverviewEntryToSalvage.Length)
                                    yield return listOverviewEntryToSalvage.FirstOrDefault().ClickMenuEntryByRegexPattern(bot, @"abandon all nearby wrecks");
                                else
                                    yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"^scoop*");
                            }
                            else
                                yield return new SelectOverviewTab(memoryMeasurement, "Loot");
                        }
                        else {
                            if (overviewCaption == "Overview (General)")
                                Completed = true;
                            else
                                yield return new SelectOverviewTab(memoryMeasurement, "General");
                        }
                }
                finally {
                    if (null != listOverviewEntryToAttack)
                        listOverviewEntryToAttackLastLength = listOverviewEntryToAttack.Length;
                }

            }
        }

        private bool IsWhite(ColorORGB color)
        {
            if (color.OMilli != 1000) return false;
            if (color.RMilli != 1000) return false;
            if (color.GMilli != 1000) return false;
            if (color.BMilli != 1000) return false;
            return true;
        }

        public MotionParam Motion => null;
	}

    public class ReturnDroneTask : IBotTask
    {
        public Bot bot;

        public IShipUiModule module;

        public IEnumerable<IBotTask> Component => null;

        public MotionParam Motion
        {
            get
            {
                VirtualKeyCode[] toggleKey = { VirtualKeyCode.SHIFT, VirtualKeyCode.VK_R};
                return toggleKey?.KeyboardPressCombined();
            }
        }
    }

    public class SelectOverviewTab : IBotTask
    {
        public Sanderling.Parse.IMemoryMeasurement MemoryMeasurement;
        private string Name;

        public SelectOverviewTab(Sanderling.Parse.IMemoryMeasurement memoryMeasurement, string name)
        {
            MemoryMeasurement = memoryMeasurement;
            Name = name;
        }

        public IEnumerable<IBotTask> Component => null;

        public MotionParam Motion
        {
            get
            {
                if (null == MemoryMeasurement?.WindowOverview?.FirstOrDefault()?.PresetTab)
                    return null;

                foreach (var tab in MemoryMeasurement?.WindowOverview?.FirstOrDefault()?.PresetTab) {
                    if (tab.Label.Text == Name)
                        return tab.MouseClick(MouseButtonIdEnum.Left);
                }

                return null;
            }
        }
    }

    public class EngageDroneTask : IBotTask
    {
        public Bot bot;

        public IShipUiModule module;

        public IEnumerable<IBotTask> Component => null;

        public MotionParam Motion
        {
            get
            {
                VirtualKeyCode[] toggleKey = { VirtualKeyCode.VK_F };
                return toggleKey?.KeyboardPressCombined();
            }
        }
    }
}
