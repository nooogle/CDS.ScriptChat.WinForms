using AwesomeAssertions;

using FlaUI.Core;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace CDS.ScriptChat.WinForms.UITests;

/// <summary>
/// Drives the real TestHost app out-of-process via FlaUI to check that the mouse wheel scrolls
/// the input box once its content overflows. Win32 delivers <c>WM_MOUSEWHEEL</c> to whichever
/// control is directly under the cursor; without a scrollbar enabled, a multiline
/// <see cref="TextBox"/> has nothing to scroll and the message is simply dropped.
/// </summary>
/// <remarks>
/// This test drives the real OS mouse cursor, so it is sensitive to the host machine's mouse
/// settings (pointer precision/acceleration in particular can make a simulated move land short
/// of its target) and to whatever else has focus at the moment it runs. It is intentionally not
/// part of the CI-required check for that reason — treat a local failure here as a prompt to
/// re-verify by hand before assuming a real regression, not as a release blocker on its own.
/// </remarks>
[TestClass]
public class InputBoxScrollTests
{
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
                window!.Focus();
                Thread.Sleep(200);

                var inputBox = window.FindFirstDescendant(cf => cf.ByAutomationId("_inputTextBox"));
                inputBox.Should().NotBeNull();

                inputBox!.Focus();
                for (var i = 0; i < 10; i++)
                {
                    Keyboard.Type($"line {i}");
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
                }
                Thread.Sleep(200);

                var rect = inputBox.BoundingRectangle;
                var center = new System.Drawing.Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

                using var before = Capture.Element(inputBox);

                // An absolute position set (not a smoothed MoveTo) — focus is already set via
                // UIA above, so nothing here depends on a genuine hover/MouseEnter transition,
                // and a smoothed move over this large a distance can land short of the target
                // when Windows pointer-precision/acceleration is enabled.
                Mouse.Position = center;
                Thread.Sleep(200);
                for (var i = 0; i < 5; i++)
                {
                    Mouse.Scroll(3);
                }
                Thread.Sleep(300);

                using var after = Capture.Element(inputBox);

                BitmapsEqual(before.Bitmap, after.Bitmap).Should().BeFalse(
                    "scrolling over an overflowing input box should move its visible content");
            }
            finally
            {
                app.Close();
            }
        });
    }

    private static bool BitmapsEqual(System.Drawing.Bitmap a, System.Drawing.Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        for (var x = 0; x < a.Width; x += 4)
        {
            for (var y = 0; y < a.Height; y += 4)
            {
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
