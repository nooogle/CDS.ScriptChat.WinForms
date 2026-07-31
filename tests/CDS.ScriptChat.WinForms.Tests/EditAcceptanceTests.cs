using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.Core.Tests;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers the milestone-1 acceptance criterion that a proposed edit leaves the script alone
/// until the user clicks Accept (D5).
/// </summary>
[TestClass]
[TestCategory("EditAcceptance")]
public sealed class EditAcceptanceTests
{
    private const string OriginalScript = "var x = 1;";
    private const string ProposedScript = "var x = 2;";

    [TestMethod]
    public async Task ProposedEdit_BeforeAnyDecision_LeavesTheScriptUntouched()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();

        harness.CurrentScript.Should().Be(OriginalScript);
        harness.SetterCallCount.Should().Be(0);
    }

    [TestMethod]
    public async Task ProposedEdit_BeforeAnyDecision_IsPendingReview()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();

        harness.Session.Turns[^1].Disposition.Should().Be(EditDisposition.PendingReview);
    }

    [TestMethod]
    public async Task Accept_Clicked_AppliesTheProposalThroughTheHostSetter()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();

        harness.ClickAccept();

        harness.CurrentScript.Should().Be(ProposedScript);
        harness.SetterCallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Accept_Clicked_MarksTheTurnAccepted()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();

        harness.ClickAccept();

        harness.Session.Turns[^1].Disposition.Should().Be(EditDisposition.Accepted);
    }

    [TestMethod]
    public async Task Accept_Clicked_RaisesEditAcceptedWithTheAppliedScript()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();
        ScriptEditAcceptedEventArgs? raised = null;
        harness.Panel.EditAccepted += (_, e) => raised = e;

        harness.ClickAccept();

        raised.Should().NotBeNull();
        raised!.ProposedCode.Should().Be(ProposedScript);
        raised.Summary.Should().Be("Bump x to 2");
    }

    [TestMethod]
    public async Task Reject_Clicked_LeavesTheScriptUntouched()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();

        harness.ClickReject();

        harness.CurrentScript.Should().Be(OriginalScript);
        harness.SetterCallCount.Should().Be(0);
        harness.Session.Turns[^1].Disposition.Should().Be(EditDisposition.Rejected);
    }

    [TestMethod]
    public async Task Accept_ClickedTwice_AppliesTheEditOnlyOnce()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();

        harness.ClickAccept();
        harness.ClickAccept();

        harness.SetterCallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Accept_AfterReject_DoesNotApplyTheEdit()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();

        harness.ClickReject();
        harness.ClickAccept();

        harness.CurrentScript.Should().Be(OriginalScript);
        harness.SetterCallCount.Should().Be(0);
    }

    [TestMethod]
    public async Task Accept_WithNoScriptSetterConfigured_LeavesTheTurnPending()
    {
        using var harness = await ProposingPanelHarness.CreateAsync(wireSetter: false);

        harness.ClickAccept();

        // Nothing was applied, so the proposal must stay actionable rather than reading as done.
        harness.Session.Turns[^1].Disposition.Should().Be(EditDisposition.PendingReview);
    }

    [TestMethod]
    public async Task Accept_WhenTheHostSetterThrows_LeavesTheTurnPending()
    {
        using var harness = await ProposingPanelHarness.CreateAsync(
            setterBehaviour: _ => throw new InvalidOperationException("Editor is read-only."));

        harness.ClickAccept();

        harness.Session.Turns[^1].Disposition.Should().Be(EditDisposition.PendingReview);
    }

    [TestMethod]
    public async Task TextOnlyTurn_Always_OffersNoAcceptOrReject()
    {
        using var harness = await ProposingPanelHarness.CreateAsync(proposeEdit: false);

        harness.FindActionsPanel().Visible.Should().BeFalse();
    }

    [TestMethod]
    public async Task ProposedEdit_PendingReview_ShowsAcceptAndReject()
    {
        using var harness = await ProposingPanelHarness.CreateAsync();

        harness.FindActionsPanel().Visible.Should().BeTrue();
    }

    /// <summary>
    /// Drives a panel through one turn that proposes an edit, and exposes the pieces the tests
    /// need to poke at.
    /// </summary>
    private sealed class ProposingPanelHarness : IDisposable
    {
        private ProposingPanelHarness(ScriptChatPanel panel, ScriptChatSession session)
        {
            Panel = panel;
            Session = session;
        }

        public ScriptChatPanel Panel { get; }

        public ScriptChatSession Session { get; }

        public string CurrentScript { get; private set; } = OriginalScript;

        public int SetterCallCount { get; private set; }

        public static async Task<ProposingPanelHarness> CreateAsync(
            bool proposeEdit = true,
            bool wireSetter = true,
            Action<string>? setterBehaviour = null)
        {
            var client = proposeEdit
                ? new FakeChatClient(
                    FakeChatClient.ToolCall("propose_script_edit", new Dictionary<string, object?>
                    {
                        ["newScript"] = ProposedScript,
                        ["summary"] = "Bump x to 2",
                    }),
                    FakeChatClient.Text("I have bumped x to 2."))
                : new FakeChatClient(FakeChatClient.Text("Nothing needs to change."));

            var session = new ScriptChatSession(client);
            var panel = new ScriptChatPanel();
            var harness = new ProposingPanelHarness(panel, session);

            panel.ScriptTextProvider = () => harness.CurrentScript;

            if (wireSetter)
            {
                panel.ScriptTextSetter = script =>
                {
                    harness.SetterCallCount++;
                    if (setterBehaviour is not null)
                    {
                        setterBehaviour(script);
                        return;
                    }

                    harness.CurrentScript = script;
                };
            }

            panel.AttachSession(session);

            // Drive a real turn through the session so the panel binds genuine turn state.
            await session.SendAsync("Set x to 2", harness.CurrentScript);
            panel.AttachSession(session);
            harness.RebindLastTurnWithBaseline();

            return harness;
        }

        public void ClickAccept() => FindButton("_acceptButton").PerformClick();

        public void ClickReject() => FindButton("_rejectButton").PerformClick();

        public FlowLayoutPanel FindActionsPanel() =>
            LastTurnView().Controls.Find("_actionsPanel", searchAllChildren: true)
                .OfType<FlowLayoutPanel>().Single();

        public void Dispose() => Panel.Dispose();

        /// <summary>
        /// <see cref="ScriptChatPanel.AttachSession"/> restores turns without a baseline, which
        /// is faithful to the real restore path but skips diff rendering. Re-binding with the
        /// original script exercises the diff path the live send uses.
        /// </summary>
        private void RebindLastTurnWithBaseline()
        {
            LastTurnView().Bind(Session.Turns[^1], OriginalScript);
        }

        private ChatTurnView LastTurnView()
        {
            var transcript = Panel.Controls.Find("_transcriptPanel", searchAllChildren: true)
                .OfType<FlowLayoutPanel>().Single();
            return transcript.Controls.OfType<ChatTurnView>().Last();
        }

        private Button FindButton(string name) =>
            LastTurnView().Controls.Find(name, searchAllChildren: true).OfType<Button>().Single();
    }
}
