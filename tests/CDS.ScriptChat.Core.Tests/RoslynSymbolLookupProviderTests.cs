using AwesomeAssertions;

using Microsoft.CodeAnalysis;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Covers <see cref="RoslynSymbolLookupProvider"/> — the adapter every consumer used to
/// hand-write, twice (D22).
/// </summary>
[TestClass]
[TestCategory("Roslyn")]
public sealed class RoslynSymbolLookupProviderTests
{
    private static RoslynSymbolResolver Resolver =>
        new(["CDS.ScriptChat.Core.Tests"], typeof(SampleGlobals), typeof(SamplePanel));

    [TestMethod]
    public async Task LookupAsync_OverAFixedCompilation_ResolvesAHostSymbol()
    {
        var provider = new RoslynSymbolLookupProvider(
            MetadataCompilation.FromTypes(typeof(SampleGlobals)),
            Resolver);

        var result = await provider.LookupAsync("CreatePanel", "SampleApi");

        result.Should().NotBeNull();
        result!.Signature.Should().Contain("SampleApi.CreatePanel(string name)");
    }

    [TestMethod]
    public async Task LookupAsync_OverALiveCompilationSource_ReReadsItEachTime()
    {
        var calls = 0;
        var compilation = MetadataCompilation.FromTypes(typeof(SampleGlobals));

        var provider = new RoslynSymbolLookupProvider(
            _ =>
            {
                calls++;
                return Task.FromResult<Compilation?>(compilation);
            },
            Resolver);

        await provider.LookupAsync("CreatePanel", "SampleApi");
        await provider.LookupAsync("Log", "SampleApi");

        // A live editor's buffer changes between turns, so the source must be asked every time
        // rather than cached at construction.
        calls.Should().Be(2);
    }

    [TestMethod]
    public async Task LookupAsync_WhenTheHostHasNoCompilation_ReportsAMissRatherThanThrowing()
    {
        var provider = new RoslynSymbolLookupProvider(_ => Task.FromResult<Compilation?>(null), Resolver);

        var result = await provider.LookupAsync("CreatePanel", "SampleApi");

        result.Should().BeNull("a script that does not currently parse is an ordinary state, not a fault");
    }

    [TestMethod]
    public async Task LookupAsync_RaisesSymbolLookedUpWithTheOutcome()
    {
        var provider = new RoslynSymbolLookupProvider(
            MetadataCompilation.FromTypes(typeof(SampleGlobals)),
            Resolver);

        var raised = new List<SymbolLookedUpEventArgs>();
        provider.SymbolLookedUp += (_, e) => raised.Add(e);

        await provider.LookupAsync("CreatePanel", "SampleApi");
        await provider.LookupAsync("NoSuchThing", null);

        raised.Should().HaveCount(2);
        raised[0].Found.Should().BeTrue();
        raised[0].ToString().Should().Be("lookup_symbol: SampleApi.CreatePanel — found");
        raised[1].Found.Should().BeFalse();
        raised[1].ToString().Should().Be("lookup_symbol: NoSuchThing — not found");
    }

    [TestMethod]
    public async Task LookupAsync_ResultReachesTheSessionsLookupSymbolTool()
    {
        // End to end: wiring the shipped provider into a session is all an adopter should have
        // to do, and it must also flip lookup_symbol back on (D20).
        var client = new FakeChatClient(
            FakeChatClient.ToolCall("lookup_symbol", new Dictionary<string, object?>
            {
                ["symbolName"] = "CreatePanel",
                ["containingType"] = "SampleApi",
            }),
            FakeChatClient.Text("It takes a name."));

        var session = new ScriptChatSession(client, new ScriptChatSessionOptions
        {
            SymbolLookup = new RoslynSymbolLookupProvider(
                MetadataCompilation.FromTypes(typeof(SampleGlobals)),
                Resolver),
        });

        var result = await session.SendAsync("What does CreatePanel take?", "var x = 1;");

        client.LastOptions!.Tools!.Select(tool => tool.Name).Should().Contain("lookup_symbol");
        result.SymbolsLookedUp.Should().ContainSingle().Which.Should().Be("SampleApi.CreatePanel");
    }

    [TestMethod]
    public void Constructor_WithANullResolver_Throws()
    {
        var act = () => new RoslynSymbolLookupProvider(
            MetadataCompilation.FromTypes(typeof(SampleGlobals)),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
