using AwesomeAssertions;

using FlaUI.Core;
using FlaUI.UIA3;

namespace CDS.ScriptChat.WinForms.UITests;

/// <summary>
/// Drives the real TestHost app out-of-process via FlaUI to check that a seeded Markdown-bearing
/// assistant turn actually shows formatted text in a real, painted window — not just that the
/// in-process control geometry looks right, which the MSTest suite already covers.
/// </summary>
[TestClass]
public class MarkdownTurnRenderingTests
{
    private static string TestHostPath =>
        Path.Combine(AppContext.BaseDirectory, "CDS.ScriptChat.TestHost.exe");

    [TestMethod]
    public void MarkdownDemoTurn_AfterLaunch_RendersTextInTheTranscript()
    {
        StaThreadRunner.Run(() =>
        {
            using var app = FlaUI.Core.Application.Launch(TestHostPath, "--demo=markdown");
            using var automation = new UIA3Automation();

            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(10));
            window.Should().NotBeNull("the test host window should appear after launch");

            try
            {
                var transcript = window!.FindFirstDescendant(cf => cf.ByAutomationId("_transcriptTextBox"));
                transcript.Should().NotBeNull("the seeded user turn and assistant reply should both render into the transcript");

                var text = transcript!.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;

                text.Should().NotBeNullOrWhiteSpace("the assistant turn carries Markdown prose and a table");
                text.Should().Contain("Detectors");
                transcript.BoundingRectangle.Height.Should().BeGreaterThan(
                    20, "the transcript should be a real, painted control, not a collapsed one");
            }
            finally
            {
                app.Close();
            }
        });
    }
}
