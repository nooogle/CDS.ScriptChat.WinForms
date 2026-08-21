using AwesomeAssertions;

using CDS.Markdown;
using CDS.ScriptChat.Core;
using CDS.ScriptChat.Core.Tests;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers the transcript rendering both turns of a send into the single, continuously-appended
/// <see cref="MarkdownTextBox"/> that replaced the earlier per-turn control layout.
/// </summary>
[TestClass]
[TestCategory("TranscriptRendering")]
public sealed class TranscriptRenderingTests
{
    private const string Script = "var x = 1;";
    private const string UserMessage = "Do you know how to generate code for this test?";
    private const string AssistantReply = "Short answer: not yet.";

    [TestMethod]
    public async Task AppendTurn_AfterASend_RendersBothTurnsInOrder()
    {
        using var panel = await CreatePanelWithOneTurnAsync();

        var text = FindTranscript(panel).Text;
        text.Should().Contain(UserMessage).And.Contain(AssistantReply);
        text.IndexOf(UserMessage, StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf(AssistantReply, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AppendTurn_AfterASend_LabelsEachTurnWithItsSpeaker()
    {
        using var panel = await CreatePanelWithOneTurnAsync();

        var text = FindTranscript(panel).Text;
        text.Should().Contain("You").And.Contain("Assistant");
    }

    [TestMethod]
    public async Task ClearTranscript_WithSeveralTurns_LeavesTheTranscriptEmpty()
    {
        using var panel = await CreatePanelWithOneTurnAsync();

        panel.ClearTranscript();

        FindTranscript(panel).TextLength.Should().Be(0);
    }

    /// <summary>
    /// Drives one real turn through a panel, so the transcript holds the same content a live
    /// send produces.
    /// </summary>
    private static async Task<ScriptChatPanel> CreatePanelWithOneTurnAsync()
    {
        var session = new ScriptChatSession(new FakeChatClient(FakeChatClient.Text(AssistantReply)));

        var panel = new ScriptChatPanel { Size = new Size(420, 560) };
        panel.ScriptTextProvider = () => Script;

        await session.SendAsync(UserMessage, Script);
        panel.AttachSession(session);

        return panel;
    }

    private static MarkdownTextBox FindTranscript(ScriptChatPanel panel) =>
        panel.Controls.Find("_transcriptTextBox", searchAllChildren: true)
            .OfType<MarkdownTextBox>().Single();
}
