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
                var transcript = window.FindFirstDescendant(cf => cf.ByAutomationId("_transcriptTextBox"));
                transcript.Should().NotBeNull();

                var textBefore = GetTranscriptText(transcript!);

                inputBox!.Focus();
                Keyboard.Type("one more question");
                Keyboard.Press(VirtualKeyShort.ENTER);
                Thread.Sleep(1000);

                var textAfter = GetTranscriptText(transcript!);
                textAfter.Should().Contain("one more question", "a bare Enter should send, appending the typed message to the transcript");
                textAfter.Length.Should().BeGreaterThan(textBefore.Length, "the assistant's reply should also have been appended");
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
                var transcript = window.FindFirstDescendant(cf => cf.ByAutomationId("_transcriptTextBox"));
                transcript.Should().NotBeNull();

                var textBefore = GetTranscriptText(transcript!);

                inputBox!.Focus();
                Keyboard.Type("line one");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
                Keyboard.Type("line two");
                Thread.Sleep(300);

                GetTranscriptText(transcript!).Should().Be(textBefore, "Ctrl+Enter should insert a newline, not send");

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
                var transcript = window.FindFirstDescendant(cf => cf.ByAutomationId("_transcriptTextBox"));
                transcript.Should().NotBeNull();

                var textBefore = GetTranscriptText(transcript!);

                inputBox!.Focus();
                Keyboard.Type("line one");
                Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.ENTER);
                Keyboard.Type("line two");
                Thread.Sleep(300);

                GetTranscriptText(transcript!).Should().Be(textBefore, "Shift+Enter should insert a newline, not send");

                var text = inputBox.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
                text.Should().Contain("line one").And.Contain("line two");
            }
            finally
            {
                app.Close();
            }
        });
    }

    private static string GetTranscriptText(FlaUI.Core.AutomationElements.AutomationElement transcript) =>
        transcript.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? string.Empty;
}
