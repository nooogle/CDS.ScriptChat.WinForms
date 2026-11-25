using AwesomeAssertions;

using CDS.Markdown;
using CDS.ScriptChat.Core;
using CDS.ScriptChat.Core.Tests;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// A host may attach a different session while a turn is in flight — switching targets, or
/// replacing them when its document changes. The reply belongs to the session it was sent on.
/// </summary>
[TestClass]
[TestCategory("ReplacedSession")]
public sealed class ReplacedSessionTests
{
    private const string Script = "var x = 1;";
    private const string Reply = "Here is my answer.";

    [TestMethod]
    public async Task Send_WhenAnotherSessionIsAttachedMidTurn_LeavesTheNewTranscriptAlone()
    {
        var (panel, _, _) = await SendAcrossASessionSwitchAsync();
        using (panel)
        {
            FindTranscript(panel).TextLength.Should().Be(0, "the reply belongs to the conversation it was sent in");
        }
    }

    [TestMethod]
    public async Task Send_WhenAnotherSessionIsAttachedMidTurn_RecordsTheReplyOnTheSessionItWasSentOn()
    {
        var (panel, sent, _) = await SendAcrossASessionSwitchAsync();
        using (panel)
        {
            sent.Turns.Should().HaveCount(2);
            sent.Turns[^1].Text.Should().Contain(Reply);
        }
    }

    [TestMethod]
    public async Task Send_WhenAnotherSessionIsAttachedMidTurn_LeavesThatSessionEmpty()
    {
        var (panel, _, attached) = await SendAcrossASessionSwitchAsync();
        using (panel)
        {
            attached.Turns.Should().BeEmpty();
        }
    }

    private static async Task<(ScriptChatPanel Panel, ScriptChatSession Sent, ScriptChatSession Attached)> SendAcrossASessionSwitchAsync()
    {
        var held = new HeldChatClient();
        var sent = new ScriptChatSession(held);
        var attached = new ScriptChatSession(new FakeChatClient());

        var panel = new ScriptChatPanel();
        panel.ScriptTextProvider = () => Script;
        panel.AttachSession(sent);
        FindInputBox(panel).Text = "Set x to 2";

        // Constructing the panel installed a WindowsFormsSynchronizationContext that nothing here
        // pumps; without clearing it the send's continuation would never run.
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var send = panel.SendCurrentInputAsync();
            panel.AttachSession(attached);
            held.Release(FakeChatClient.Text(Reply));
            await send;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        return (panel, sent, attached);
    }

    private static TextBoxBase FindInputBox(ScriptChatPanel panel) =>
        panel.Controls.Find("_inputTextBox", searchAllChildren: true).OfType<TextBoxBase>().Single();

    private static MarkdownTextBox FindTranscript(ScriptChatPanel panel) =>
        panel.Controls.Find("_transcriptTextBox", searchAllChildren: true)
            .OfType<MarkdownTextBox>().Single();
}
