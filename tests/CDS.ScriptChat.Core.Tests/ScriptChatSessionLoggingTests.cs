using AwesomeAssertions;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Covers what the session records, and — for the levels a shipping host would enable — what it
/// must not (D16).
/// </summary>
[TestClass]
[TestCategory("Logging")]
public sealed class ScriptChatSessionLoggingTests
{
    private const string SampleScript = "var secretScriptContents = 1;";
    private const string SampleQuestion = "What does this do, and mention pumpernickel?";

    [TestMethod]
    public async Task SendAsync_WithALogger_RecordsTurnStartAndCompletion()
    {
        var capture = new CapturingLoggerProvider();
        var session = NewSession(capture, FakeChatClient.Text("It sets a variable."));

        await session.SendAsync(SampleQuestion, SampleScript);

        capture.Entries.Select(e => e.EventId.Name)
            .Should().Contain(["TurnStarted", "TurnCompleted"]);
    }

    [TestMethod]
    public async Task SendAsync_WithALogger_RecordsElapsedTimeAndTokenCountsOnCompletion()
    {
        var capture = new CapturingLoggerProvider();
        var response = FakeChatClient.Text("It sets a variable.");
        response.Usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 8 };

        var session = NewSession(capture, response);

        await session.SendAsync(SampleQuestion, SampleScript);

        var completed = capture.Entries.Single(e => e.EventId.Name == "TurnCompleted");
        completed.Message.Should().Contain("InputTokens=120").And.Contain("OutputTokens=8");
        completed.Message.Should().MatchRegex(@"completed in \d+ms");
    }

    [TestMethod]
    public async Task SendAsync_ModelCallsLookupSymbol_RecordsTheCallAndItsOutcome()
    {
        var capture = new CapturingLoggerProvider();
        var provider = new StubSymbolLookupProvider(new Dictionary<string, SymbolLookupResult>
        {
            ["FindContours"] = new() { Signature = "void FindContours()", Namespace = "Kestrel" },
        });

        var session = NewSession(
            capture,
            provider,
            FakeChatClient.ToolCall("lookup_symbol", new Dictionary<string, object?> { ["symbolName"] = "FindContours" }),
            FakeChatClient.Text("Resolved."));

        await session.SendAsync("Tell me about FindContours", SampleScript);

        var names = capture.Entries.Select(e => e.EventId.Name).ToList();
        names.Should().Contain("SymbolLookupRequested").And.Contain("SymbolLookupResolved");
    }

    [TestMethod]
    public async Task SendAsync_SymbolNotFound_RecordsTheMissRatherThanAResolution()
    {
        var capture = new CapturingLoggerProvider();

        var session = NewSession(
            capture,
            new StubSymbolLookupProvider(new Dictionary<string, SymbolLookupResult>()),
            FakeChatClient.ToolCall("lookup_symbol", new Dictionary<string, object?> { ["symbolName"] = "Nope" }),
            FakeChatClient.Text("Not available here."));

        await session.SendAsync("Tell me about Nope", SampleScript);

        var names = capture.Entries.Select(e => e.EventId.Name).ToList();
        names.Should().Contain("SymbolLookupNotFound");
        names.Should().NotContain("SymbolLookupResolved");
    }

    [TestMethod]
    public async Task SendAsync_ModelProposesAnEdit_RecordsTheProposalLengthWithoutTheScriptAboveTrace()
    {
        var capture = new CapturingLoggerProvider(LogLevel.Debug);

        var session = NewSession(
            capture,
            FakeChatClient.ToolCall("propose_script_edit", new Dictionary<string, object?>
            {
                ["newScript"] = "var replaced = 2;",
                ["summary"] = "Replace the variable",
            }),
            FakeChatClient.Text("Done."));

        await session.SendAsync("Change it", SampleScript);

        var proposed = capture.Entries.Single(e => e.EventId.Name == "EditProposed");
        proposed.Message.Should().Contain("ScriptLength=17");
        capture.Entries.Should().NotContain(e => e.Message.Contains("var replaced = 2;"));
    }

    [TestMethod]
    public async Task SendAsync_Fails_LogsTheExceptionAtError()
    {
        var capture = new CapturingLoggerProvider();
        var session = NewSession(capture);  // No scripted responses, so the client throws.

        var act = async () => await session.SendAsync(SampleQuestion, SampleScript);

        await act.Should().ThrowAsync<InvalidOperationException>();

        var failure = capture.Entries.Single(e => e.EventId.Name == "TurnFailed");
        failure.Level.Should().Be(LogLevel.Error);
        failure.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [TestMethod]
    public async Task SendAsync_AtInformation_RecordsNoScriptOrPromptOrResponseContent()
    {
        // The shipping configuration (D16): everything above Trace must be structure only, so a
        // host that never enables Trace records no user or model content by construction.
        var capture = new CapturingLoggerProvider(LogLevel.Information);
        var session = NewSession(capture, FakeChatClient.Text("It sets pumpernickel to one."));

        await session.SendAsync(SampleQuestion, SampleScript);

        capture.Entries.Should().NotBeEmpty();
        capture.Entries.Should().NotContain(e => e.Message.Contains("secretScriptContents"));
        capture.Entries.Should().NotContain(e => e.Message.Contains("pumpernickel"));
        capture.Entries.Should().NotContain(e => e.Message.Contains("embedded in a script editor"));
    }

    [TestMethod]
    public async Task SendAsync_AtTrace_RecordsPromptAndResponseContentForDiagnosis()
    {
        var capture = new CapturingLoggerProvider(LogLevel.Trace);
        var session = NewSession(capture, FakeChatClient.Text("It sets pumpernickel to one."));

        await session.SendAsync(SampleQuestion, SampleScript);

        capture.Entries.Should().Contain(e => e.Message.Contains("secretScriptContents"));
        capture.Entries.Should().Contain(e => e.Message.Contains("pumpernickel"));
    }

    [TestMethod]
    public async Task SendAsync_WithNoLoggerFactory_StillWorks()
    {
        // The default path, and the one every existing caller takes.
        var session = new ScriptChatSession(new FakeChatClient(FakeChatClient.Text("Fine.")));

        var result = await session.SendAsync(SampleQuestion, SampleScript);

        result.Text.Should().Be("Fine.");
    }

    [TestMethod]
    public void Reset_WithALogger_RecordsWhatWasDiscarded()
    {
        var capture = new CapturingLoggerProvider();
        var session = NewSession(capture);

        session.Reset();

        capture.Entries.Should().Contain(e => e.EventId.Name == "SessionReset");
    }

    private static ScriptChatSession NewSession(CapturingLoggerProvider capture, params ChatResponse[] responses)
    {
        return NewSession(capture, NullSymbolLookupProvider.Instance, responses);
    }

    private static ScriptChatSession NewSession(
        CapturingLoggerProvider capture,
        ISymbolLookupProvider symbolLookup,
        params ChatResponse[] responses)
    {
        return new ScriptChatSession(
            new FakeChatClient(responses),
            new ScriptChatSessionOptions
            {
                SymbolLookup = symbolLookup,
                LoggerFactory = capture,
            });
    }
}
