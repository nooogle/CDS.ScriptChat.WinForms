using AwesomeAssertions;

using FlaUI.Core;
using FlaUI.UIA3;

namespace CDS.ScriptChat.WinForms.UITests;

/// <summary>
/// Drives the real TestHost app out-of-process via FlaUI to check that a seeded Markdown-bearing
/// assistant turn actually shows formatted text in a real, painted window — not just that the
/// in-process control geometry looks right, which the MSTest suite already covers. A turn view
/// binds and lays out before it is parented into the transcript, and that ordering is exactly
/// where <see cref="MarkdownTextBox"/>'s height calculation previously broke silently: the
/// control existed with the right text but zero height, so nothing was visible.
/// </summary>
[TestClass]
public class MarkdownTurnRenderingTests
{
    private static string TestHostPath =>
        Path.Combine(AppContext.BaseDirectory, "CDS.ScriptChat.TestHost.exe");

    [TestMethod]
    public void MarkdownDemoTurn_AfterLaunch_RendersTextInTheAssistantBubble()
    {
        StaThreadRunner.Run(() =>
        {
            using var app = FlaUI.Core.Application.Launch(TestHostPath, "--demo=markdown");
            using var automation = new UIA3Automation();

            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(10));
            window.Should().NotBeNull("the test host window should appear after launch");

            try
            {
                var messageBoxes = window!.FindAllDescendants(cf => cf.ByAutomationId("_messageLabel"));
                messageBoxes.Should().HaveCountGreaterThanOrEqualTo(
                    2, "the seeded user turn and assistant reply should both render a ChatTurnView with a _messageLabel");

                var assistantMessage = messageBoxes[^1];
                var text = assistantMessage.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;

                text.Should().NotBeNullOrWhiteSpace("the assistant turn carries Markdown prose and a table");
                text.Should().Contain("Detectors");
                assistantMessage.BoundingRectangle.Height.Should().BeGreaterThan(
                    20, "a real multi-line reply needs real screen height, not just a single collapsed row");
            }
            finally
            {
                app.Close();
            }
        });
    }
}
