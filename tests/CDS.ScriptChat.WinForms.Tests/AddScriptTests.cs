using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.Core.Tests;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers <see cref="ScriptChatHostPanel.AddScript(string, Func{string}, Action{string}, Type, Type[])"/>
/// — the easy path, where one API type drives both the orientation index and <c>lookup_symbol</c>.
/// </summary>
[TestClass]
[TestCategory("AddScript")]
public sealed class AddScriptTests
{
    private static ScriptChatClientOptions ClaudeOptions => new()
    {
        Provider = ScriptChatProvider.Claude,
        ApiKey = "sk-ant-not-a-real-key",
        ModelId = ScriptChatModels.ClaudeDefault,
    };

    [TestMethod]
    public void AddScript_AddsATargetToTheSelector()
    {
        using var panel = new ScriptChatHostPanel();

        panel.AddScript("Processing", () => "var x = 1;", _ => { }, typeof(SampleGlobals));

        FindSelector(panel).Items.Cast<string>().Should().BeEquivalentTo(["Processing"]);
    }

    [TestMethod]
    public void AddScript_CalledTwice_Accumulates()
    {
        using var panel = new ScriptChatHostPanel();

        panel.AddScript("Workspace", () => "a", _ => { }, typeof(SampleGlobals));
        panel.AddScript("Processing", () => "b", _ => { }, typeof(SampleGlobals));

        var selector = FindSelector(panel);
        selector.Items.Cast<string>().Should().BeEquivalentTo(["Workspace", "Processing"]);
        selector.SelectedIndex.Should().Be(0, "adding a second script must not move the user off the first");
    }

    [TestMethod]
    public void AddScript_AfterConfigure_GivesTheNewScriptAConversationImmediately()
    {
        using var panel = new ScriptChatHostPanel();
        panel.Configure(ClaudeOptions);

        panel.AddScript("Processing", () => "var x = 1;", _ => { }, typeof(SampleGlobals));

        // Order must not matter: a host is free to configure first and add scripts afterwards.
        FindStatus(panel).Text.Should().Be("Ready.");
    }

    [TestMethod]
    public void SetTargets_AfterConfigure_AlsoGivesEveryTargetAConversation()
    {
        using var panel = new ScriptChatHostPanel();
        panel.Configure(ClaudeOptions);

        panel.SetTargets(new ScriptChatTarget
        {
            DisplayName = "Script",
            ScriptTextProvider = () => "var x = 1;",
            ScriptTextSetter = _ => { },
            CreateSessionOptions = () => new ScriptChatSessionOptions(),
        });

        FindStatus(panel).Text.Should().Be("Ready.");
    }

    [TestMethod]
    public void AddScript_WithASessionOptionsFactory_UsesItPerConversation()
    {
        using var panel = new ScriptChatHostPanel();
        var calls = 0;

        panel.AddScript("Script", () => "a", _ => { }, () =>
        {
            calls++;
            return new ScriptChatSessionOptions();
        });

        panel.Configure(ClaudeOptions);
        panel.RestartConversations();

        // Once when the script was added, once for Configure, once for the restart — the point
        // is that it is asked again rather than reusing a value captured at wiring time.
        calls.Should().BeGreaterThan(1);
    }

    [TestMethod]
    public async Task Quickstart_TwoCalls_ProducesAWorkingPanel()
    {
        // The acceptance criterion for Job 5, kept as a test so it cannot quietly stop being
        // true: an app with a script editor and an API type needs exactly these two calls.
        var script = "var x = 1;";
        using var panel = new ScriptChatHostPanel();

        panel.AddScript(
            name: "Processing",
            read: () => script,
            write: text => script = text,
            api: typeof(SampleGlobals));

        panel.UseStoredKey(
            new FakeApiKeyStore { [ScriptChatProvider.Claude] = "sk-ant-not-a-real-key" },
            () => null,
            _ => { });

        FindStatus(panel).Text.Should().Be("Ready.");

        // …and the assistant can actually answer from the host's API, which is the whole point.
        var options = ScriptChatSessionOptions.ForHostApi(typeof(SampleGlobals));
        var lookup = await options.SymbolLookup.LookupAsync("CreatePanel", "SampleApi");
        lookup.Should().NotBeNull();
    }

    [TestMethod]
    public void AddScript_InvalidArguments_Throw()
    {
        using var panel = new ScriptChatHostPanel();

        ((Action)(() => panel.AddScript("  ", () => "a", _ => { }, typeof(SampleGlobals))))
            .Should().Throw<ArgumentException>();
        ((Action)(() => panel.AddScript("S", null!, _ => { }, typeof(SampleGlobals))))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => panel.AddScript("S", () => "a", null!, typeof(SampleGlobals))))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => panel.AddScript("S", () => "a", _ => { }, (Type)null!)))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => panel.AddScript("S", () => "a", _ => { }, (Func<ScriptChatSessionOptions>)null!)))
            .Should().Throw<ArgumentNullException>();
    }

    private static ComboBox FindSelector(Control root) =>
        root.Controls.Find("_targetSelector", searchAllChildren: true).OfType<ComboBox>().Single();

    private static Label FindStatus(Control root) =>
        root.Controls.Find("_statusLabel", searchAllChildren: true).OfType<Label>().Single();
}
