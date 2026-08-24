using AwesomeAssertions;

using Microsoft.CodeAnalysis;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Exercises <see cref="RoslynSymbolResolver"/> against a real compilation over this test
/// assembly, standing in for the one a host's editor hands over (D22).
/// </summary>
/// <remarks>
/// The compilation carries no syntax trees: everything asked of it is a metadata lookup, which is
/// exactly what a script's compilation is used for here. Documentation comes from the <c>.xml</c>
/// beside the assembly, so the summary test also pins down that an assembly without its
/// documentation file has nothing to say, however well commented.
/// </remarks>
[TestClass]
[TestCategory("Roslyn")]
public sealed class RoslynSymbolResolverTests
{
    private const string TestNamespace = "CDS.ScriptChat.Core.Tests";

    private static Compilation Compilation => MetadataCompilation.FromTypes(typeof(SampleGlobals));

    private static RoslynSymbolResolver CreateResolver() =>
        new([TestNamespace], typeof(SampleGlobals), typeof(SamplePanel));

    [TestMethod]
    public void Resolve_WithABareTypeName_QualifiesItWithTheScriptsImports()
    {
        var result = CreateResolver().Resolve(Compilation, "SamplePanel", containingType: null);

        result.Should().NotBeNull();
        result!.Namespace.Should().Be(TestNamespace);
    }

    [TestMethod]
    public void Resolve_WithAMemberAndItsDeclaringType_ReturnsTheSignature()
    {
        var result = CreateResolver().Resolve(Compilation, "CreatePanel", "SampleApi");

        result.Should().NotBeNull();
        result!.Signature.Should().Contain("SampleApi.CreatePanel(string name)");
    }

    [TestMethod]
    public void Resolve_WithAGlobalsPropertyNameAsTheDeclaringType_ResolvesToTheTypeBehindIt()
    {
        // The script writes API.CreatePanel(...), so "API" is what a caller names as the
        // declaring type — a property, not a type. Nothing in the library knows a host calls
        // it "API".
        var result = CreateResolver().Resolve(Compilation, "CreatePanel", "API");

        result.Should().NotBeNull();
        result!.Signature.Should().Contain("SampleApi.CreatePanel(string name)");
    }

    [TestMethod]
    public void Resolve_WithABareMemberName_SearchesTheScriptFacingTypes()
    {
        // No declaring type given: SetValue is only findable because SamplePanel's .API facade
        // is one of the roots HostApiIndex produces.
        var result = CreateResolver().Resolve(Compilation, "SetValue", containingType: null);

        result.Should().NotBeNull();
        result!.Signature.Should().Contain("SetValue(int value)");
    }

    [TestMethod]
    public void Resolve_WithAWrongDeclaringType_FallsBackRatherThanReportingAMiss()
    {
        var result = CreateResolver().Resolve(Compilation, "SetValue", "SampleApi");

        result.Should().NotBeNull("a caller guessing the wrong declaring type should still get an answer");
        result!.Signature.Should().Contain("SetValue(int value)");
    }

    [TestMethod]
    public void Resolve_WithOverloads_ReturnsTheOthersAlongside()
    {
        var result = CreateResolver().Resolve(Compilation, "Log", "SampleApi");

        result.Should().NotBeNull();
        result!.Overloads.Should().ContainSingle();
    }

    [TestMethod]
    public void Resolve_ReadsTheXmlDocSummaryFlattenedOntoOneLine()
    {
        var result = CreateResolver().Resolve(Compilation, "CreatePanel", "SampleApi");

        result.Should().NotBeNull();
        result!.XmlDocSummary.Should().Be(
            "Creates a panel and docks it. Deliberately documented across several lines, so a "
            + "test can prove the summary comes back flattened.");
    }

    [TestMethod]
    public void Resolve_WithAnUnknownName_ReturnsNull()
    {
        var result = CreateResolver().Resolve(Compilation, "NoSuchThingAnywhere", containingType: null);

        result.Should().BeNull("a miss is an ordinary outcome and must be reported as one");
    }

    [TestMethod]
    public void Resolve_WithoutTheScriptsImports_DoesNotFindABareTypeName()
    {
        // The imports are the whole reason a bare name resolves. Without them "not found" means
        // what it says: not reachable from this script.
        var resolver = new RoslynSymbolResolver([], typeof(SampleGlobals), typeof(SamplePanel));

        resolver.Resolve(Compilation, "SamplePanel", containingType: null).Should().BeNull();
    }

    [TestMethod]
    public void Resolve_WithAnEmptySymbolName_ReturnsNull() =>
        CreateResolver().Resolve(Compilation, "   ", containingType: null).Should().BeNull();

    [TestMethod]
    public void Resolve_WithANullCompilation_Throws()
    {
        var act = () => CreateResolver().Resolve(null!, "SamplePanel", null);

        act.Should().Throw<ArgumentNullException>();
    }
}
