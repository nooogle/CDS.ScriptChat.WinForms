using AwesomeAssertions;

using FlaUI.Core;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace CDS.ScriptChat.WinForms.UITests;

/// <summary>
/// Drives the real TestHost app out-of-process via FlaUI to check the input box's Enter
/// handling: a bare Enter sends immediately, and Shift+Enter or Ctrl+Enter inserts a newline
/// instead of sending.
/// </summary>
[TestClass]
public class SendConfirmationTests
{
    private static string TestHostPath =>
        Path.Combine(AppContext.BaseDirectory, "CDS.ScriptChat.TestHost.exe");

    [TestMethod]
    public void Enter_PressedInTheInputBox_SendsImmediately()
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

                var turnCountBefore = window.FindAllDescendants(cf => cf.ByAutomationId("_messageLabel")).Length;

                inputBox!.Focus();
                Keyboard.Type("one more question");
                Keyboard.Press(VirtualKeyShort.ENTER);
                Thread.Sleep(1000);

                var turnCountAfter = window.FindAllDescendants(cf => cf.ByAutomationId("_messageLabel")).Length;
                turnCountAfter.Should().Be(turnCountBefore + 2, "a bare Enter should send, adding a user turn and an assistant reply");
                automation.FocusedElement().Properties.AutomationId.Value.Should().Be(
                    "_inputTextBox", "focus should return to the input box once the turn completes");
            }
            finally
            {
                app.Close();
            }
        });
    }

    [TestMethod]
    public void CtrlEnter_PressedInTheInputBox_InsertsANewlineInsteadOfSending()
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

                var turnCountBefore = window.FindAllDescendants(cf => cf.ByAutomationId("_messageLabel")).Length;

                inputBox!.Focus();
                Keyboard.Type("line one");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
                Keyboard.Type("line two");
                Thread.Sleep(300);

                var turnCountAfter = window.FindAllDescendants(cf => cf.ByAutomationId("_messageLabel")).Length;
                turnCountAfter.Should().Be(turnCountBefore, "Ctrl+Enter should insert a newline, not send");

                var text = inputBox.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
                text.Should().Contain("line one").And.Contain("line two");
            }
            finally
            {
                app.Close();
            }
        });
    }

    [TestMethod]
    public void ShiftEnter_PressedInTheInputBox_InsertsANewlineInsteadOfSending()
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

                var turnCountBefore = window.FindAllDescendants(cf => cf.ByAutomationId("_messageLabel")).Length;

                inputBox!.Focus();
                Keyboard.Type("line one");
                Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.ENTER);
                Keyboard.Type("line two");
                Thread.Sleep(300);

                var turnCountAfter = window.FindAllDescendants(cf => cf.ByAutomationId("_messageLabel")).Length;
                turnCountAfter.Should().Be(turnCountBefore, "Shift+Enter should insert a newline, not send");

                var text = inputBox.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
                text.Should().Contain("line one").And.Contain("line two");
            }
            finally
            {
                app.Close();
            }
        });
    }
}
