using System.Diagnostics;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.WinForms;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDS.ScriptChat.TestHost;

/// <summary>
/// A minimal host: a plain text box standing in for the editor, the chat panel, the settings
/// panel, a live view of every <c>lookup_symbol</c> call, and a link to this run's CSV log.
/// </summary>
/// <remarks>
/// This is the reference wiring a consuming app copies. Note what the host supplies and the
/// library does not (D15): the script getter and setter, an <see cref="ISymbolLookupProvider"/>,
/// and an <see cref="ILoggerFactory"/>.
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
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly string? _logFilePath;
    private readonly bool _traceEnabled;

    /// <summary>
    /// Initialises a new instance of the <see cref="MainForm"/> class with no logging.
    /// </summary>
    /// <remarks>
    /// Present so the form opens in the WinForms Designer, which can only construct a type that
    /// has a parameterless constructor (D14). The application itself uses the overload.
    /// </remarks>
    public MainForm()
        : this(NullLoggerFactory.Instance, logFilePath: null, traceEnabled: false)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="MainForm"/> class.
    /// </summary>
    /// <param name="loggerFactory">
    /// The factory every part of the panel logs through. One property on each control, set
    /// below, instruments the whole chain down to the provider round-trips.
    /// </param>
    /// <param name="logFilePath">
    /// The CSV file this run is writing, shown at the bottom of the window so it can be opened
    /// while the app is still running. <see langword="null"/> when nothing is being written.
    /// </param>
    /// <param name="traceEnabled">
    /// Whether <paramref name="loggerFactory"/> was configured for <c>Trace</c> — shown in the
    /// window so a run that is recording prompt/script content is never silently so (D3, D17).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
    public MainForm(ILoggerFactory loggerFactory, string? logFilePath, bool traceEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MainForm>();
        _logFilePath = logFilePath;
        _traceEnabled = traceEnabled;

        InitializeComponent();

        _scriptTextBox.Text = StarterScript;
        _symbolLookup.SymbolRequested += OnSymbolRequested;

        WireLogging();
        WireChatPanelToTheEditor();
        WireSettingsPanel();

        _logger.LogInformation(
            "Test host started. LogFile={LogFile} StarterScriptLength={StarterScriptLength}",
            logFilePath ?? "(none)",
            StarterScript.Length);
    }

    /// <summary>
    /// Hands the library its logger factory, and shows the user where the log is going.
    /// </summary>
    private void WireLogging()
    {
        _chatPanel.LoggerFactory = _loggerFactory;

        // Before KeyStore, so the settings panel's initial load of the stored key is logged too.
        _settingsPanel.LoggerFactory = _loggerFactory;

        _logFileLinkLabel.Text = _logFilePath is null
            ? "Logging is off for this run."
            : _traceEnabled
                ? $"Log (--trace: contains your prompts, replies, and scripts): {_logFilePath}"
                : $"Log: {_logFilePath}";
        _logFileLinkLabel.LinkArea = _logFilePath is null
            ? default
            : new LinkArea(0, _logFileLinkLabel.Text.Length);
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
        _settingsPanel.KeyStore = DpapiApiKeyStore.ForApplication(
            Program.ApplicationName,
            _loggerFactory.CreateLogger<DpapiApiKeyStore>());
        _settingsPanel.ConfigurationApplied += OnConfigurationApplied;
    }

    private void OnConfigurationApplied(object? sender, ScriptChatConfigurationEventArgs e)
    {
        // Reads scriptchat.context.md from the output directory; falls back to a host-supplied
        // property if the file is absent (D12). Here the file is the source.
        var orientation = HostOrientationResolver.Resolve(
            hostContext: null,
            searchDirectory: null,
            logger: _loggerFactory.CreateLogger(typeof(HostOrientationResolver)));

        _lookupListBox.Items.Clear();

        // LoggerFactory is left unset here: the chat panel fills it in from its own, so a host
        // only has to wire logging once.
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

    private void OnLogFileLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (_logFilePath is null)
        {
            return;
        }

        try
        {
            // Whatever the user has .csv associated with — usually a spreadsheet. The file is
            // flushed on every row, so it is readable while the app is still running.
            Process.Start(new ProcessStartInfo(_logFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the log file at {LogFile}.", _logFilePath);
            MessageBox.Show(
                this,
                $"Could not open the log file:{Environment.NewLine}{ex.Message}",
                "Log file",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
