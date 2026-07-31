using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

[TestClass]
[TestCategory("Session")]
public sealed class ScriptChatSessionBaselineTests
{
    [TestMethod]
    public async Task GetScriptBaseline_AfterATurn_ReturnsTheScriptThatWasSent()
    {
        var client = new FakeChatClient(FakeChatClient.Text("Noted."));
        var session = new ScriptChatSession(client);

        await session.SendAsync("Explain this", "var answer = 42;");

        for (var i = 0; i < session.Turns.Count; i++)
        {
            session.GetScriptBaseline(i).Should().Be("var answer = 42;");
        }
    }

    [TestMethod]
    public async Task GetScriptBaseline_AcrossTurnsWithADifferentScript_TracksEachSeparately()
    {
        var client = new FakeChatClient(
            FakeChatClient.Text("First."),
            FakeChatClient.Text("Second."));
        var session = new ScriptChatSession(client);

        await session.SendAsync("First question", "version one");
        await session.SendAsync("Second question", "version two");

        session.GetScriptBaseline(0).Should().Be("version one");
        session.GetScriptBaseline(session.Turns.Count - 1).Should().Be("version two");
    }

    [TestMethod]
    public async Task GetScriptBaseline_StaysAlignedWithTurns()
    {
        var client = new FakeChatClient(
            FakeChatClient.ToolCall("propose_script_edit", new Dictionary<string, object?>
            {
                ["newScript"] = "var x = 2;",
                ["summary"] = "Bump x",
            }),
            FakeChatClient.Text("Done."));
        var session = new ScriptChatSession(client);

        await session.SendAsync("Set x to 2", "var x = 1;");

        // Every turn must have a baseline; an off-by-one here would silently diff a proposal
        // against the wrong script.
        var act = () => session.GetScriptBaseline(session.Turns.Count - 1);
        act.Should().NotThrow();
    }

    [TestMethod]
    public async Task GetScriptBaseline_AfterReset_IsOutOfRangeAgain()
    {
        var client = new FakeChatClient(FakeChatClient.Text("Noted."));
        var session = new ScriptChatSession(client);
        await session.SendAsync("A question", "var x = 1;");

        session.Reset();

        var act = () => session.GetScriptBaseline(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void GetScriptBaseline_NegativeIndex_Throws()
    {
        var session = new ScriptChatSession(new FakeChatClient());

        var act = () => session.GetScriptBaseline(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
