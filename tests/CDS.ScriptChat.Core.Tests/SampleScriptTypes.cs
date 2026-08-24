namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// A host's own globals type, in the shape a consumer brings: one property returning its API
/// class, and some plain data alongside it. The library imposes nothing on this — which is what
/// <see cref="HostApiIndex"/> and <see cref="RoslynSymbolResolver"/> have to cope with.
/// </summary>
public sealed class SampleGlobals
{
    /// <summary>The class the script reaches the host through.</summary>
    public SampleApi API { get; } = new();

    /// <summary>Plain data a script reads by name, with no type worth indexing behind it.</summary>
    public int Threshold { get; init; }

    /// <summary>The run's cancellation token — framework-typed, so not followed into.</summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>The class a sample script reaches the host through.</summary>
public sealed class SampleApi
{
    /// <summary>
    /// Creates a panel and docks it. Deliberately documented across
    /// several lines, so a test can prove the summary comes back flattened.
    /// </summary>
    /// <param name="name">What to call it.</param>
    /// <returns>The new panel.</returns>
    public SamplePanel CreatePanel(string name) => new() { Name = name };

    /// <summary>Writes a message to the host's output.</summary>
    /// <param name="message">What to write.</param>
    public void Log(string message)
    {
    }

    /// <summary>Writes a message at a given level.</summary>
    /// <param name="message">What to write.</param>
    /// <param name="level">How important it is.</param>
    public void Log(string message, int level)
    {
    }
}

/// <summary>
/// Stands in for a UI framework base class. Its members exist so a test can prove the index lists
/// only what a host declares itself, not everything a control inherits — Core targets plain
/// <c>net10.0</c>, so a real <c>UserControl</c> is not available to derive from here.
/// </summary>
public abstract class SampleControlBase
{
    /// <summary>Inherited: must not appear in the index.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Inherited: must not appear in the index.</summary>
    public bool Visible { get; set; }

    /// <summary>Inherited: must not appear in the index.</summary>
    public void Refresh()
    {
    }
}

/// <summary>
/// A host's own panel, deriving from a base with members of its own, so that the index has
/// inherited members it is supposed to leave out.
/// </summary>
public sealed class SamplePanel : SampleControlBase
{
    /// <summary>The members added on top of the control, kept behind <c>.API</c>.</summary>
    public SamplePanelApi API { get; } = new();

    /// <summary>The members added on top of the control.</summary>
    public sealed class SamplePanelApi
    {
        /// <summary>Sets the displayed value.</summary>
        /// <param name="value">The value to show.</param>
        public void SetValue(int value)
        {
        }
    }
}

/// <summary>
/// A flat domain API with no globals indirection — the shape of an app that has scripts but no
/// globals type. <c>Describe</c> used to return an empty string for this, silently.
/// </summary>
public sealed class FlatSampleApi
{
    /// <summary>Starts an inspection.</summary>
    /// <param name="recipeName">Which recipe to run.</param>
    public void StartInspection(string recipeName)
    {
    }

    /// <summary>The camera exposure, in milliseconds.</summary>
    public double ExposureMs { get; set; }
}
