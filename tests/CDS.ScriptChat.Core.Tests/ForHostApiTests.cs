using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Covers <see cref="ScriptChatSessionOptions.ForHostApi(Type, Type[])"/> — the batteries-included
/// path, where one API type drives both the orientation index and <c>lookup_symbol</c>.
/// </summary>
/// <remarks>
/// This is what makes the two-call quickstart possible: everything an adopter used to hand-write
/// (a metadata compilation, a resolver, an <see cref="ISymbolLookupProvider"/> adapter, and an
/// orientation blurb) now falls out of naming one type.
/// </remarks>
[TestClass]
[TestCategory("Roslyn")]
public sealed class ForHostApiTests
{
    [TestMethod]
    public async Task ForHostApi_WiresUpSymbolLookupFromTheApiType()
    {
        var options = ScriptChatSessionOptions.ForHostApi(typeof(SampleGlobals), typeof(SamplePanel));

        var result = await options.SymbolLookup.LookupAsync("CreatePanel", "SampleApi");

        result.Should().NotBeNull("the whole point of naming an API type is that lookup_symbol works");
        result!.Signature.Should().Contain("SampleApi.CreatePanel(string name)");
    }

    [TestMethod]
    public async Task ForHostApi_ResolvesABareTypeNameFromTheApiTypesOwnNamespace()
    {
        // A host that never said which namespaces its scripts import still gets bare names
        // resolving, because its own API's namespace is the obvious default.
        var options = ScriptChatSessionOptions.ForHostApi(typeof(SampleGlobals), typeof(SamplePanel));

        var result = await options.SymbolLookup.LookupAsync("SamplePanel", containingType: null);

        result.Should().NotBeNull();
        result!.Namespace.Should().Be("CDS.ScriptChat.Core.Tests");
    }

    [TestMethod]
    public async Task ForHostApi_CarriesXmlDocumentationThrough()
    {
        var options = ScriptChatSessionOptions.ForHostApi(typeof(SampleGlobals), typeof(SamplePanel));

        var result = await options.SymbolLookup.LookupAsync("SetValue", containingType: null);

        result!.XmlDocSummary.Should().Be("Sets the displayed value.");
    }

    [TestMethod]
    public void ForHostApi_BuildsAnOrientationBlurbFromTheApiType()
    {
        var options = ScriptChatSessionOptions.ForHostApi(typeof(SampleGlobals), typeof(SamplePanel));

        options.OrientationBlurb.Should().NotBeNullOrWhiteSpace();
        options.OrientationBlurb.Should().Contain("- `API`: CreatePanel, Log");
        options.OrientationBlurb.Should().Contain("- `SamplePanel.API`: SetValue");
    }

    [TestMethod]
    public void ForHostApi_TellsTheModelTheIndexIsNamesOnly()
    {
        var options = ScriptChatSessionOptions.ForHostApi(typeof(SampleGlobals));

        // Without this the model treats the index as the whole API surface and stops asking.
        options.OrientationBlurb.Should().Contain("lookup_symbol");
    }

    [TestMethod]
    public void ForHostApi_TurnsLookupSymbolBackOn()
    {
        var options = ScriptChatSessionOptions.ForHostApi(typeof(SampleGlobals));

        // D20: a tool is only advertised when something can answer it, so the easy path must
        // never leave NullSymbolLookupProvider in place.
        options.SymbolLookup.Should().BeOfType<RoslynSymbolLookupProvider>();
    }

    [TestMethod]
    public async Task ForHostApi_ReachesTheSessionsLookupSymbolTool()
    {
        var client = new FakeChatClient(
            FakeChatClient.ToolCall("lookup_symbol", new Dictionary<string, object?>
            {
                ["symbolName"] = "CreatePanel",
                ["containingType"] = "SampleApi",
            }),
            FakeChatClient.Text("It takes a name."));

        var session = new ScriptChatSession(
            client,
            ScriptChatSessionOptions.ForHostApi(typeof(SampleGlobals), typeof(SamplePanel)));

        var result = await session.SendAsync("What does CreatePanel take?", "var x = 1;");

        client.LastOptions!.Tools!.Select(tool => tool.Name).Should().Contain("lookup_symbol");
        result.SymbolsLookedUp.Should().ContainSingle().Which.Should().Be("SampleApi.CreatePanel");
    }

    [TestMethod]
    public void ForHostApi_WithANullApiType_Throws()
    {
        var act = () => ScriptChatSessionOptions.ForHostApi(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
