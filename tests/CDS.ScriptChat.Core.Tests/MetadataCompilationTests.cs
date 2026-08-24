using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Covers <see cref="MetadataCompilation"/> — the path for a host that runs scripts but never
/// exposes a Roslyn compilation of its own.
/// </summary>
[TestClass]
[TestCategory("Roslyn")]
public sealed class MetadataCompilationTests
{
    [TestMethod]
    public void FromTypes_ResolvesATypeFromTheHostsOwnAssembly()
    {
        var compilation = MetadataCompilation.FromTypes(typeof(SampleGlobals));

        compilation.GetTypeByMetadataName("CDS.ScriptChat.Core.Tests.SampleApi").Should().NotBeNull();
    }

    [TestMethod]
    public void FromTypes_ResolvesFrameworkTypesToo()
    {
        // A script's API surface reaches into the BCL constantly; a compilation that only knows
        // the host's own assembly would answer "not found" for most real questions.
        var compilation = MetadataCompilation.FromTypes(typeof(SampleGlobals));

        compilation.GetTypeByMetadataName("System.String").Should().NotBeNull();
    }

    [TestMethod]
    public void FromTypes_AttachesXmlDocumentation()
    {
        // The trap this class exists to close: Roslyn does not look for the .xml on its own, so
        // without an explicit DocumentationProvider every lookup returns a correct signature with
        // no documentation — and nothing looks wrong.
        var resolver = new RoslynSymbolResolver(
            ["CDS.ScriptChat.Core.Tests"],
            typeof(SampleGlobals),
            typeof(SamplePanel));

        var result = resolver.Resolve(
            MetadataCompilation.FromTypes(typeof(SampleGlobals)),
            "SetValue",
            containingType: null);

        result.Should().NotBeNull();
        result!.XmlDocSummary.Should().Be("Sets the displayed value.");
    }

    [TestMethod]
    public void FromTypes_WithNoTypes_Throws()
    {
        var act = () => MetadataCompilation.FromTypes();

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void FromTypes_WithNullTypes_Throws()
    {
        var act = () => MetadataCompilation.FromTypes(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void FromAssemblies_WithNoAssemblies_Throws()
    {
        var act = () => MetadataCompilation.FromAssemblies();

        act.Should().Throw<ArgumentException>();
    }
}
