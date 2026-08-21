using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.Core.Tests;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers accept/reject for a patch proposal (Job 3): unlike a full-script replacement, accepting
/// a patch re-applies its hunks to a fresh read of the buffer rather than the frozen baseline the
/// diff was rendered against, so it can fail cleanly if the buffer changed in the meantime.
/// </summary>
[TestClass]
[TestCategory("EditAcceptance")]
public sealed class PatchAcceptanceTests
{
    private const string OriginalScript = "var x = 1;\nvar y = 2;";

    [TestMethod]
    public async Task ProposedPatch_BeforeAnyDecision_LeavesTheScriptUntouched()
    {
        using var harness = await ProposingPatchPanelHarness.CreateAsync();

        harness.CurrentScript.Should().Be(OriginalScript);
        harness.SetterCallCount.Should().Be(0);
    }

    [TestMethod]
    public async Task ProposedPatch_BeforeAnyDecision_IsPendingReview()
    {
        using var harness = await ProposingPatchPanelHarness.CreateAsync();

        harness.Session.Turns[^1].Disposition.Should().Be(EditDisposition.PendingReview);
    }

    [TestMethod]
    public async Task Accept_Clicked_AppliesTheHunkThroughTheHostSetter()
    {
        using var harness = await ProposingPatchPanelHarness.CreateAsync();

        harness.ClickAccept();

        // Models emit bare "\n"; the panel normalises to the platform's line ending.
        harness.CurrentScript.Should().Be($"var x = 2;{Environment.NewLine}var y = 2;");
        harness.SetterCallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Accept_Clicked_AppliesAgainstTheCurrentBufferNotTheFrozenBaseline()
    {
        // The user made an unrelated edit to the buffer after the proposal but before deciding
        // on it. The hunk's anchor is untouched, so it should still apply cleanly on top of that
        // other change — proving accept reads the buffer fresh rather than using the baseline
        // the diff was rendered against.
        using var harness = await ProposingPatchPanelHarness.CreateAsync();
        harness.CurrentScript = "var x = 1;\nvar y = 99;";

        harness.ClickAccept();

        harness.CurrentScript.Should().Be($"var x = 2;{Environment.NewLine}var y = 99;");
    }

    [TestMethod]
    public async Task Accept_WhenTheHunkNoLongerMatchesTheBuffer_LeavesTheTurnPending()
    {
        // The user edited away the exact text the hunk anchors to, so it can no longer apply —
        // this must fail closed (leave the turn pending) rather than silently doing nothing or
        // applying to the wrong place.
        using var harness = await ProposingPatchPanelHarness.CreateAsync();
        harness.CurrentScript = "var x = 100;\nvar y = 2;";

        harness.ClickAccept();

        harness.SetterCallCount.Should().Be(0);
        harness.Session.Turns[^1].Disposition.Should().Be(EditDisposition.PendingReview);
    }

    [TestMethod]
    public async Task Reject_Clicked_LeavesTheScriptUntouched()
    {
        using var harness = await ProposingPatchPanelHarness.CreateAsync();

        harness.ClickReject();

        harness.CurrentScript.Should().Be(OriginalScript);
        harness.SetterCallCount.Should().Be(0);
        harness.Session.Turns[^1].Disposition.Should().Be(EditDisposition.Rejected);
    }

    /// <summary>
    /// Drives a panel through one turn that proposes a single-hunk patch, and exposes the pieces
    /// the tests need to poke at.
    /// </summary>
    private sealed class ProposingPatchPanelHarness : IDisposable
    {
        private ProposingPatchPanelHarness(ScriptChatPanel panel, ScriptChatSession session)
        {
            Panel = panel;
            Session = session;
        }

        public ScriptChatPanel Panel { get; }

        public ScriptChatSession Session { get; }

        public string CurrentScript { get; set; } = OriginalScript;

        public int SetterCallCount { get; private set; }

        public static async Task<ProposingPatchPanelHarness> CreateAsync()
        {
            var client = new FakeChatClient(
                FakeChatClient.ToolCall("propose_script_patch", new Dictionary<string, object?>
                {
                    ["hunks"] = new[]
                    {
                        new Dictionary<string, object?> { ["oldText"] = "var x = 1;", ["newText"] = "var x = 2;" },
                    },
                    ["summary"] = "Bump x to 2",
                }),
                FakeChatClient.Text("I have bumped x to 2."));

            var session = new ScriptChatSession(client);
            var panel = new ScriptChatPanel();
            var harness = new ProposingPatchPanelHarness(panel, session);

            panel.ScriptTextProvider = () => harness.CurrentScript;
            panel.ScriptTextSetter = script =>
            {
                harness.SetterCallCount++;
                harness.CurrentScript = script;
            };

            panel.AttachSession(session);

            await session.SendAsync("Set x to 2", harness.CurrentScript);
            panel.AttachSession(session);

            return harness;
        }

        public void ClickAccept() => FindButton("_acceptButton").PerformClick();

        public void ClickReject() => FindButton("_rejectButton").PerformClick();

        public void Dispose() => Panel.Dispose();

        private Button FindButton(string name) =>
            Panel.Controls.Find(name, searchAllChildren: true).OfType<Button>().Single();
    }
}
