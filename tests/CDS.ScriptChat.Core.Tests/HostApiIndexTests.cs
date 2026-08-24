using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

/// <summary>
/// Covers <see cref="HostApiIndex"/> — the generated "what exists" list a host puts in its
/// orientation blurb.
/// </summary>
[TestClass]
[TestCategory("Roslyn")]
public sealed class HostApiIndexTests
{
    [TestMethod]
    public void Describe_LabelsAPropertysTypeByThePropertyName()
    {
        var index = HostApiIndex.Describe(typeof(SampleGlobals), typeof(SamplePanel));

        // A script writes API.CreatePanel(...), so the label must be the property name, not
        // "SampleApi" and not "SampleGlobals.API".
        index.Should().Contain("- `API`: CreatePanel, Log");
    }

    [TestMethod]
    public void Describe_ListsTheRootTypesOwnMembers()
    {
        var index = HostApiIndex.Describe(typeof(SampleGlobals), typeof(SamplePanel));

        // Threshold is plain data a script reads by name. Nothing else in the walk mentions it.
        index.Should().Contain("- `SampleGlobals`:").And.Contain("Threshold");
    }

    [TestMethod]
    public void Describe_AFlatApiClassWithNoGlobalsIndirection_IsNotEmpty()
    {
        // Regression: this returned "" — silently — for the commonest shape an adopting app has.
        var index = HostApiIndex.Describe(typeof(FlatSampleApi));

        index.Should().Contain("- `FlatSampleApi`: ExposureMs, StartInspection");
    }

    [TestMethod]
    public void Describe_FollowsTheFacadeOfAnAdditionalType()
    {
        var index = HostApiIndex.Describe(typeof(SampleGlobals), typeof(SamplePanel));

        index.Should().Contain("- `SamplePanel.API`: SetValue");
    }

    [TestMethod]
    public void Describe_WithACustomFacadeName_FollowsThatInstead()
    {
        // The "API" convention belongs to the donating repo, not to every adopter.
        var index = HostApiIndex.Describe(typeof(SampleGlobals), facadePropertyName: "Nothing", typeof(SamplePanel));

        index.Should().NotContain("SetValue");
    }

    [TestMethod]
    public void MemberNames_LeavesOutInheritedMembers()
    {
        var names = HostApiIndex.MemberNames(typeof(SamplePanel)).ToArray();

        names.Should().Contain("API");
        names.Should().NotContain("Refresh").And.NotContain("Visible").And.NotContain("Name");
    }

    [TestMethod]
    public void MemberNames_LeavesOutTheUniversalObjectMembers()
    {
        var names = HostApiIndex.MemberNames(typeof(SampleApi)).ToArray();

        names.Should().NotContain("ToString").And.NotContain("GetHashCode").And.NotContain("Equals");
    }

    [TestMethod]
    public void ScriptFacingTypes_ReturnsExactlyWhatDescribeLists()
    {
        // The invariant the whole design rests on: what the model is told exists and what it can
        // then ask about come from one call, so they cannot drift apart.
        var types = HostApiIndex.ScriptFacingTypes(typeof(SampleGlobals), typeof(SamplePanel));

        types.Should().BeEquivalentTo(
        [
            typeof(SampleGlobals),
            typeof(SampleApi),
            typeof(SamplePanel),
            typeof(SamplePanel.SamplePanelApi),
        ]);
    }

    [TestMethod]
    public void Describe_WithANullRootType_Throws()
    {
        var act = () => HostApiIndex.Describe(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
