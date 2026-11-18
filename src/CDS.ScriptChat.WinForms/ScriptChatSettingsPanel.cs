using System.ComponentModel;
using System.Diagnostics;

using CDS.ScriptChat.Core;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDS.ScriptChat.WinForms;

/// <summary>
/// BYOK onboarding (UC6): choose a provider and model, enter your own API key, and check it
/// works before using it.
/// </summary>
/// <remarks>
/// The key is held in the text box only as long as it takes to store it, and is written
/// through <see cref="IApiKeyStore"/> — never logged, never put in a status message, and never
/// sent anywhere but the provider's own SDK call (D3).
/// </remarks>
public partial class ScriptChatSettingsPanel : UserControl
{
    private readonly Dictionary<ScriptChatProvider, IReadOnlyList<string>> _fetchedModelIds = [];
    private IApiKeyStore? _keyStore;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = NullLogger.Instance;
    private bool _suppressProviderChange;
    private CancellationTokenSource? _modelFetchCancellation;

    /// <summary>Initialises a new instance of the <see cref="ScriptChatSettingsPanel"/> class.</summary>
    public ScriptChatSettingsPanel()
    {
        InitializeComponent();
        PopulateProviders();
    }

    /// <summary>
    /// Raised when the user applies a configuration that is complete enough to use.
    /// </summary>
    public event EventHandler<ScriptChatConfigurationEventArgs>? ConfigurationApplied;

    /// <summary>
    /// Gets or sets where API keys are kept between sessions. Leave unset to keep keys in
    /// memory for the lifetime of the panel only.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IApiKeyStore? KeyStore
    {
        get => _keyStore;
        set
        {
            _keyStore = value;
            LoadStoredKeyForSelectedProvider();
            _ = RefreshModelsAsync();
        }
    }

    /// <summary>
    /// Gets or sets where every new panel gets its live model list from. Tests replace it so that
    /// constructing a panel with a stored key never reaches a real provider.
    /// </summary>
    internal static Func<ScriptChatProvider, string, CancellationToken, Task<IReadOnlyList<ScriptChatModelInfo>>> DefaultModelListSource { get; set; } =
        ScriptChatModelCatalog.ListModelsAsync;

    /// <summary>Gets or sets where this panel gets its live model list from.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<ScriptChatProvider, string, CancellationToken, Task<IReadOnlyList<ScriptChatModelInfo>>> ModelListSource { get; set; } =
        DefaultModelListSource;

    /// <summary>
    /// Gets or sets the factory the panel logs through, including the connection test it runs.
    /// <see langword="null"/> — the default — disables logging.
    /// </summary>
    /// <remarks>
    /// Never receives key material at any level, only a key's length (D3). Set this before
    /// <see cref="KeyStore"/> if you want the store's own load to be logged.
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILoggerFactory? LoggerFactory
    {
        get => _loggerFactory;
        set
        {
            _loggerFactory = value;
            _logger = value?.CreateLogger(typeof(ScriptChatSettingsPanel)) ?? NullLogger.Instance;
        }
    }

    /// <summary>Gets the provider currently selected.</summary>
    [Browsable(false)]
    public ScriptChatProvider SelectedProvider =>
        _providerComboBox.SelectedItem as ScriptChatProvider? ?? ScriptChatProvider.Claude;

    /// <summary>Gets the model ID currently entered or selected.</summary>
    [Browsable(false)]
    public string SelectedModelId => _modelComboBox.Text.Trim();

    /// <summary>
    /// Gets a value indicating whether a key is present for the selected provider — without
    /// exposing the key itself.
    /// </summary>
    [Browsable(false)]
    public bool HasApiKey => _apiKeyTextBox.Text.Trim().Length > 0;

    /// <summary>
    /// Builds the client options from what is currently on screen.
    /// </summary>
    /// <returns>The options, ready for <see cref="ScriptChatClientFactory.Create(ScriptChatClientOptions, ILoggerFactory?)"/>.</returns>
    /// <exception cref="InvalidOperationException">No API key or model has been entered.</exception>
    public ScriptChatClientOptions BuildClientOptions()
    {
        var apiKey = _apiKeyTextBox.Text.Trim();
        if (apiKey.Length == 0)
        {
            throw new InvalidOperationException("No API key has been entered.");
        }

        var modelId = SelectedModelId;
        if (modelId.Length == 0)
        {
            throw new InvalidOperationException("No model has been selected.");
        }

        return new ScriptChatClientOptions
        {
            Provider = SelectedProvider,
            ApiKey = apiKey,
            ModelId = modelId,
        };
    }

    private void PopulateProviders()
    {
        _suppressProviderChange = true;
        try
        {
            _providerComboBox.Items.Clear();
            foreach (var provider in Enum.GetValues<ScriptChatProvider>())
            {
                _providerComboBox.Items.Add(provider);
            }

            _providerComboBox.SelectedItem = ScriptChatProvider.Claude;
        }
        finally
        {
            _suppressProviderChange = false;
        }

        PopulateModelsForSelectedProvider();
    }

    private void PopulateModelsForSelectedProvider()
    {
        var provider = SelectedProvider;
        var modelIds = _fetchedModelIds.TryGetValue(provider, out var fetched)
            ? fetched
            : ScriptChatModels.ForProvider(provider);

        ReplaceModelItems(modelIds);
        _modelComboBox.Text = ScriptChatModels.DefaultForProvider(provider);
    }

