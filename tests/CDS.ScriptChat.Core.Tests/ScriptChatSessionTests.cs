using AwesomeAssertions;

using Microsoft.Extensions.AI;

namespace CDS.ScriptChat.Core.Tests;

[TestClass]
[TestCategory("Session")]
public sealed class ScriptChatSessionTests
{
    private const string SampleScript = "var x = 1;";

    [TestMethod]
    public async Task SendAsync_QuestionWithNoImpliedChange_ReturnsTextWithoutProposal()
    {
        var client = new FakeChatClient(FakeChatClient.Text("The contour count dropped because the threshold rose."));
        var session = new ScriptChatSession(client);

        var result = await session.SendAsync("Why did the contour count drop?", SampleScript);

        result.Text.Should().Be("The contour count dropped because the threshold rose.");
        result.Proposal.Should().BeNull();
        result.ProposedEdit.Should().BeFalse();
    }

    [TestMethod]
    public async Task SendAsync_QuestionWithNoImpliedChange_RecordsNoPendingEditOnTheTurn()
    {
        var client = new FakeChatClient(FakeChatClient.Text("No change needed."));
        var session = new ScriptChatSession(client);

        await session.SendAsync("What does this script do?", SampleScript);

        var assistantTurn = session.Turns.Last();
        assistantTurn.Role.Should().Be(ChatTurnRole.Assistant);
        assistantTurn.HasProposedEdit.Should().BeFalse();
        assistantTurn.Disposition.Should().Be(EditDisposition.None);
    }

    [TestMethod]
    public async Task SendAsync_ModelCallsProposeScriptEdit_CapturesProposalAsPendingReview()
    {
        var client = new FakeChatClient(
            FakeChatClient.ToolCall("propose_script_edit", new Dictionary<string, object?>
            {
                ["newScript"] = "var x = 2;",
                ["summary"] = "Bump x to 2",
            }),
            FakeChatClient.Text("I have bumped x to 2."));

        var session = new ScriptChatSession(client);

        var result = await session.SendAsync("Set x to 2", SampleScript);

        result.ProposedEdit.Should().BeTrue();
        result.Proposal!.ProposedCode.Should().Be("var x = 2;");
        result.Proposal.Summary.Should().Be("Bump x to 2");

        var assistantTurn = session.Turns.Last();
        assistantTurn.ProposedCode.Should().Be("var x = 2;");
        assistantTurn.Disposition.Should().Be(EditDisposition.PendingReview);
    }

    [TestMethod]
    public async Task SendAsync_ModelCallsLookupSymbol_ResolvesViaProviderAndRecordsIt()
    {
        var provider = new StubSymbolLookupProvider(new Dictionary<string, SymbolLookupResult>
        {
            ["FindContours"] = new()
            {
                Signature = "void Cv2.FindContours(InputOutputArray image, out Point[][] contours, ...)",
                Namespace = "OpenCvSharp",
                XmlDocSummary = "Finds contours in a binary image.",
            },
        });

        var client = new FakeChatClient(
            FakeChatClient.ToolCall("lookup_symbol", new Dictionary<string, object?>
            {
                ["symbolName"] = "FindContours",
                ["containingType"] = "Cv2",
            }),
            FakeChatClient.Text("FindContours takes an InputOutputArray."));

        var session = new ScriptChatSession(client, new ScriptChatSessionOptions { SymbolLookup = provider });

        var result = await session.SendAsync("What are the parameters of FindContours?", SampleScript);

        provider.Calls.Should().ContainSingle()
            .Which.Should().Be(("FindContours", "Cv2"));
        result.SymbolsLookedUp.Should().ContainSingle().Which.Should().Be("Cv2.FindContours");
    }

    [TestMethod]
    public async Task SendAsync_UnknownSymbol_ReportsMissWithoutThrowing()
    {
        var provider = new StubSymbolLookupProvider([]);

        var client = new FakeChatClient(
            FakeChatClient.ToolCall("lookup_symbol", new Dictionary<string, object?>
            {
                ["symbolName"] = "NoSuchThing",
            }),
            FakeChatClient.Text("I could not find that symbol."));

        var session = new ScriptChatSession(client, new ScriptChatSessionOptions { SymbolLookup = provider });

        var result = await session.SendAsync("What is NoSuchThing?", SampleScript);

        result.Text.Should().Be("I could not find that symbol.");
        result.SymbolsLookedUp.Should().ContainSingle().Which.Should().Be("NoSuchThing");
    }

