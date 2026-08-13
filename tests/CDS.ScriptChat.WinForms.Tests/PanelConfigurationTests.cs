using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.Core.Tests;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers the milestone-1 acceptance criterion that changing provider or model takes effect
/// without restarting the host app (D10 — the conversation restarts, the app does not).
/// </summary>
[TestClass]
[TestCategory("Configuration")]
public sealed class PanelConfigurationTests
{
    private static ScriptChatClientOptions ClaudeOptions => new()
    {
        Provider = ScriptChatProvider.Claude,
        ApiKey = "sk-ant-not-a-real-key",
        ModelId = ScriptChatModels.ClaudeDefault,
    };

    [TestMethod]
    public void Configure_ValidOptions_MakesThePanelReady()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };

        panel.Configure(ClaudeOptions);

        panel.IsReady.Should().BeTrue();
    }

    [TestMethod]
    public void Configure_MissingApiKey_LeavesThePanelUnavailableRatherThanThrowing()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };

        var act = () => panel.Configure(ClaudeOptions with { ApiKey = "   " });

        act.Should().NotThrow();
        panel.IsReady.Should().BeFalse();
    }

    [TestMethod]
    public void Configure_ProviderNotYetWiredUp_LeavesThePanelUnavailable()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };

        panel.Configure(ClaudeOptions with { Provider = ScriptChatProvider.Grok, ModelId = "grok-4" });

        panel.IsReady.Should().BeFalse();
    }

    [TestMethod]
    public void Configure_AfterAFailedAttempt_RecoversWithValidOptions()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };

        panel.Configure(ClaudeOptions with { ApiKey = "   " });
        panel.Configure(ClaudeOptions);

        panel.IsReady.Should().BeTrue();
    }

    [TestMethod]
    public async Task Configure_CalledAgain_StartsAFreshConversation()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };
        var session = new ScriptChatSession(new FakeChatClient(FakeChatClient.Text("Hello.")));
        panel.AttachSession(session);
        await session.SendAsync("A question", "var x = 1;");
        panel.AttachSession(session);

        panel.Configure(ClaudeOptions with { ModelId = "claude-sonnet-5" });

        // History is not carried across a provider or model change (D10).
        FindTranscript(panel).Controls.Count.Should().Be(0);
    }

    [TestMethod]
    public void Configure_NullOptions_Throws()
    {
        using var panel = new ScriptChatPanel();

        var act = () => panel.Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public async Task AttachSession_SessionWithAPastProposal_StillRendersItAsADiff()
    {
        var client = new FakeChatClient(
            FakeChatClient.ToolCall("propose_script_edit", new Dictionary<string, object?>
            {
                ["newScript"] = "one\ntwo",
                ["summary"] = "Add a line",
            }),
            FakeChatClient.Text("Added a line."));

        var session = new ScriptChatSession(client);
        await session.SendAsync("Add a line", "one");

        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "one" };
        panel.AttachSession(session);

        // The baseline travels with the session, so a reloaded transcript still shows what
        // changed rather than just the whole proposed script.
        var diffBox = FindTranscript(panel).Controls.OfType<ChatTurnView>().Last()
            .Controls.Find("_diffTextBox", searchAllChildren: true).OfType<RichTextBox>().Single();

        diffBox.Lines.Should().Contain("  one").And.Contain("+ two");
    }

    private static FlowLayoutPanel FindTranscript(ScriptChatPanel panel) =>
        panel.Controls.Find("_transcriptPanel", searchAllChildren: true).OfType<FlowLayoutPanel>().Single();
}
