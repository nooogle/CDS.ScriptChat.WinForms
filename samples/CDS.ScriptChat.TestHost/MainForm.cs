using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

namespace CDS.ScriptChat.TestHost;

/// <summary>
/// A minimal host: a plain text box standing in for the editor, the chat panel, the settings
/// panel, and a live view of every <c>lookup_symbol</c> call.
/// </summary>
/// <remarks>
/// This is the reference wiring a consuming app copies. Note what the host supplies and the
/// library does not (D15): the script getter and setter, and an
/// <see cref="ISymbolLookupProvider"/>.
/// </remarks>
public partial class MainForm : Form
{
    private const string StarterScript = """
        // Kestrel script. 'frame' is supplied by the host; return a KestrelFrame.
        // Try asking the panel: "denoise this dark-field frame, then count the bright spots"
        var result = frame;
        return result;
        """;

    private readonly KestrelSymbolLookupProvider _symbolLookup = new();

    /// <summary>Initialises a new instance of the <see cref="MainForm"/> class.</summary>
    public MainForm()
    {
        InitializeComponent();

        _scriptTextBox.Text = StarterScript;
        _symbolLookup.SymbolRequested += OnSymbolRequested;

        WireChatPanelToTheEditor();
        WireSettingsPanel();
    }

    /// <summary>
    /// Gives the panel a way to read and write the script. These two delegates are the whole
    /// editor contract — there is no editor interface to implement (D15).
    /// </summary>
    private void WireChatPanelToTheEditor()
    {
        _chatPanel.ScriptTextProvider = () => _scriptTextBox.Text;
        _chatPanel.ScriptTextSetter = script => _scriptTextBox.Text = script;
        _chatPanel.SetUnavailable("Enter your API key above, then choose Apply.");
    }

    private void WireSettingsPanel()
    {
        _settingsPanel.KeyStore = DpapiApiKeyStore.ForApplication("CDS.ScriptChat.TestHost");
        _settingsPanel.ConfigurationApplied += OnConfigurationApplied;
    }

    private void OnConfigurationApplied(object? sender, ScriptChatConfigurationEventArgs e)
    {
        // Reads scriptchat.context.md from the output directory; falls back to a host-supplied
        // property if the file is absent (D12). Here the file is the source.
        var orientation = HostOrientationResolver.Resolve(hostContext: null);

        _lookupListBox.Items.Clear();

        _chatPanel.Configure(
            e.ClientOptions,
            new ScriptChatSessionOptions
            {
                SymbolLookup = _symbolLookup,
                OrientationBlurb = orientation,
            });
    }

    private void OnSymbolRequested(object? sender, SymbolLookupEventArgs e)
    {
        // Tool calls arrive on a background thread; the list box needs the UI thread.
        if (_lookupListBox.InvokeRequired)
        {
            _lookupListBox.BeginInvoke(() => AppendLookup(e));
            return;
        }

        AppendLookup(e);
    }

    private void AppendLookup(SymbolLookupEventArgs e)
    {
        var index = _lookupListBox.Items.Add(e.ToString());
        _lookupListBox.TopIndex = index;
    }
}
