using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers <see cref="ScriptChatHostPanel"/>: the selector, and that every target gets its own
/// conversation while sharing one chat client.
/// </summary>
[TestClass]
[TestCategory("HostPanel")]
public sealed class HostPanelTests
{
    private static ScriptChatClientOptions ClaudeOptions => new()
    {
        Provider = ScriptChatProvider.Claude,
        ApiKey = "sk-ant-not-a-real-key",
        ModelId = ScriptChatModels.ClaudeDefault,
    };

    private static ScriptChatTarget MakeTarget(string displayName, Func<string>? scriptTextProvider = null) => new()
    {
        DisplayName = displayName,
        ScriptTextProvider = scriptTextProvider ?? (() => $"// {displayName}"),
        ScriptTextSetter = _ => { },
        CreateSessionOptions = () => new ScriptChatSessionOptions(),
    };

    [TestMethod]
    public void SetTargets_Null_Throws()
    {
        using var panel = new ScriptChatHostPanel();

        var act = () => panel.SetTargets(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void SetTargets_Empty_Throws()
    {
        using var panel = new ScriptChatHostPanel();

        var act = () => panel.SetTargets();

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void SetTargets_MoreThanTwoTargets_PopulatesTheSelectorAndSelectsTheFirst()
    {
        using var panel = new ScriptChatHostPanel();

        panel.SetTargets(MakeTarget("A"), MakeTarget("B"), MakeTarget("C"));

        var selector = FindSelector(panel);
        selector.Items.Count.Should().Be(3);
        selector.SelectedIndex.Should().Be(0);
        selector.Items[0].Should().Be("A");
        selector.Items[2].Should().Be("C");
    }

    [TestMethod]
    public void SwitchingSelection_RepointsTheInnerPanelToTheNewTarget()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget("A", () => "script a"), MakeTarget("B", () => "script b"));

        FindSelector(panel).SelectedIndex = 1;

        FindChatPanel(panel).ScriptTextProvider!().Should().Be("script b");
    }

    [TestMethod]
    public void Configure_GivesEveryTargetAReadySession()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget("A"), MakeTarget("B"));

        panel.Configure(ClaudeOptions);

        FindChatPanel(panel).IsReady.Should().BeTrue();

        FindSelector(panel).SelectedIndex = 1;
        FindChatPanel(panel).IsReady.Should().BeTrue();
    }

    [TestMethod]
    public void RestartConversations_BeforeConfigure_DoesNothing()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget("A"));

        var act = () => panel.RestartConversations();

        act.Should().NotThrow();
        FindChatPanel(panel).IsReady.Should().BeFalse();
    }

    [TestMethod]
    public void SetUnavailable_AppliesRegardlessOfWhichTargetIsSelected()
    {
        using var panel = new ScriptChatHostPanel();
        panel.SetTargets(MakeTarget("A"), MakeTarget("B"));
        panel.Configure(ClaudeOptions);

        panel.SetUnavailable("No API key configured.");

        FindChatPanel(panel).IsReady.Should().BeFalse();
        FindSelector(panel).SelectedIndex = 1;
        FindChatPanel(panel).IsReady.Should().BeFalse();
    }

    private static ComboBox FindSelector(ScriptChatHostPanel panel) =>
        panel.Controls.Find("_targetSelector", searchAllChildren: true).OfType<ComboBox>().Single();

    private static ScriptChatPanel FindChatPanel(ScriptChatHostPanel panel) =>
        panel.Controls.Find("_chatPanel", searchAllChildren: true).OfType<ScriptChatPanel>().Single();
}
