namespace CDS.ScriptChat.Core;

/// <summary>
/// What a host app knows about one symbol: enough for the model to write correct calls
/// against it without relying on training-data recall.
/// </summary>
public sealed record SymbolLookupResult
{
    /// <summary>The full display signature of the symbol.</summary>
    public required string Signature { get; init; }

    /// <summary>The fully-qualified namespace the symbol lives in.</summary>
    public required string Namespace { get; init; }

    /// <summary>Content of the XML <c>&lt;summary&gt;</c> documentation tag, when available.</summary>
    public string? XmlDocSummary { get; init; }

    /// <summary>Display signatures of the other overloads. Empty when the symbol has none.</summary>
    public IReadOnlyList<string> Overloads { get; init; } = [];
}
