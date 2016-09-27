using System.Collections.Generic;
using System.Linq;
using Sanderling.Motor;
using Sanderling.Parse;
using BotEngine.Common;
using Sanderling.ABot.Parse;
using System;
using BotEngine.Motor;
using WindowsInput.Native;

namespace Sanderling.ABot.Bot.Task
{
	public class AnomalyEnter : IBotTask
	{
		public const string NoSuitableAnomalyFoundDiagnosticMessage = "no suitable anomaly found. waiting for anomaly to appear.";

		public Bot bot;

		static public bool AnomalySuitableGeneral(Interface.MemoryStruct.IListEntry scanResult) =>
			scanResult?.CellValueFromColumnHeader("Group")?.RegexMatchSuccessIgnoreCase("combat") ?? false;

        static string[] route = {"Romi","Madirmilire","Saana","Bahromab","Kudi","Fabum","Sharji","Gosalav","Sayartchen","Abaim","Somouh","Sorzielang","Teshi","Ashab","Kehour","Boranai","Toshabia","Irnin","Martha","Kooreng","Shaggoth","Murini","Nordar","Kador Prime","Khafis","Gonan","Ghesis","Gamdis","Zororzih","Gensela","Dresi","Aphend"};

    public IEnumerable<IBotTask> Component
		{
			get
			{
				var memoryMeasurementAtTime = bot?.MemoryMeasurementAtTime;
				var memoryMeasurementAccu = bot?.MemoryMeasurementAccu;

				var memoryMeasurement = memoryMeasurementAtTime?.Value;

                if (!memoryMeasurement.ManeuverStartPossible())
					yield break;

                var overviewWindow = memoryMeasurement?.WindowOverview?.FirstOrDefault();

                Interface.MemoryStruct.IListEntry scanResultCombatSite =
                    overviewWindow?.ListView?.Entry?.Where(entry => entry.CellValueFromColumnHeader("Name").StartsWith("Purity of the Throne") && entry.CellValueFromColumnHeader("Type") == "Celestial Beacon")?.FirstOrDefault();

                var probeScannerWindow = memoryMeasurement?.WindowProbeScanner?.FirstOrDefault();

                if (null == scanResultCombatSite)
                    scanResultCombatSite = probeScannerWindow?.ScanResultView?.Entry?.FirstOrDefault(AnomalySuitableGeneral);

                if (null == scanResultCombatSite)
                {

                    if (getNextSystem(memoryMeasurement) >= 0)
                    {
                        yield return JumpToNextSystem(memoryMeasurement, bot);
                    }
                    else {
                        yield return new DiagnosticTask
                        {
                            MessageText = NoSuitableAnomalyFoundDiagnosticMessage,
                        };
                    }

                }

                if (null != scanResultCombatSite)
					yield return scanResultCombatSite.ClickMenuEntryByRegexPattern(bot, ParseStatic.MenuEntryWarpToAtLeafRegexPattern);
			}
		}

        static public IBotTask JumpToNextSystem(IMemoryMeasurement memoryMeasurement, Bot bot)
        { 
            return memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => entry?.CellValueFromColumnHeader("Name") == route[getNextSystem(memoryMeasurement)])
                            ?.Last().ClickMenuEntryByRegexPattern(bot, "Jump");
        }

        static private int getNextSystem(IMemoryMeasurement memoryMeasurement)
        {
            int currentSystem = getCurrentSystem(memoryMeasurement);
            if (currentSystem == - 1)
                return -1;
            if (currentSystem == route.Length - 1)
                return 0;
            return currentSystem + 1;
        }

        static private int getCurrentSystem(IMemoryMeasurement memoryMeasurement)
        {
            string currentSystemInfo = memoryMeasurement.InfoPanelCurrentSystem.HeaderContent.LabelText.FirstOrDefault().Text;
            for (int i = 0; i < route.Length; i++) {
                if (currentSystemInfo.Contains(route[i])) return i;
            }
            return -1;
        }

        public MotionParam Motion => null;
	}

    public class CloseOtherWindow : IBotTask
    {
        public IMemoryMeasurement MemoryMeasurement;

        public CloseOtherWindow(IMemoryMeasurement memoryMeasurement)
        {
            MemoryMeasurement = memoryMeasurement;
        }

        public IEnumerable<IBotTask> Component => null;

        public MotionParam Motion
        {
            get
            {
                if (null == MemoryMeasurement?.WindowOther.FirstOrDefault())
                    return null;

                return MemoryMeasurement?.WindowOther?.FirstOrDefault()?.ButtonText?.FirstOrDefault()?.RegionInteraction?.MouseClick(MouseButtonIdEnum.Left);
            }
        }
    }

    public class CloseTelecomWindow : IBotTask
    {
        public IMemoryMeasurement MemoryMeasurement;

        public CloseTelecomWindow(IMemoryMeasurement memoryMeasurement)
        {
            MemoryMeasurement = memoryMeasurement;
        }

        public IEnumerable<IBotTask> Component => null;

        public MotionParam Motion
        {
            get
            {
                if (null == MemoryMeasurement?.WindowTelecom.FirstOrDefault())
                    return null;

                return MemoryMeasurement?.WindowTelecom?.FirstOrDefault()?.ButtonText?.FirstOrDefault()?.RegionInteraction?.MouseClick(MouseButtonIdEnum.Left);
            }
        }
    }

    public class MoveShip : IBotTask
    {
        public Bot bot;

        public IEnumerable<IBotTask> Component => null;

        public MotionParam Motion
        {
            get
            {
                VirtualKeyCode[] toggleKey = { VirtualKeyCode.ADD };
                return toggleKey?.KeyboardPressCombined();
            }
        }
    }
}
