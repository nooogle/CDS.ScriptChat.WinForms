using CDS.ScriptChat.Core;

namespace CDS.ScriptChat.TestHost;

/// <summary>
/// A wholly invented machine-vision API, used to prove that <c>lookup_symbol</c> is doing real
/// work.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here exists. "Kestrel" is not a real library, so a model cannot know it from
/// training data or find it on the internet — the only way to write correct Kestrel code is to
/// call <c>lookup_symbol</c> and read the answer.
/// </para>
/// <para>
/// The signatures are deliberately awkward, so guessing from convention produces
/// <em>wrong</em> code rather than accidentally-right code:
/// </para>
/// <list type="bullet">
///   <item><description>Denoising is called <c>Sift</c>, which no one would guess.</description></item>
///   <item><description><c>Sift</c>'s window parameter must be odd, and it is called <c>cadence</c>.</description></item>
///   <item><description><c>CountMotes</c> returns a report object, not an <see cref="int"/>.</description></item>
///   <item><description>Components come from <c>Workspace.Acquire</c>, not a <c>Get…</c> method.</description></item>
/// </list>
/// </remarks>
internal static class KestrelApiCatalogue
{
    /// <summary>
    /// Gets the invented symbols, keyed by the name a caller would ask for. Lookup is
    /// case-insensitive so the model is not punished for casing.
    /// </summary>
    public static IReadOnlyDictionary<string, SymbolLookupResult> Symbols { get; } =
        new Dictionary<string, SymbolLookupResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sift"] = new SymbolLookupResult
            {
                Signature = "public static KestrelFrame Sift(KestrelFrame frame, int cadence, SiftPolarity polarity)",
                Namespace = "Kestrel.Imaging",
                XmlDocSummary =
                    "Removes speckle noise from a frame. 'cadence' is the window size and MUST be odd — "
                    + "an even value throws KestrelArgumentException. Use SiftPolarity.Inverted for "
                    + "dark-field frames; Normal is only correct for bright-field. Returns a new frame; "
                    + "the input is not modified.",
                Overloads =
                [
                    "public static KestrelFrame Sift(KestrelFrame frame, int cadence, SiftPolarity polarity)",
                    "public static KestrelFrame Sift(KestrelFrame frame, SiftProfile profile)",
                ],
            },

            ["CountMotes"] = new SymbolLookupResult
            {
                Signature = "public static MoteReport CountMotes(KestrelFrame frame, MoteFilter filter)",
                Namespace = "Kestrel.Imaging",
                XmlDocSummary =
                    "Counts discrete bright regions ('motes'). Returns a MoteReport, NOT an int — read "
                    + "report.Total for the count. The frame must already be sifted; passing a raw frame "
                    + "throws KestrelStateException.",
            },

            ["MoteReport"] = new SymbolLookupResult
            {
                Signature = "public sealed class MoteReport",
                Namespace = "Kestrel.Imaging",
                XmlDocSummary =
                    "The result of CountMotes. Members: int Total, double MeanArea, "
                    + "IReadOnlyList<Mote> Motes.",
            },

            ["SiftPolarity"] = new SymbolLookupResult
            {
                Signature = "public enum SiftPolarity { Normal, Inverted }",
                Namespace = "Kestrel.Imaging",
                XmlDocSummary =
                    "Selects how Sift interprets contrast. Normal for bright-field frames, Inverted for "
                    + "dark-field. There is no automatic option.",
            },

            ["MoteFilter"] = new SymbolLookupResult
            {
                Signature = "public sealed record MoteFilter(double MinArea, double MaxArea)",
                Namespace = "Kestrel.Imaging",
                XmlDocSummary =
                    "Bounds which motes are counted, in square microns. Use MoteFilter.Any to count "
                    + "everything.",
            },

            ["Acquire"] = new SymbolLookupResult
            {
                Signature = "public T Acquire<T>(string slot) where T : class",
                Namespace = "Kestrel.Pipeline",
                XmlDocSummary =
                    "Fetches a pipeline component from the workspace by slot name. Slot names are "
                    + "declared in the .kestrel manifest, never in code. Throws KestrelSlotException if "
                    + "the slot is missing or the type does not match.",
            },

            ["KestrelFrame"] = new SymbolLookupResult
            {
                Signature = "public sealed class KestrelFrame",
                Namespace = "Kestrel.Imaging",
                XmlDocSummary =
                    "An immutable 16-bit greyscale frame. Members: int Width, int Height, "
                    + "KestrelFrame Rebase(double gamma). Every operation returns a new frame.",
            },

            ["Rebase"] = new SymbolLookupResult
            {
                Signature = "public KestrelFrame Rebase(double gamma)",
                Namespace = "Kestrel.Imaging",
                XmlDocSummary =
                    "Applies a gamma correction and returns a new frame. Gamma must be greater than 0; "
                    + "1.0 is a no-op.",
            },
        };
}