    /// <summary>
    /// Replaces the built-in model list with the one the provider reports for the entered key,
    /// once per provider for the life of the panel. Any failure leaves the built-in list in place.
    /// </summary>
    internal async Task RefreshModelsAsync()
    {
        var provider = SelectedProvider;
        var apiKey = _apiKeyTextBox.Text.Trim();
        if (apiKey.Length == 0
            || !ScriptChatModelCatalog.CanListModels(provider)
            || _fetchedModelIds.ContainsKey(provider))
        {
            return;
        }

        _modelFetchCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _modelFetchCancellation = cancellation;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var models = await ModelListSource(provider, apiKey, cancellation.Token).ConfigureAwait(true);
            _logger.ModelListFetched(provider, stopwatch.ElapsedMilliseconds, models.Count);

            if (models.Count == 0)
            {
                return;
            }

            var modelIds = models.Select(model => model.Id).ToArray();
            _fetchedModelIds[provider] = modelIds;

            if (!IsDisposed && SelectedProvider == provider)
            {
                var entered = _modelComboBox.Text;
                ReplaceModelItems(modelIds);
                _modelComboBox.Text = entered;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.ModelListFetchFailed(ex, provider, stopwatch.ElapsedMilliseconds);

            if (!IsDisposed && SelectedProvider == provider)
            {
                SetStatus($"Could not fetch the model list: {ex.Message}");
            }
        }
    }

    private void ReplaceModelItems(IReadOnlyList<string> modelIds)
    {
        _modelComboBox.BeginUpdate();
        try
        {
            _modelComboBox.Items.Clear();
            foreach (var modelId in modelIds)
            {
                _modelComboBox.Items.Add(modelId);
            }
        }
        finally
        {
            _modelComboBox.EndUpdate();
        }
    }

    private void LoadStoredKeyForSelectedProvider()
    {
        if (_keyStore is null)
        {
            return;
        }

        try
        {
            _apiKeyTextBox.Text = _keyStore.Load(SelectedProvider) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.ApiKeyStoreFailed(ex, "read", SelectedProvider);
            _apiKeyTextBox.Clear();
            SetStatus("Could not read the stored key. Enter it again.");
        }
    }

    private void OnProviderSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressProviderChange)
        {
            return;
        }

        PopulateModelsForSelectedProvider();
        LoadStoredKeyForSelectedProvider();
        SetStatus(string.Empty);

        _logger.ProviderSelectionChanged(SelectedProvider, SelectedModelId, HasApiKey);

        _ = RefreshModelsAsync();
    }

    private void OnApplyButtonClick(object? sender, EventArgs e)
    {
        ScriptChatClientOptions options;
        try
        {
            options = BuildClientOptions();
        }
        catch (InvalidOperationException ex)
        {
            _logger.ConfigurationIncomplete(ex.Message);
            SetStatus(ex.Message);
            return;
        }

        if (!TryPersistKey(options))
        {
            return;
        }

        SetStatus($"Applied {options.Provider} · {options.ModelId}.");
        _logger.ConfigurationApplied(options.Provider, options.ModelId, options.ApiKey.Length);
        ConfigurationApplied?.Invoke(this, new ScriptChatConfigurationEventArgs(options));

        _ = RefreshModelsAsync();
    }

    private async void OnTestButtonClick(object? sender, EventArgs e)
    {
        ScriptChatClientOptions options;
        try
        {
            options = BuildClientOptions();
        }
        catch (InvalidOperationException ex)
        {
            _logger.ConfigurationIncomplete(ex.Message);
            SetStatus(ex.Message);
            return;
        }

        SetButtonsEnabled(false);
        SetStatus("Testing…");
        _logger.ConnectionTestStarted(options.Provider, options.ModelId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = ScriptChatClientFactory.Create(options, _loggerFactory);

            // Deliberately tiny: this is a credential and reachability check, not a warm-up.
            var response = await client
                .GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "Reply with the single word: ok")],
                    new ChatOptions { MaxOutputTokens = 16 })
                .ConfigureAwait(true);

            SetStatus(string.IsNullOrWhiteSpace(response.Text)
                ? "Connected, but the provider returned nothing."
                : "Connection succeeded.");

            _logger.ConnectionTestSucceeded(
                stopwatch.ElapsedMilliseconds,
                options.Provider,
                response.Text?.Length ?? 0);

            _ = RefreshModelsAsync();
        }
        catch (Exception ex)
        {
            // The provider's message can be surfaced: it describes the failure, and the key
            // itself is never part of it. The log keeps the stack the status label cannot show.
            _logger.ConnectionTestFailed(ex, stopwatch.ElapsedMilliseconds, options.Provider);
            SetStatus($"Connection failed: {ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private void OnForgetKeyButtonClick(object? sender, EventArgs e)
    {
        _apiKeyTextBox.Clear();

        if (_keyStore is null)
        {
            SetStatus("Key cleared.");
            return;
        }

        try
        {
            _keyStore.Clear(SelectedProvider);
            SetStatus("Stored key removed.");
        }
        catch (Exception ex)
        {
            _logger.ApiKeyStoreFailed(ex, "remove", SelectedProvider);
            SetStatus($"Could not remove the stored key: {ex.Message}");
        }
    }

    private bool TryPersistKey(ScriptChatClientOptions options)
    {
        if (_keyStore is null)
        {
            return true;
        }

        try
        {
            _keyStore.Save(options.Provider, options.ApiKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.ApiKeyStoreFailed(ex, "store", options.Provider);
            SetStatus($"Could not store the key: {ex.Message}");
            return false;
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _applyButton.Enabled = enabled;
        _testButton.Enabled = enabled;
        _forgetKeyButton.Enabled = enabled;
    }

    private void SetStatus(string status)
    {
        _statusLabel.Text = status;
    }
}
