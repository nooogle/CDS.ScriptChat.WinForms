using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.WinForms.Tests;

[TestClass]
public static class TestAssemblySetup
{
    /// <summary>
    /// A settings panel fetches the live model list as soon as it has a key, so without this every
    /// test that loads a stored key would call a real provider with a fake one.
    /// </summary>
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        ScriptChatSettingsPanel.DefaultModelListSource =
            (_, _, _) => Task.FromResult<IReadOnlyList<ScriptChatModelInfo>>([]);
    }
}
