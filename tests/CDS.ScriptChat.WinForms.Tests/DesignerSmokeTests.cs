using AwesomeAssertions;

using CDS.Markdown;
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
        FindTranscript(panel).TextLength.Should().Be(0);
    }

    [TestMethod]
    public void ClearTranscript_AfterAttach_LeavesTheTranscriptEmpty()
    {
        using var panel = new ScriptChatPanel();

        panel.ClearTranscript();

        FindTranscript(panel).TextLength.Should().Be(0);
    }

    [TestMethod]
    public void ScriptChatSettingsForm_Constructed_InitialisesWithoutThrowing()
    {
        using var form = new ScriptChatSettingsForm();

        form.Controls.Count.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void ScriptChatSettingsForm_KeyStore_ForwardsToTheInnerPanel()
    {
        using var form = new ScriptChatSettingsForm();
        var store = new StubApiKeyStore();

        form.KeyStore = store;

        form.KeyStore.Should().BeSameAs(store);
    }

    private static MarkdownTextBox FindTranscript(ScriptChatPanel panel) =>
        panel.Controls.Find("_transcriptTextBox", searchAllChildren: true).OfType<MarkdownTextBox>().Single();

    private sealed class StubApiKeyStore : IApiKeyStore
    {
        public string? Load(ScriptChatProvider provider) => null;

        public void Save(ScriptChatProvider provider, string apiKey)
        {
        }

        public void Clear(ScriptChatProvider provider)
        {
        }
    }
}
