using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.Core.Tests;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// A failed turn must not cost the user their prompt: the input box is cleared before the call,
/// so the failure path has to put it back.
/// </summary>
[TestClass]
[TestCategory("FailedTurn")]
public sealed class FailedTurnTests
{
    private const string Prompt = "Set x to 2";

    [TestMethod]
    public async Task FailedTurn_WithAnEmptyInputBox_RestoresThePrompt()
    {
        using var panel = CreatePanelWhoseNextTurnFails();
        var input = FindInputBox(panel);
        input.Text = Prompt;

        await panel.SendCurrentInputAsync();

        input.Text.Should().Be(Prompt);
    }

    [TestMethod]
    public async Task FailedTurn_WithAnEmptyInputBox_PutsTheCaretAtTheEnd()
    {
        using var panel = CreatePanelWhoseNextTurnFails();
        var input = FindInputBox(panel);
        input.Text = Prompt;

        await panel.SendCurrentInputAsync();

        input.SelectionStart.Should().Be(Prompt.Length);
    }

    [TestMethod]
    public async Task FailedTurn_AfterTheUserTypedSomethingElse_KeepsTheNewText()
    {
        const string typedDuringTheTurn = "never mind";
        var input = default(TextBoxBase);
        using var panel = CreatePanelWhoseNextTurnFails(() => input!.Text = typedDuringTheTurn);
        input = FindInputBox(panel);
        input.Text = Prompt;

        await panel.SendCurrentInputAsync();

        input.Text.Should().Be(typedDuringTheTurn);
    }

    private static ScriptChatPanel CreatePanelWhoseNextTurnFails() => CreatePanelWhoseNextTurnFails(() => { });

    private static ScriptChatPanel CreatePanelWhoseNextTurnFails(Action duringTheTurn)
    {
        // A FakeChatClient with no scripted responses throws on its first call.
        var session = new ScriptChatSession(new FakeChatClient());
        var panel = new ScriptChatPanel();
        panel.ScriptTextProvider = () =>
        {
            duringTheTurn();
            return "var x = 1;";
        };
        panel.AttachSession(session);
        return panel;
    }

    private static TextBoxBase FindInputBox(ScriptChatPanel panel) =>
        panel.Controls.Find("_inputTextBox", searchAllChildren: true).OfType<TextBoxBase>().Single();
}
