using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.TestHost.Demo;

/// <summary>
/// Seeds the chat panel with one canned turn carrying Markdown prose and a table, without a real
/// provider key — the fixture <c>--demo=markdown</c> launches for UI-automation coverage of
/// <see cref="MarkdownTextBox"/> against a real, painted window.
/// </summary>
internal static class MarkdownDemo
{
    /// <summary>The reply an assistant turn carries once seeded — read back by UI tests.</summary>
    public const string Reply = """
        For faint, thin, *straight* lines you generally need two stages: enhance the evidence, then fit lines.

        **Detectors**

        | API | Notes |
        |---|---|
        | `Cv2.HoughLines(img, rho, theta, threshold, srn, stn)` | Accumulates along the whole line — the most sensitive option for long faint lines. |
        | `Cv2.HoughLinesP(img, rho, theta, threshold, minLineLength, maxLineGap)` | Gives endpoints; `maxLineGap` bridges dropouts in a broken faint line. |
        """;

    /// <summary>Attaches a fresh session to <paramref name="panel"/> with one seeded turn.</summary>
    /// <param name="panel">The chat panel to seed.</param>
    /// <param name="script">The script text the seeded turn is sent against.</param>
    public static async Task SeedAsync(ScriptChatPanel panel, string script)
    {
        var session = new ScriptChatSession(new EchoChatClient(Reply));
        await session.SendAsync("Show me a table of line detectors.", script);
        panel.AttachSession(session);
    }
}
