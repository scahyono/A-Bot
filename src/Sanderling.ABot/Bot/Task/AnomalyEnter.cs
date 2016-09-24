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

        static string[] route = {"Romi","Madirmilire","Bahromab","Kudi","Sharji","Sayartchen","Teshi","Ashab","Kehour","Akhragan","Zororzih","Dresi","Aphend"};
        static string lastJump="";

    public IEnumerable<IBotTask> Component
		{
			get
			{
				var memoryMeasurementAtTime = bot?.MemoryMeasurementAtTime;
				var memoryMeasurementAccu = bot?.MemoryMeasurementAccu;

				var memoryMeasurement = memoryMeasurementAtTime?.Value;

                var aboveMainMessage = memoryMeasurement?.AbovemainMessage?.FirstOrDefault();
                if (null != aboveMainMessage && aboveMainMessage.Text.Contains("Interference from the cloaking")) 
                    yield return new MoveShip();

                var telecomWindow = memoryMeasurement?.WindowTelecom?.FirstOrDefault();
                if (null != telecomWindow)
                    yield return new CloseTelecomWindow(memoryMeasurement);

                if (!memoryMeasurement.ManeuverStartPossible())
					yield break;

				var probeScannerWindow = memoryMeasurement?.WindowProbeScanner?.FirstOrDefault();

                var scanResultCombatSite =
					probeScannerWindow?.ScanResultView?.Entry?.FirstOrDefault(AnomalySuitableGeneral);

                if (null == scanResultCombatSite)
                {

                    int nextSystem = getNextSystem(memoryMeasurement);
                    if (nextSystem >= 0)
                    {
                        yield return memoryMeasurement?.WindowOverview?.FirstOrDefault()?.ListView?.Entry?.Where(entry => entry?.CellValueFromColumnHeader("Name") == route[nextSystem])
                            ?.Last().ClickMenuEntryByRegexPattern(bot,"Jump");
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

        private int getNextSystem(IMemoryMeasurement memoryMeasurement)
        {
            int currentSystem = getCurrentSystem(memoryMeasurement);
            if (currentSystem == - 1)
                return -1;
            if (currentSystem == route.Length - 1)
                return 0;
            return currentSystem + 1;
        }

        private int getCurrentSystem(IMemoryMeasurement memoryMeasurement)
        {
            string currentSystemInfo = memoryMeasurement.InfoPanelCurrentSystem.HeaderContent.LabelText.FirstOrDefault().Text;
            for (int i = 0; i < route.Length; i++) {
                if (currentSystemInfo.Contains(route[i])) return i;
            }
            return -1;
        }

        public MotionParam Motion => null;
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
