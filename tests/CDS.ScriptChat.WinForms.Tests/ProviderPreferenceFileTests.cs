using AwesomeAssertions;

using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers the default place <see cref="ScriptChatHostPanel.UseStoredKey(string)"/> remembers the
/// chosen provider and model, for a host with no settings file of its own.
/// </summary>
/// <remarks>
/// Every failure path here must degrade to "no preference recorded" rather than throwing: falling
/// back to the default provider is always survivable, and refusing to open the chat panel because
/// a preference file is unreadable is not.
/// </remarks>
[TestClass]
[TestCategory("StoredKey")]
public sealed class ProviderPreferenceFileTests
{
    private string _directory = string.Empty;

    private string Path => System.IO.Path.Combine(_directory, "provider.txt");

    [TestInitialize]
    public void CreateTempDirectory()
    {
        _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "scriptchat-tests",
            Guid.NewGuid().ToString("N"));

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
    public void SaveThenLoad_RoundTripsProviderAndModel()
    {
        var file = new ProviderPreferenceFile(Path);

        file.Save(new ScriptChatProviderPreference(ScriptChatProvider.OpenAI, "gpt-5"));

        file.Load().Should().Be(new ScriptChatProviderPreference(ScriptChatProvider.OpenAI, "gpt-5"));
    }

    [TestMethod]
    public void SaveThenLoad_WithNoModel_RoundTripsAsNull()
    {
        var file = new ProviderPreferenceFile(Path);

        file.Save(new ScriptChatProviderPreference(ScriptChatProvider.Claude, null));

        file.Load()!.ModelId.Should().BeNull();
    }

    [TestMethod]
    public void Save_CreatesTheDirectoryWhenItIsNotThereYet()
    {
        var nested = System.IO.Path.Combine(_directory, "a", "b", "provider.txt");
        var file = new ProviderPreferenceFile(nested);

        file.Save(new ScriptChatProviderPreference(ScriptChatProvider.Claude, "claude-opus-5"));

        file.Load()!.Provider.Should().Be(ScriptChatProvider.Claude);
    }

    [TestMethod]
    public void Load_WithNoFile_ReturnsNull() =>
        new ProviderPreferenceFile(Path).Load().Should().BeNull();

    [TestMethod]
    public void Load_WithAnUnrecognisedProvider_ReturnsNullRatherThanThrowing()
    {
        // A file written by a later build that knows a provider this one does not must not stop
        // the app configuring itself.
        File.WriteAllLines(Path, ["provider=SomeFutureProvider", "model=whatever"]);

        new ProviderPreferenceFile(Path).Load().Should().BeNull();
    }

    [TestMethod]
    public void Load_WithGarbage_ReturnsNull()
    {
        File.WriteAllText(Path, "this is not a preference file\n\n===\n");

        new ProviderPreferenceFile(Path).Load().Should().BeNull();
    }

    [TestMethod]
    public void Save_WhenThePathIsBlockedByADirectory_DoesNotThrow()
    {
        // The chat still works this session; the user just gets the default provider next time.
        Directory.CreateDirectory(Path);
        var file = new ProviderPreferenceFile(Path);

        var act = () => file.Save(new ScriptChatProviderPreference(ScriptChatProvider.Claude, null));

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Load_WhenThePathIsBlockedByADirectory_ReturnsNull()
    {
        Directory.CreateDirectory(Path);

        new ProviderPreferenceFile(Path).Load().Should().BeNull();
    }

    [TestMethod]
    public void ForApplication_WithABlankName_Throws()
    {
        var act = () => ProviderPreferenceFile.ForApplication("   ");

        act.Should().Throw<ArgumentException>();
    }
}
