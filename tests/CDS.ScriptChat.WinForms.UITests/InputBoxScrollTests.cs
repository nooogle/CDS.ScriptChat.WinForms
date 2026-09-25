using System.Runtime.InteropServices;

using AwesomeAssertions;

using FlaUI.UIA3;

namespace CDS.ScriptChat.WinForms.UITests;

/// <summary>
/// Drives the real TestHost app out-of-process via FlaUI to check that the mouse wheel scrolls
/// the input box once its content overflows. Without a scrollbar enabled, a multiline
/// <see cref="TextBox"/> has nothing to scroll and <c>WM_MOUSEWHEEL</c> is simply dropped.
/// </summary>
/// <remarks>
/// The wheel message is posted straight to the input box's window and the result is read back
/// with <c>EM_GETFIRSTVISIBLELINE</c>, rather than moving the real cursor and comparing
/// screenshots. The screenshot version was flaky: pointer precision/acceleration could land the
/// cursor short of the control, and anything else taking focus changed the pixels. Win32's own
/// routing of the wheel to the control under the cursor is the OS's behaviour, not ours, so
/// bypassing it loses nothing this test was meant to cover.
/// </remarks>
[TestClass]
public class InputBoxScrollTests
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    private const int WHEEL_DELTA = 120;

    private static string TestHostPath =>
        Path.Combine(AppContext.BaseDirectory, "CDS.ScriptChat.TestHost.exe");

    [TestMethod]
    public void MouseWheel_OverAnOverflowingInputBox_ScrollsIt()
    {
        StaThreadRunner.Run(() =>
        {
            using var app = FlaUI.Core.Application.Launch(TestHostPath, "--demo=markdown");
            using var automation = new UIA3Automation();

            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(10));
            window.Should().NotBeNull("the test host window should appear after launch");

            try
            {
                var inputBox = window!.FindFirstDescendant(cf => cf.ByAutomationId("_inputTextBox"));
                inputBox.Should().NotBeNull();

                var lines = Enumerable.Range(0, 20).Select(i => $"line {i}");
                inputBox!.Patterns.Value.Pattern.SetValue(string.Join("\r\n", lines));
                Thread.Sleep(200);

                var handle = inputBox.Properties.NativeWindowHandle.Value;
                FirstVisibleLine(handle).Should().Be(0, "setting the text leaves the view at the top");

                var rect = inputBox.BoundingRectangle;
                var centreX = rect.X + (rect.Width / 2);
                var centreY = rect.Y + (rect.Height / 2);

                // Negative delta scrolls down; the high word of wParam carries it, lParam the
                // screen position the wheel event happened at.
                var wParam = (nint)((-3 * WHEEL_DELTA) << 16);
                var lParam = (nint)((centreY << 16) | (centreX & 0xFFFF));
                SendMessage(handle, WM_MOUSEWHEEL, wParam, lParam);

                FirstVisibleLine(handle).Should().BeGreaterThan(
                    0,
                    "scrolling over an overflowing input box should move its visible content");
            }
            finally
            {
                app.Close();
            }
        });
    }

    private static int FirstVisibleLine(nint handle) =>
        (int)SendMessage(handle, EM_GETFIRSTVISIBLELINE, 0, 0);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);
}
