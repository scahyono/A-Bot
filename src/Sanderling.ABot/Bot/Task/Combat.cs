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

                var listOverviewEntryToAttack = GetListOverviewToAttack(memoryMeasurement, bot);

                var listOverviewEntryToAvoid =
                    memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => !entry.MainIcon.Color.IsRed() && !entry.Type.StartsWith("Amarr") && !entry.Type.StartsWith("Caldari") && !entry.Type.StartsWith("Minmatar") && !entry.Type.StartsWith("Gallente") && !entry.Type.StartsWith("Stargate") && !entry.Type.EndsWith("Gate") && !entry.Type.EndsWith("Sanctum") && !entry.Type.StartsWith("Celestial") && entry.Type!= "Astrahus" && entry.Type != "Fortizar")
                    ?.OrderBy(entry => entry?.DistanceMax ?? int.MaxValue)
                    ?.ToArray();

                var listOverviewEntryToDock =
                    memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => (entry?.Type.EndsWith("Station") ?? false))
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

                    var droneGroupInBay = droneGroupWithNameMatchingPattern("combat");
                    var droneGroupInLocalSpace = droneGroupWithNameMatchingPattern("local space");

                    var droneInBayCount = droneGroupInBay?.Caption?.Text?.CountFromDroneGroupCaption();
                    var droneInLocalSpaceCount = droneGroupInLocalSpace?.Caption?.Text?.CountFromDroneGroupCaption();

                    //	assuming that local space is bottommost group.
                    var setDroneInLocalSpace =
                        droneListView?.Entry?.OfType<DroneViewEntryItem>()
                        ?.Where(drone => droneGroupInLocalSpace?.RegionCenter()?.B < drone?.RegionCenter()?.B)
                        ?.ToArray();

                    var droneInLocalSpaceSetStatus =
                        setDroneInLocalSpace?.Select(drone => drone?.LabelText?.Select(label => label?.Text))?.ConcatNullable()?.WhereNotDefault()?.Distinct()?.ToArray();

                    var droneInLocalSpaceIdle =
                        droneInLocalSpaceSetStatus?.Any(droneStatus => droneStatus.RegexMatchSuccessIgnoreCase("idle")) ?? false;

                    int shield = memoryMeasurement?.ShipUi?.HitpointsAndEnergy?.Shield ?? 1000;
                    int armor = memoryMeasurement?.ShipUi?.HitpointsAndEnergy?.Armor ?? 1000;

                    if (shield == 0)
                    {
                        shouldAttackTarget = false;
                        if (0 < droneInLocalSpaceCount)
                            yield return DroneTaskExtension.ReturnDrone(); // prevent drone from being targetted
                        else if (listOverviewEntryToDock.Length > 0)
                            yield return listOverviewEntryToDock?.FirstOrDefault()?.ClickMenuEntryByRegexPattern(bot, @"dock");
                        else
                            yield return AnomalyEnter.JumpToNextSystem(memoryMeasurement, bot);
                    }
                    else if (armor == 0) {
                        shouldAttackTarget = false;
                        if (listOverviewEntryToDock.Length > 0)
                            yield return listOverviewEntryToDock?.FirstOrDefault()?.ClickMenuEntryByRegexPattern(bot, @"dock");
                        else
                            yield return AnomalyEnter.JumpToNextSystem(memoryMeasurement, bot);
                    }

                    if (listOverviewEntryToAttack?.Length > 0 && listOverviewEntryToAvoid.Length > 0 && droneInLocalSpaceCount == 0 && overviewCaption == "Overview (General)") // restrain and jump to the next system when a pilot is already in the plex
                        yield return AnomalyEnter.JumpToNextSystem(memoryMeasurement, bot);

                    if (listOverviewEntryToAttack?.Length > listOverviewEntryToAttackLastLength) // reinforment detected
                        if (0 < droneInLocalSpaceCount)
                            yield return DroneTaskExtension.ReturnDrone(); // prevent drone from being targetted

                    if (shouldAttackTarget)
                    {
                        if (0 < droneInBayCount && droneInLocalSpaceCount < 5)
                            yield return droneGroupInBay.ClickMenuEntryByRegexPattern(bot, @"launch");

                        if (droneInLocalSpaceIdle)
                            yield return DroneTaskExtension.EngageDrone();
                    }

                    var overviewEntryLockTarget =
                        listOverviewEntryToAttack?.FirstOrDefault(entry => !((entry?.MeTargeted ?? false) || (entry?.MeTargeting ?? false)));

                    if (null != overviewEntryLockTarget && !(TargetCountMax <= memoryMeasurement?.Target?.Length))
                        yield return overviewEntryLockTarget.ClickMenuEntryByRegexPattern(bot, @"^lock\s*target");

                    if (!(0 < listOverviewEntryToAttack?.Length))
                            Completed = true;
                }
                finally {
                    if (null != listOverviewEntryToAttack)
                        listOverviewEntryToAttackLastLength = listOverviewEntryToAttack.Length;
                }

            }
        }

        public static Sanderling.Parse.IOverviewEntry[] GetListOverviewToAttack(Sanderling.Parse.IMemoryMeasurement memoryMeasurement, Bot bot)
        {
            return memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => (entry?.MainIcon?.Color?.IsRed() ?? false) && (entry?.CellValueFromColumnHeader("Type") != "Circadian Seeker"))
                    ?.OrderBy(entry => bot.AttackPriorityIndex(entry))
                    ?.ThenBy(entry => entry?.DistanceMax ?? int.MaxValue)
                    ?.ToArray();
        }

        public MotionParam Motion => null;
	}
}
