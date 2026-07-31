using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// The Designer files are hand-written rather than emitted by Visual Studio, so these tests
/// prove <c>InitializeComponent</c> actually runs and the controls bind — a broken Designer
/// file otherwise compiles happily and only fails when a host app first shows the panel.
/// </summary>
[TestClass]
[TestCategory("Designer")]
public sealed class DesignerSmokeTests
{
    [TestMethod]
    public void ScriptChatPanel_Constructed_InitialisesWithoutThrowing()
    {
        using var panel = new ScriptChatPanel();

        panel.Controls.Count.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void ScriptChatPanel_NoSessionAttached_IsNotReady()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };

        panel.IsReady.Should().BeFalse();
    }

    [TestMethod]
    public void ScriptChatPanel_SessionAndScriptSourceBothSet_IsReady()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };
        panel.AttachSession(new ScriptChatSession(new StubChatClient()));

        panel.IsReady.Should().BeTrue();
    }

    [TestMethod]
    public void ScriptChatPanel_SessionAttachedButNoScriptSource_IsNotReady()
    {
        using var panel = new ScriptChatPanel();
        panel.AttachSession(new ScriptChatSession(new StubChatClient()));

        panel.IsReady.Should().BeFalse();
    }

    [TestMethod]
    public void SetUnavailable_Always_LeavesThePanelNotReady()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };
        panel.AttachSession(new ScriptChatSession(new StubChatClient()));

        panel.SetUnavailable("No API key configured.");

        panel.IsReady.Should().BeFalse();
    }

    [TestMethod]
    public void AttachSession_WithExistingTurns_RendersThemIntoTheTranscript()
    {
        using var panel = new ScriptChatPanel { ScriptTextProvider = () => "var x = 1;" };
        var session = new ScriptChatSession(new StubChatClient());

        panel.AttachSession(session);

        // A fresh session has no turns, so the transcript starts empty.
        FindTranscript(panel).Controls.Count.Should().Be(0);
    }

    [TestMethod]
    public void ClearTranscript_AfterAttach_LeavesTheTranscriptEmpty()
    {
        using var panel = new ScriptChatPanel();

        panel.ClearTranscript();

        FindTranscript(panel).Controls.Count.Should().Be(0);
    }

    [TestMethod]
    public void ChatTurnView_UserTurn_BindsWithoutThrowing()
    {
        using var view = new ChatTurnView();

        var act = () => view.Bind(new ChatTurn(ChatTurnRole.User, "Add denoising", null, null, EditDisposition.None));

        act.Should().NotThrow();
    }

    [TestMethod]
    public void ChatTurnView_AssistantTurnProposingAnEdit_BindsWithoutThrowing()
    {
        using var view = new ChatTurnView();

        var act = () => view.Bind(new ChatTurn(
            ChatTurnRole.Assistant,
            "I have added a denoising step.",
            "var denoised = Denoise(src);",
            "Add denoising",
            EditDisposition.PendingReview));

        act.Should().NotThrow();
    }

    [TestMethod]
    public void ChatTurnView_ProposalWithNoBaseline_ShowsTheProposedCodeInFull()
    {
        using var view = new ChatTurnView();

        view.Bind(
            new ChatTurn(ChatTurnRole.Assistant, null, "line one\nline two", "Two lines", EditDisposition.PendingReview),
            baselineScript: null);

        var diffBox = FindDiffTextBox(view);
        diffBox.Lines.Should().Equal("line one", "line two");
        diffBox.Visible.Should().BeTrue();
    }

    [TestMethod]
    public void ChatTurnView_ProposalWithBaseline_RendersMarkedUpDiffLines()
    {
        using var view = new ChatTurnView();

        view.Bind(
            new ChatTurn(ChatTurnRole.Assistant, null, "one\ntwo", "Add a line", EditDisposition.PendingReview),
            baselineScript: "one");

        FindDiffTextBox(view).Lines.Should().Contain("  one").And.Contain("+ two");
    }

    [TestMethod]
    public void ChatTurnView_ProposalIdenticalToTheCurrentScript_SaysSoRatherThanShowingAnEmptyDiff()
    {
        using var view = new ChatTurnView();

        view.Bind(
            new ChatTurn(ChatTurnRole.Assistant, null, "one", "No-op", EditDisposition.PendingReview),
            baselineScript: "one");

        FindDiffTextBox(view).Text.Should().Contain("identical");
    }

    [TestMethod]
    public void ChatTurnView_TurnWithNoProposedCode_HidesTheDiffBox()
    {
        using var view = new ChatTurnView();

        view.Bind(new ChatTurn(ChatTurnRole.Assistant, "Just an answer.", null, null, EditDisposition.None));

        FindDiffTextBox(view).Visible.Should().BeFalse();
    }

    [TestMethod]
    public void ChatTurnView_NullTurn_Throws()
    {
        using var view = new ChatTurnView();

        var act = () => view.Bind(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static FlowLayoutPanel FindTranscript(ScriptChatPanel panel) =>
        panel.Controls.Find("_transcriptPanel", searchAllChildren: true).OfType<FlowLayoutPanel>().Single();

    private static RichTextBox FindDiffTextBox(ChatTurnView view) =>
        view.Controls.Find("_diffTextBox", searchAllChildren: true).OfType<RichTextBox>().Single();
}
