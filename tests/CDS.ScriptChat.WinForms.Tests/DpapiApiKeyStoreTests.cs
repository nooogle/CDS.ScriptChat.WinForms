using System.Text;

using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

[TestClass]
[TestCategory("KeyStore")]
public sealed class DpapiApiKeyStoreTests
{
    private const string SampleKey = "sk-ant-not-a-real-key-0123456789";

    private string _directory = string.Empty;
    private DpapiApiKeyStore _store = null!;

    [TestInitialize]
    public void CreateTempDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "scriptchat-keys-" + Guid.NewGuid().ToString("N"));
        _store = DpapiApiKeyStore.ForDirectory(_directory);
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
    public void Load_NothingStored_ReturnsNull()
    {
        _store.Load(ScriptChatProvider.Claude).Should().BeNull();
    }

    [TestMethod]
    public void SaveThenLoad_SameProvider_RoundTripsTheKey()
    {
        _store.Save(ScriptChatProvider.Claude, SampleKey);

        _store.Load(ScriptChatProvider.Claude).Should().Be(SampleKey);
    }

    [TestMethod]
    public void Save_Always_WritesNoPlaintextKeyToDisk()
    {
        _store.Save(ScriptChatProvider.Claude, SampleKey);

        var keyBytes = Encoding.UTF8.GetBytes(SampleKey);
        foreach (var file in Directory.GetFiles(_directory))
        {
            var contents = File.ReadAllBytes(file);
            IndexOfSequence(contents, keyBytes).Should().Be(-1, "the key must never hit disk in the clear");
        }
    }

    [TestMethod]
    public void Save_CalledTwice_KeepsOnlyTheLatestKey()
    {
        _store.Save(ScriptChatProvider.Claude, SampleKey);
        _store.Save(ScriptChatProvider.Claude, "sk-ant-a-different-key");

        _store.Load(ScriptChatProvider.Claude).Should().Be("sk-ant-a-different-key");
    }

    [TestMethod]
    public void Save_DifferentProviders_KeepsThemSeparate()
    {
        _store.Save(ScriptChatProvider.Claude, "claude-key");
        _store.Save(ScriptChatProvider.OpenAI, "openai-key");

        _store.Load(ScriptChatProvider.Claude).Should().Be("claude-key");
        _store.Load(ScriptChatProvider.OpenAI).Should().Be("openai-key");
    }

    [TestMethod]
    public void Clear_AfterSave_RemovesTheKey()
    {
        _store.Save(ScriptChatProvider.Claude, SampleKey);

        _store.Clear(ScriptChatProvider.Claude);

        _store.Load(ScriptChatProvider.Claude).Should().BeNull();
    }

    [TestMethod]
    public void Clear_NothingStored_DoesNotThrow()
    {
        var act = () => _store.Clear(ScriptChatProvider.Claude);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Load_CorruptFile_ReportsNoKeyRatherThanThrowing()
    {
        _store.Save(ScriptChatProvider.Claude, SampleKey);
        var keyFile = Directory.GetFiles(_directory).Single();
        File.WriteAllBytes(keyFile, [1, 2, 3, 4, 5]);

        // A blob written by another Windows user, or restored from another machine, lands here
        // too — the panel should prompt for a key rather than fail to open.
        _store.Load(ScriptChatProvider.Claude).Should().BeNull();
    }

    [TestMethod]
    public void Save_BlankKey_Throws()
    {
        var act = () => _store.Save(ScriptChatProvider.Claude, "   ");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void ForApplication_BlankName_Throws()
    {
        var act = () => DpapiApiKeyStore.ForApplication("  ");

        act.Should().Throw<ArgumentException>();
    }

    private static int IndexOfSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }
}
