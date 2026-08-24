namespace CDS.ScriptChat.SampleApp;

/// <summary>
/// What a script sees without qualifying anything: the station's API, plus the tolerance the
/// current job is running to.
/// </summary>
/// <remarks>
/// This is the type handed to <c>AddScript</c>, and it is the single thing that tells the
/// assistant what scripts here can reach. Everything else — the orientation index in the system
/// prompt, and what <c>lookup_symbol</c> will resolve — is derived from it by reflection, so the
/// two cannot drift apart and neither can fall behind the code.
/// </remarks>
public sealed class ScriptGlobals
{
    /// <summary>Gets the inspection station's API.</summary>
    public required InspectionApi API { get; init; }

    /// <summary>Gets the largest dimension, in millimetres, that still counts as a pass.</summary>
    public double UpperLimitMm { get; init; } = 12.5;

    /// <summary>Gets the smallest dimension, in millimetres, that still counts as a pass.</summary>
    public double LowerLimitMm { get; init; } = 11.5;
}