    [TestMethod]
    public async Task SendAsync_Always_ExposesBothToolsToTheModel()
    {
        var client = new FakeChatClient(FakeChatClient.Text("Fine."));
        var session = new ScriptChatSession(client);

        await session.SendAsync("Hello", SampleScript);

        client.LastOptions!.Tools!.Select(t => t.Name)
            .Should().BeEquivalentTo(["lookup_symbol", "propose_script_edit"]);
    }

    [TestMethod]
    public async Task SendAsync_FirstTurn_SendsOrientationBlurbInSystemPrompt()
    {
        var client = new FakeChatClient(FakeChatClient.Text("Understood."));
        var session = new ScriptChatSession(client, new ScriptChatSessionOptions
        {
            OrientationBlurb = "Scripts use a pull-based two-script model.",
        });

        await session.SendAsync("Hello", SampleScript);

        var systemMessage = client.ReceivedRequests[0].First(m => m.Role == ChatRole.System);
        systemMessage.Text.Should().Contain("Scripts use a pull-based two-script model.");
    }

    [TestMethod]
    public async Task SendAsync_Always_IncludesTheCurrentScriptInTheUserTurn()
    {
        var client = new FakeChatClient(FakeChatClient.Text("Fine."));
        var session = new ScriptChatSession(client);

        await session.SendAsync("Explain this", "var answer = 42;");

        var userMessage = client.ReceivedRequests[0].Last(m => m.Role == ChatRole.User);
        userMessage.Text.Should().Contain("var answer = 42;").And.Contain("Explain this");
    }

    [TestMethod]
    public async Task SendAsync_SecondTurn_ReusesTheExistingHistoryWithOneSystemPrompt()
    {
        var client = new FakeChatClient(
            FakeChatClient.Text("First answer."),
            FakeChatClient.Text("Second answer."));

        var session = new ScriptChatSession(client);

        await session.SendAsync("First question", SampleScript);
        await session.SendAsync("Second question", SampleScript);

        client.ReceivedRequests[1].Count(m => m.Role == ChatRole.System).Should().Be(1);
        client.ReceivedRequests[1].Count.Should().BeGreaterThan(client.ReceivedRequests[0].Count);
    }

    [TestMethod]
    public async Task SendAsync_EmptyUserMessage_Throws()
    {
        var client = new FakeChatClient(FakeChatClient.Text("Unused."));
        var session = new ScriptChatSession(client);

        var act = async () => await session.SendAsync("   ", SampleScript);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task SetEditDisposition_AcceptedOnProposingTurn_UpdatesTheTurn()
    {
        var client = new FakeChatClient(
            FakeChatClient.ToolCall("propose_script_edit", new Dictionary<string, object?>
            {
                ["newScript"] = "var x = 2;",
                ["summary"] = "Bump x",
            }),
            FakeChatClient.Text("Done."));

        var session = new ScriptChatSession(client);
        await session.SendAsync("Set x to 2", SampleScript);

        var turnIndex = session.Turns.Count - 1;
        session.SetEditDisposition(turnIndex, EditDisposition.Accepted);

        session.Turns[turnIndex].Disposition.Should().Be(EditDisposition.Accepted);
    }

    [TestMethod]
    public async Task SetEditDisposition_TurnWithNoProposal_Throws()
    {
        var client = new FakeChatClient(FakeChatClient.Text("Just prose."));
        var session = new ScriptChatSession(client);
        await session.SendAsync("A question", SampleScript);

        var act = () => session.SetEditDisposition(session.Turns.Count - 1, EditDisposition.Accepted);

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public async Task SetEditDisposition_PendingReview_ThrowsBecauseItIsNotAUserDecision()
    {
        var client = new FakeChatClient(
            FakeChatClient.ToolCall("propose_script_edit", new Dictionary<string, object?>
            {
                ["newScript"] = "var x = 2;",
                ["summary"] = "Bump x",
            }),
            FakeChatClient.Text("Done."));

        var session = new ScriptChatSession(client);
        await session.SendAsync("Set x to 2", SampleScript);

        var act = () => session.SetEditDisposition(session.Turns.Count - 1, EditDisposition.PendingReview);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public async Task Reset_AfterATurn_ClearsTranscriptAndStartsAFreshSystemPrompt()
    {
        var client = new FakeChatClient(
            FakeChatClient.Text("First."),
            FakeChatClient.Text("Second."));

        var session = new ScriptChatSession(client);
        await session.SendAsync("First question", SampleScript);

        session.Reset();

        session.Turns.Should().BeEmpty();

        await session.SendAsync("Second question", SampleScript);

        // A fresh conversation, not a continuation: system prompt plus the one user turn.
        client.ReceivedRequests[1].Should().HaveCount(2);
    }
}
