using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Covers the per-script orientation file convention — the gap that made the first real adopter
/// hand-roll its own file loading rather than use <see cref="HostOrientationResolver"/>.
/// </summary>
[TestClass]
[TestCategory("Orientation")]
public sealed class PerScriptOrientationTests
{
    private string _directory = string.Empty;

    [TestInitialize]
    public void CreateTempDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "scriptchat-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveTempDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void FileNameFor_LowercasesAndStripsSpaces() =>
        HostOrientationResolver.FileNameFor("Image Processing")
            .Should().Be("scriptchat.imageprocessing.context.md");

    [TestMethod]
    public void ResolveForScript_PrefersTheScriptsOwnFile()
    {
        Write("scriptchat.context.md", "shared prose");
        Write("scriptchat.processing.context.md", "processing prose");

        HostOrientationResolver.ResolveForScript("Processing", _directory, logger: null)
            .Should().Be("processing prose");
    }

    [TestMethod]
    public void ResolveForScript_FallsBackToTheSharedFile()
    {
        // A host whose scripts share one description writes one file, and only splits it when a
        // script actually needs its own.
        Write("scriptchat.context.md", "shared prose");

        HostOrientationResolver.ResolveForScript("Processing", _directory, logger: null)
            .Should().Be("shared prose");
    }

    [TestMethod]
    public void ResolveForScript_WithNeitherFile_ReturnsNull() =>
        HostOrientationResolver.ResolveForScript("Processing", _directory, logger: null)
            .Should().BeNull();

    [TestMethod]
    public void ResolveForScript_WithABlankPerScriptFile_FallsBackRatherThanReturningNothing()
    {
        // Touching a placeholder into existence must not silently blank out the shared prose.
        Write("scriptchat.processing.context.md", "   \n  ");
        Write("scriptchat.context.md", "shared prose");

        HostOrientationResolver.ResolveForScript("Processing", _directory, logger: null)
            .Should().Be("shared prose");
    }

    [TestMethod]
    public void ResolveForScript_WithABlankName_Throws()
    {
        var act = () => HostOrientationResolver.ResolveForScript("  ", _directory, logger: null);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Resolve_StillReadsTheSharedFile()
    {
        Write("scriptchat.context.md", "shared prose");

        HostOrientationResolver.Resolve(hostContext: null, _directory, logger: null)
            .Should().Be("shared prose");
    }

    private void Write(string fileName, string contents) =>
        File.WriteAllText(Path.Combine(_directory, fileName), contents);
}
