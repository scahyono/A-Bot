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
	public class CleanUpTask : IBotTask
	{
		const int TargetCountMax = 4;

        // counter to detect reinforcement
        static int listOverviewEntryToCleanUpLastCount = 0;
        static int lastCleanUpListChanged = Environment.TickCount;

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

                var inventoryWindow = memoryMeasurement?.WindowInventory?.FirstOrDefault();

                var listOverviewEntryToAttack = CombatTask.GetListOverviewToAttack(memoryMeasurement, bot);

                var listOverviewEntryToSalvage =
                    memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => (entry?.Type.EndsWith("Wreck") ?? false))
                    ?.OrderBy(entry => entry?.DistanceMax ?? int.MaxValue)
                    ?.ToArray();

                var listOverviewEntryToLoot =
                    memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => (entry?.Type == "Cargo Container"))
                    ?.OrderBy(entry => entry?.DistanceMax ?? int.MaxValue)
                    ?.ToArray();

                Interface.MemoryStruct.IListEntry scanResultAccelerationGate =
                    memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => entry.CellValueFromColumnHeader("Type") == "Acceleration Gate")?.FirstOrDefault();

                try
                {

                    var droneListView = memoryMeasurement?.WindowDroneView?.FirstOrDefault()?.ListView;

                    var droneGroupWithNameMatchingPattern = new Func<string, DroneViewEntryGroup>(namePattern =>
                            droneListView?.Entry?.OfType<DroneViewEntryGroup>()?.FirstOrDefault(group => group?.LabelTextLargest()?.Text?.RegexMatchSuccessIgnoreCase(namePattern) ?? false));

                    var droneGroupInBay = droneGroupWithNameMatchingPattern("CleanUp");
                    var droneGroupInBaySalvage = droneGroupWithNameMatchingPattern("salvage");
                    var droneGroupInLocalSpace = droneGroupWithNameMatchingPattern("local space");

                    var droneInBayCount = droneGroupInBay?.Caption?.Text?.CountFromDroneGroupCaption();
                    var droneInBaySalvageCount = droneGroupInBaySalvage?.Caption?.Text?.CountFromDroneGroupCaption();
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

                    var droneInLocalSpaceSalvageCount =
                        droneInLocalSpaceSetStatus?.Count(droneStatus => droneStatus.RegexMatchSuccessIgnoreCase("salvage drone"));

                    if (!(0 < listOverviewEntryToAttack?.Length))
                        if (inventoryWindow?.ButtonText?.FirstOrDefault()?.Text == "Loot All")
                        {
                            yield return new LootAll(inventoryWindow);
                        }
                        else if (overviewCaption != "Overview (Loot)")
                            if (0 < droneInLocalSpaceCount)
                            {
                                if (null == scanResultAccelerationGate) {                               
                                    yield return new SelectOverviewTab(memoryMeasurement, "Loot");
                                }
                                else {
                                    if (droneInLocalSpaceIdle)
                                        yield return DroneTaskExtension.ReturnDrone(); // prevent drone from being targetted
                                    else
                                        yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"^scoop*");
                                }
                            }
                            else
                            {
                                Completed = true;
                            }
                        else if (Environment.TickCount - lastCleanUpListChanged > 5 * 60 * 1000) // 5 minutes limit
                        { // if taking too long cleaning up one wreak/loot then warp out! We are color blind. So, we cannot detect other people wrecks.
                            if (0 < droneInLocalSpaceCount)
                            {
                                if (droneInLocalSpaceIdle)
                                    yield return DroneTaskExtension.ReturnDrone(); // prevent drone from being targetted
                                else
                                    yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"^scoop*");
                            }
                            else {
                                yield return new SelectOverviewTab(memoryMeasurement, "General");
                            }
                        }
                        else if (overviewCaption == "Overview (Loot)")
                        {
                            if (0 < listOverviewEntryToSalvage.Length)
                            {
                                if (droneInLocalSpaceSalvageCount == 0)
                                {
                                    if (droneInLocalSpaceCount > 0)
                                        yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"^scoop*");
                                    else
                                        yield return droneGroupInBaySalvage.ClickMenuEntryByRegexPattern(bot, @"launch");
                                }
                                if (droneInLocalSpaceIdle)
                                {
                                    yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"^salvage*");
                                }
                                else {
                                    if (listOverviewEntryToLoot.Length > 0)
                                    {
                                        yield return listOverviewEntryToLoot.FirstOrDefault().ClickMenuEntryByRegexPattern(bot, @"^open cargo*");
                                    }
                                }
                                //                                    yield return listOverviewEntryToSalvage.FirstOrDefault().ClickMenuEntryByRegexPattern(bot, @"abandon all nearby wrecks");
                            }
                            else {
                                if (listOverviewEntryToLoot.Length > 0)
                                {
                                    yield return listOverviewEntryToLoot.FirstOrDefault().ClickMenuEntryByRegexPattern(bot, @"^open cargo*");
                                }
                                else if (0 < droneInLocalSpaceCount)
                                {
                                    if (droneInLocalSpaceIdle)
                                        yield return DroneTaskExtension.ReturnDrone(); // prevent drone from being targetted
                                    else
                                        yield return droneGroupInLocalSpace.ClickMenuEntryByRegexPattern(bot, @"^scoop*");
                                }
                                else {
                                    yield return new SelectOverviewTab(memoryMeasurement, "General");
                                }
                            }
                        }
                        else
                            yield return new SelectOverviewTab(memoryMeasurement, "Loot");

                }
                finally {
                    if (null != listOverviewEntryToLoot && null != listOverviewEntryToSalvage) {
                        // Detect clean up count change
                        int listOverviewEntryToCleanUpCurrentCount = listOverviewEntryToLoot.Length + listOverviewEntryToSalvage.Length;
                        if (listOverviewEntryToCleanUpLastCount != listOverviewEntryToCleanUpCurrentCount) {
                            listOverviewEntryToCleanUpLastCount = listOverviewEntryToCleanUpCurrentCount;
                            lastCleanUpListChanged = Environment.TickCount;
                        }
                    }
                }

            }
        }

        public MotionParam Motion => null;
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

    public class LootAll : IBotTask
    {
        public Sanderling.Parse.IWindowInventory InventoryWindow;

        public LootAll(Sanderling.Parse.IWindowInventory inventoryWindow)
        {
            InventoryWindow = inventoryWindow;
        }

        public IEnumerable<IBotTask> Component => null;

        public MotionParam Motion
        {
            get
            {
                if (null == InventoryWindow?.ButtonText?.FirstOrDefault())
                    return null;

                return InventoryWindow?.ButtonText?.FirstOrDefault()?.RegionInteraction?.MouseClick(MouseButtonIdEnum.Left);
            }
        }
    }
}
