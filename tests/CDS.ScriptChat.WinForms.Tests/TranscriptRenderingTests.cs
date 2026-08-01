using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.Core.Tests;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers the transcript actually being visible. A turn view that lays out at zero width still
/// binds its text, raises no exception and logs a completed turn — the panel just renders
/// nothing — so the geometry needs asserting rather than the state alone.
/// </summary>
[TestClass]
[TestCategory("TranscriptRendering")]
public sealed class TranscriptRenderingTests
{
    private const int PanelWidth = 550;
    private const string Script = "var x = 1;";

    [TestMethod]
    public async Task AppendTurn_AfterASend_GivesEveryTurnViewTheTranscriptWidth()
    {
        using var panel = await CreatePanelWithOneTurnAsync();
        var transcript = FindTranscript(panel);

        var views = transcript.Controls.OfType<ChatTurnView>().ToArray();

        views.Should().HaveCount(2, "the user turn and the assistant reply are both rendered");
        foreach (var view in views)
        {
            // The turn views are sized by the panel, not by themselves: a docked child
            // contributes no preferred width, so autosizing collapses them to nothing.
            view.Width.Should().BeGreaterThan(PanelWidth / 2);
        }
    }

    [TestMethod]
    public async Task AppendTurn_WithProseTooLongForOneLine_WrapsItAcrossTheTurnWidth()
    {
        using var panel = await CreatePanelWithOneTurnAsync();

        // The assistant reply is the long one; a short user message autosizes to a narrow label
        // however much room it is given, so it says nothing about the available width.
        var assistant = FindTranscript(panel).Controls.OfType<ChatTurnView>().Last();
        var message = assistant.Controls.Find("_messageLabel", searchAllChildren: true).Single();

        // A collapsed view still lays its label out at a pixel or two, so this asserts the prose
        // has real room rather than merely a positive width.
        message.Width.Should().BeGreaterThan(PanelWidth / 2);
    }

    [TestMethod]
    public async Task AppendTurn_AfterASend_MakesEveryTurnViewTallEnoughForItsProse()
    {
        using var panel = await CreatePanelWithOneTurnAsync();

        foreach (var view in FindTranscript(panel).Controls.OfType<ChatTurnView>())
        {
            var message = view.Controls.Find("_messageLabel", searchAllChildren: true).Single();

            view.Height.Should().BeGreaterThanOrEqualTo(message.Bottom);
        }
    }

    [TestMethod]
    public async Task ClearTranscript_WithSeveralTurns_DisposesEveryTurnView()
    {
        using var panel = await CreatePanelWithOneTurnAsync();
        var views = FindTranscript(panel).Controls.OfType<ChatTurnView>().ToArray();

        panel.ClearTranscript();

        views.Should().OnlyContain(view => view.IsDisposed);
    }

    /// <summary>
    /// Builds a sized panel and drives one real turn through it, so the transcript holds the
    /// same views a live send produces.
    /// </summary>
    private static async Task<ScriptChatPanel> CreatePanelWithOneTurnAsync()
    {
        var session = new ScriptChatSession(
            new FakeChatClient(FakeChatClient.Text(
                "Short answer: not yet. Two things block me, and both come down to what the "
                + "host actually exposes rather than anything in the script itself.")));

        var panel = new ScriptChatPanel { Size = new Size(PanelWidth, 400) };
        panel.ScriptTextProvider = () => Script;

        await session.SendAsync("Do you know how to generate code for this test?", Script);
        panel.AttachSession(session);

        return panel;
    }

    private static FlowLayoutPanel FindTranscript(ScriptChatPanel panel) =>
        panel.Controls.Find("_transcriptPanel", searchAllChildren: true)
            .OfType<FlowLayoutPanel>().Single();
}
