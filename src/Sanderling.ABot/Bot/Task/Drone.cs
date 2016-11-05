using BotEngine.Motor;
using Sanderling.Accumulation;
using Sanderling.Motor;
using System.Collections.Generic;
using System.Linq;
using WindowsInput.Native;

namespace Sanderling.ABot.Bot.Task
{
	static public class DroneTaskExtension
	{

        static public IBotTask EngageDrone() {
            return new EngageDroneTask();
        }
        static public IBotTask ReturnDrone()
        {
            return new ReturnDroneTask();
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
                VirtualKeyCode[] toggleKey = { VirtualKeyCode.TAB, VirtualKeyCode.VK_F };
                return toggleKey?.KeyboardPressCombined();
            }
        }
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
                VirtualKeyCode[] toggleKey = { VirtualKeyCode.TAB, VirtualKeyCode.SHIFT, VirtualKeyCode.VK_R };
                return toggleKey?.KeyboardPressCombined();
            }
        }
    }
}
