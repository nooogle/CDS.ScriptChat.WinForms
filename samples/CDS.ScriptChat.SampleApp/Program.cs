namespace CDS.ScriptChat.SampleApp;

/// <summary>Entry point for the sample app.</summary>
internal static class Program
{
    /// <summary>Runs the sample.</summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
