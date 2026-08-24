using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace CDS.ScriptChat.SampleApp;

/// <summary>
/// An ordinary WinForms app that happens to have a C# script editor, showing what adopting
/// CDS.ScriptChat actually costs.
/// </summary>
/// <remarks>
/// <para>
/// The whole integration is <see cref="InitializeChat"/> — two calls. Everything else in this
/// file is the app's own business: running the script, and showing its output.
/// </para>
/// <para>
/// Bring your own key. Nothing ships with this sample; the chat panel stays switched off until
/// you enter one via its Settings button, and the key is stored encrypted under your Windows
/// account (D3).
/// </para>
/// </remarks>
public partial class MainForm : Form
{
    /// <summary>
    /// Scopes the encrypted key store and the remembered provider, so this sample's settings do
    /// not collide with another app that embeds the same panel.
    /// </summary>
    private const string ApplicationName = "CDS.ScriptChat.SampleApp";

    private const string DefaultScript = """
        // Check every part on the fixture against the job's tolerance.
        foreach (var part in API.Parts)
        {
            var reading = API.Measure(part);
            API.Record(part, reading >= LowerLimitMm && reading <= UpperLimitMm);
        }

        API.Log($"{API.PassCount} passed, {API.FailCount} failed.");
        """;

    /// <summary>Initialises a new instance of the <see cref="MainForm"/> class.</summary>
    public MainForm()
    {
        InitializeComponent();

        _scriptTextBox.Text = DefaultScript;
        InitializeChat();
    }

    /// <summary>
    /// The entire integration: tell the panel how to read and write the script, and which type
    /// describes what a script can reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>api: typeof(ScriptGlobals)</c> does two jobs at once. It generates the list of what
    /// exists for the system prompt, and it answers <c>lookup_symbol</c> — with real signatures
    /// and the XML documentation on <see cref="InspectionApi"/> — out of this assembly's own
    /// metadata. Because both come from the same type, what the assistant is told exists and what
    /// it can ask about cannot drift apart.
    /// </para>
    /// <para>
    /// The prose above that list comes from <c>scriptchat.context.md</c>, picked up from beside
    /// the executable. Delete it and the assistant still works; it just knows less about why
    /// these scripts exist.
    /// </para>
    /// </remarks>
    private void InitializeChat()
    {
        _chatPanel.AddScript(
            name: "Inspection",
            read: () => _scriptTextBox.Text,
            write: script => _scriptTextBox.Text = script,
            api: typeof(ScriptGlobals));

        _chatPanel.UseStoredKey(ApplicationName);
    }

    private async void OnRunButtonClick(object? sender, EventArgs e)
    {
        _runButton.Enabled = false;
        _outputTextBox.Clear();

        try
        {
            await RunScriptAsync().ConfigureAwait(true);
        }
        finally
        {
            _runButton.Enabled = true;
        }
    }

    /// <summary>
    /// Runs the editor's contents against a fresh <see cref="ScriptGlobals"/>.
    /// </summary>
    /// <remarks>
    /// Nothing here involves the chat library — an adopting app runs its scripts however it
    /// already does. This exists so the sample shows the whole loop (ask, accept, run) rather
    /// than an editor whose contents never execute.
    /// </remarks>
    private async Task RunScriptAsync()
    {
        var globals = new ScriptGlobals { API = new InspectionApi(AppendOutput) };

        var options = ScriptOptions.Default
            .WithReferences(typeof(ScriptGlobals).Assembly)
            .WithImports("System", "System.Linq", "System.Collections.Generic");

        try
        {
            await CSharpScript.RunAsync(_scriptTextBox.Text, options, globals).ConfigureAwait(true);
        }
        catch (CompilationErrorException exception)
        {
            AppendOutput("The script did not compile:");
            foreach (var diagnostic in exception.Diagnostics)
            {
                AppendOutput($"  {diagnostic}");
            }
        }
        catch (Exception exception)
        {
            // The script is user-supplied code: anything it throws ends here rather than taking
            // the app down. A real host would log this too.
            AppendOutput($"The script failed: {exception.Message}");
        }
    }

    private void AppendOutput(string message)
    {
        if (_outputTextBox.InvokeRequired)
        {
            _outputTextBox.BeginInvoke(() => AppendOutput(message));
            return;
        }

        _outputTextBox.AppendText(message + Environment.NewLine);
    }
}
