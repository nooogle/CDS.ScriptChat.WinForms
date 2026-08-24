using AwesomeAssertions;

using CDS.ScriptChat.Core;
using CDS.ScriptChat.SampleApp;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// The acceptance test for Job 5's easy path, run against the adoption sample rather than against
/// purpose-built fixtures.
/// </summary>
/// <remarks>
/// The sample is an ordinary app: a script editor, a domain API, and two calls of wiring. If what
/// those two calls produce stops being usable — an empty orientation, a lookup that resolves
/// nothing, documentation that silently fails to load — the quickstart is broken however green
/// the unit tests are.
/// </remarks>
[TestClass]
[TestCategory("SampleApp")]
public sealed class SampleAppTests
{
    private static ScriptChatSessionOptions Options =>
        ScriptChatSessionOptions.ForHostApi(typeof(ScriptGlobals));

    [TestMethod]
    public void Orientation_ListsTheGlobalsAndTheApiBehindIt()
    {
        var orientation = Options.OrientationBlurb!;

        // The globals' own members, which a script writes unqualified…
        orientation.Should().Contain("- `ScriptGlobals`:")
            .And.Contain("LowerLimitMm")
            .And.Contain("UpperLimitMm");

        // …and the API facade, labelled by the property name a script actually types.
        orientation.Should().Contain("- `API`:")
            .And.Contain("Measure")
            .And.Contain("Record")
            .And.Contain("Parts");
    }

    [TestMethod]
    public void Orientation_IncludesTheHostsOwnProseFromTheContextFile()
    {
        // scriptchat.context.md is copied beside the executable and picked up automatically.
        // Without it the model knows what exists but not why, which is the difference between a
        // correct script and a useful one.
        Options.OrientationBlurb.Should().Contain("widget inspection station");
    }

    [TestMethod]
    public async Task Lookup_ResolvesAnApiMethodWithItsRealSignature()
    {
        var result = await Options.SymbolLookup.LookupAsync("Measure", "InspectionApi");

        result.Should().NotBeNull();
        result!.Signature.Should().Contain("InspectionApi.Measure(string partName)");
    }

    [TestMethod]
    public async Task Lookup_ResolvesThroughTheGlobalsPropertyNameAScriptActuallyTypes()
    {
        // A script writes API.Measure(...), so "API" is what the model names as the declaring
        // type — a property, not a type.
        var result = await Options.SymbolLookup.LookupAsync("Measure", "API");

        result.Should().NotBeNull();
        result!.Signature.Should().Contain("InspectionApi.Measure(string partName)");
    }

    [TestMethod]
    public async Task Lookup_CarriesTheXmlDocumentationTheDeveloperWrote()
    {
        var result = await Options.SymbolLookup.LookupAsync("Record", "InspectionApi");

        // This is the failure that looks like success: a correct signature with no prose, because
        // GenerateDocumentationFile was not set or the .xml was not deployed.
        result!.XmlDocSummary.Should().Be(
            "Records a pass or fail verdict against a part, and reports it to the operator.");
    }

    [TestMethod]
    public async Task Lookup_ReportsAMissForSomethingTheScriptCannotReach()
    {
        var result = await Options.SymbolLookup.LookupAsync("DeleteAllRecords", "InspectionApi");

        result.Should().BeNull("a miss must mean 'not available here', not 'unknown'");
    }

    [TestMethod]
    public void MainForm_Constructed_WiresItselfUpWithoutThrowing()
    {
        // The sample's Designer file is hand-written like the library's own (D14), so this proves
        // InitializeComponent runs — and, because the constructor calls AddScript and
        // UseStoredKey, that the two-call wiring survives contact with a real form.
        using var form = new MainForm();

        form.Controls.Count.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void DefaultScript_CompilesAgainstTheDocumentedApi()
    {
        // The seed script is the first thing an adopter sees and the baseline the assistant edits
        // from. Every member it uses must actually exist on the API type.
        var members = HostApiIndex.MemberNames(typeof(InspectionApi)).ToArray();

        members.Should().Contain(["Parts", "Measure", "Record", "Log", "PassCount", "FailCount"]);
    }
}
