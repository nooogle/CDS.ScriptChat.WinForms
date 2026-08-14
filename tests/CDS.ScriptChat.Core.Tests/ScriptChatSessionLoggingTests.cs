using AwesomeAssertions;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Covers what the session records, and — including when the underlying logging provider is
/// configured to allow it — what it must never record (D16, D17).
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
    public async Task SendAsync_ModelProposesAnEdit_RecordsTheProposalLengthWithoutTheScriptAtAnyLevel()
    {
        // Captures everything, including Trace — there is no level at which the proposed script
        // should appear (D17), not just "above Trace" the way earlier versions of this library
        // worked.
        var capture = new CapturingLoggerProvider(LogLevel.Trace);

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
        capture.Entries.Should().NotContain(e => e.Message.Contains("Replace the variable"));
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
        // The shipping configuration (D16, D17): everything at Information carries structure
        // only, so a host running at its default records no user or model content.
        var capture = new CapturingLoggerProvider(LogLevel.Information);
        var session = NewSession(capture, FakeChatClient.Text("It sets pumpernickel to one."));

        await session.SendAsync(SampleQuestion, SampleScript);

        capture.Entries.Should().NotBeEmpty();
        capture.Entries.Should().NotContain(e => e.Message.Contains("secretScriptContents"));
        capture.Entries.Should().NotContain(e => e.Message.Contains("pumpernickel"));
        capture.Entries.Should().NotContain(e => e.Message.Contains("embedded in a script editor"));
    }

    [TestMethod]
    public async Task SendAsync_EvenWhenTheUnderlyingProviderAllowsTrace_RecordsNoContent()
    {
        // D17: unlike D16 alone, this isn't "a host that doesn't enable Trace is safe" — it's
        // "Trace cannot carry content even if something does enable it." CapturingLoggerProvider
        // configured for Trace stands in for exactly that: a provider willing to accept anything,
        // as if a bad actor (or a misconfigured host, or another library sharing the same
        // pipeline) had cranked the minimum level up. ScriptChatSession must still record nothing
        // sensitive, because TraceSuppressingLoggerFactory refuses to ever report Trace as
        // enabled to anything it hands a logger to — this library's own code and
        // Microsoft.Extensions.AI's function-invocation/chat-client logging alike.
        var capture = new CapturingLoggerProvider(LogLevel.Trace);
        var session = NewSession(capture, FakeChatClient.Text("It sets pumpernickel to one."));

        await session.SendAsync(SampleQuestion, SampleScript);

        capture.Entries.Should().NotContain(e => e.Level == LogLevel.Trace);
        capture.Entries.Should().NotContain(e => e.Message.Contains("secretScriptContents"));
        capture.Entries.Should().NotContain(e => e.Message.Contains("pumpernickel"));
        capture.Entries.Should().NotContain(e => e.Message.Contains("embedded in a script editor"));
    }

    [TestMethod]
    public async Task SendAsync_ProposesAnEditWithTraceAllowed_DoesNotLeakViaFunctionInvocationLogging()
    {
        // The specific risk D17 was written for: Microsoft.Extensions.AI's own
        // FunctionInvokingChatClient logs full function arguments and results at Trace
        // ("Invoking {MethodName}({Arguments})") — for propose_script_edit, that is the entire
        // proposed script — independent of anything ScriptChatLog defines. That logging is
        // internal to Microsoft.Extensions.AI, so this can only be proven from the outside: run a
        // proposal through with the underlying provider willing to accept Trace, across every
        // category, and confirm the script and summary never appear anywhere.
        var capture = new CapturingLoggerProvider(LogLevel.Trace);

        var session = NewSession(
            capture,
            FakeChatClient.ToolCall("propose_script_edit", new Dictionary<string, object?>
            {
                ["newScript"] = "var replaced = 2;",
                ["summary"] = "Replace the variable",
            }),
            FakeChatClient.Text("Done."));

        await session.SendAsync("Change it", SampleScript);

        capture.Entries.Should().NotContain(e => e.Level == LogLevel.Trace);
        capture.Entries.Should().NotContain(e => e.Message.Contains("var replaced = 2;"));
        capture.Entries.Should().NotContain(e => e.Message.Contains("Replace the variable"));
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
