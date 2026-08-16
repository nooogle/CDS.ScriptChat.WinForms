using System.ComponentModel;

using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// A dialogue around <see cref="ScriptChatSettingsPanel"/>: choose a provider and model, enter
/// your own API key, and test that it works.
/// </summary>
/// <remarks>
/// <para>
/// Bring-your-own-key only. The key is written through <see cref="IApiKeyStore"/> if one is set —
/// encrypted under DPAPI for the current Windows user via <see cref="DpapiApiKeyStore"/> — and
/// never reaches the panel's own log.
/// </para>
/// <para>
/// A host typically keeps one instance for the lifetime of the session rather than creating one
/// per use, since the panel exposes no way to preselect a provider or model: a fresh instance
/// would always open on the panel's defaults, so applying it would silently switch a user who had
/// chosen something else.
/// </para>
/// </remarks>
public partial class ScriptChatSettingsForm : Form
{
    /// <summary>Initialises a new instance of the <see cref="ScriptChatSettingsForm"/> class.</summary>
    public ScriptChatSettingsForm()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user applies a configuration that is complete enough to use.</summary>
    public event EventHandler<ScriptChatConfigurationEventArgs>? ConfigurationApplied
    {
        add => _settingsPanel.ConfigurationApplied += value;
        remove => _settingsPanel.ConfigurationApplied -= value;
    }

    /// <summary>Gets or sets where API keys are kept between sessions.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IApiKeyStore? KeyStore
    {
        get => _settingsPanel.KeyStore;
        set => _settingsPanel.KeyStore = value;
    }

    /// <summary>
    /// Gets or sets the factory the settings panel logs through. Set it before
    /// <see cref="KeyStore"/> so the store's own load is logged too.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILoggerFactory? LoggerFactory
    {
        get => _settingsPanel.LoggerFactory;
        set => _settingsPanel.LoggerFactory = value;
    }
}
