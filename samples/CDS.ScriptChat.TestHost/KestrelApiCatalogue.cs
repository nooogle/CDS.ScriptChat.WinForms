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
/// <para>
/// Unguessable names need a way in, or a plain-English request is unanswerable: the model
/// probes for <c>Denoise</c>, misses, and correctly declines to invent an API. The namespace
/// entries added by <see cref="BuildCatalogue"/> are that way in — looking one up lists what it
/// contains. Discovery stays a lookup; only the starting point is given away.
/// </para>
/// </remarks>
internal static class KestrelApiCatalogue
{
    /// <summary>The invented symbols themselves, before the namespace index is derived from them.</summary>
    private static readonly Dictionary<string, SymbolLookupResult> s_symbols =
        new(StringComparer.OrdinalIgnoreCase)
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

            ["Workspace"] = new SymbolLookupResult
            {
                Signature = "KestrelWorkspace Workspace",
                Namespace = "Kestrel.Pipeline",
                XmlDocSummary =
                    "A host-supplied global, like 'frame'. The script's entry point to the pipeline: "
                    + "Acquire is an instance method, and this is what you call it on. Not needed for "
                    + "Kestrel.Imaging work, which is all static methods over a frame.",
            },

            ["KestrelWorkspace"] = new SymbolLookupResult
            {
                Signature = "public sealed class KestrelWorkspace",
                Namespace = "Kestrel.Pipeline",
                XmlDocSummary =
                    "The type of the Workspace global. Members: T Acquire<T>(string slot).",
            },

            ["Acquire"] = new SymbolLookupResult
            {
                Signature = "public T Acquire<T>(string slot) where T : class",
                Namespace = "Kestrel.Pipeline",
                XmlDocSummary =
                    "Fetches a pipeline component from the workspace by slot name. Call it on the "
                    + "Workspace global. Slot names are declared in the .kestrel manifest, never in "
                    + "code. Throws KestrelSlotException if the slot is missing or the type does not "
                    + "match.",
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

    /// <summary>
    /// Gets the invented symbols, keyed by the name a caller would ask for. Lookup is
    /// case-insensitive so the model is not punished for casing.
    /// </summary>
    public static IReadOnlyDictionary<string, SymbolLookupResult> Symbols { get; } = BuildCatalogue();

    /// <summary>
    /// Adds one entry per namespace, listing its members, plus a root entry listing the
    /// namespaces. Derived from <see cref="s_symbols"/> rather than written out, so a symbol
    /// added above cannot go missing from the index.
    /// </summary>
    private static IReadOnlyDictionary<string, SymbolLookupResult> BuildCatalogue()
    {
        var catalogue = new Dictionary<string, SymbolLookupResult>(s_symbols, StringComparer.OrdinalIgnoreCase);

        var namespaces = s_symbols
            .GroupBy(entry => entry.Value.Namespace, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var group in namespaces)
        {
            var members = string.Join(", ", group.Select(entry => entry.Key).Order(StringComparer.Ordinal));

            catalogue[group.Key] = new SymbolLookupResult
            {
                Signature = $"namespace {group.Key}",
                Namespace = group.Key,
                XmlDocSummary =
                    $"Contains: {members}. Look each one up for its signature and rules before "
                    + "calling it — the names do not follow the conventions of other imaging libraries.",
            };
        }

        catalogue["Kestrel"] = new SymbolLookupResult
        {
            Signature = "namespace Kestrel",
            Namespace = "Kestrel",
            XmlDocSummary =
                $"The root namespace. Sub-namespaces: {string.Join(", ", namespaces.Select(group => group.Key))}. "
                + "Look one up to list what it contains.",
        };

        return catalogue;
    }
}
