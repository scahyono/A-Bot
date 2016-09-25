using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace Sanderling.ABot.Exe
{
	public partial class MainWindow : Window
	{
		public string TitleComputed =>
			"A-Bot v" + (TryFindResource("AppVersionId") ?? "");

        private uint fPreviousExecutionState;

        public MainWindow()
		{
			InitializeComponent();

            // Set new state to prevent system sleep
            fPreviousExecutionState = NativeMethods.SetThreadExecutionState(
                NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED);
            if (fPreviousExecutionState == 0)
            {
                Console.WriteLine("SetThreadExecutionState failed. Do something here...");
                Close();
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);

            // Restore previous state
            if (NativeMethods.SetThreadExecutionState(fPreviousExecutionState) == 0)
            {
                // No way to recover; already exiting
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			ProcessInput();
		}

		public void ProcessInput()
		{
			if (App.SetKeyBotMotionDisable?.Any(setKey => setKey?.All(key => Keyboard.IsKeyDown(key)) ?? false) ?? false)
				Main?.BotMotionDisable();
		}
	}

    internal static class NativeMethods
    {
        // Import SetThreadExecutionState Win32 API and necessary flags
        [DllImport("kernel32.dll")]
        public static extern uint SetThreadExecutionState(uint esFlags);
        public const uint ES_CONTINUOUS = 0x80000000;
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    }
}
