using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

[TestClass]
[TestCategory("HostContext")]
public sealed class HostOrientationResolverTests
{
    private string _directory = string.Empty;

    [TestInitialize]
    public void CreateTempDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "scriptchat-tests-" + Guid.NewGuid().ToString("N"));
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
    public void Resolve_FileAndPropertyBothPresent_PrefersTheFile()
    {
        WriteContextFile("Blurb from the file.");
        var host = new StubHostContext("Blurb from the property.");

        var result = HostOrientationResolver.Resolve(host, _directory);

        result.Should().Be("Blurb from the file.");
    }

    [TestMethod]
    public void Resolve_NoFile_FallsBackToTheProperty()
    {
        var host = new StubHostContext("Blurb from the property.");

        var result = HostOrientationResolver.Resolve(host, _directory);

        result.Should().Be("Blurb from the property.");
    }

    [TestMethod]
    public void Resolve_BlankFile_FallsBackToTheProperty()
    {
        WriteContextFile("   \n  ");
        var host = new StubHostContext("Blurb from the property.");

        var result = HostOrientationResolver.Resolve(host, _directory);

        result.Should().Be("Blurb from the property.");
    }

    [TestMethod]
    public void Resolve_FileWithSurroundingWhitespace_ReturnsItTrimmed()
    {
        WriteContextFile("\n  Blurb from the file.  \n");

        var result = HostOrientationResolver.Resolve(hostContext: null, _directory);

        result.Should().Be("Blurb from the file.");
    }

    [TestMethod]
    public void Resolve_NeitherSourceSupplied_ReturnsNull()
    {
        var result = HostOrientationResolver.Resolve(hostContext: null, _directory);

        result.Should().BeNull();
    }

    [TestMethod]
    public void Resolve_PropertyIsBlank_ReturnsNull()
    {
        var host = new StubHostContext("   ");

        var result = HostOrientationResolver.Resolve(host, _directory);

        result.Should().BeNull();
    }

    private void WriteContextFile(string contents)
    {
        File.WriteAllText(Path.Combine(_directory, HostOrientationResolver.ConventionalFileName), contents);
    }

    private sealed class StubHostContext : IScriptChatHostContext
    {
        public StubHostContext(string? orientationBlurb) => OrientationBlurb = orientationBlurb;

        public string? OrientationBlurb { get; }
    }
}
