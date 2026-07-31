namespace CDS.ScriptChat.TestHost;

/// <summary>Entry point for the test host.</summary>
internal static class Program
{
    /// <summary>Runs the test host.</summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
