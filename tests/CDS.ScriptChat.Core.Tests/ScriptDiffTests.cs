using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

[TestClass]
[TestCategory("Diff")]
public sealed class ScriptDiffTests
{
    [TestMethod]
    public void Compute_IdenticalScripts_ReportsEveryLineUnchanged()
    {
        var diff = ScriptDiff.Compute("one\ntwo", "one\ntwo");

        diff.Should().OnlyContain(l => l.Kind == ScriptDiffLineKind.Unchanged);
        ScriptDiff.HasChanges(diff).Should().BeFalse();
    }

    [TestMethod]
    public void Compute_ScriptsDifferingOnlyInLineEndings_ReportsNoChange()
    {
        var diff = ScriptDiff.Compute("one\r\ntwo", "one\ntwo");

        ScriptDiff.HasChanges(diff).Should().BeFalse();
    }

    [TestMethod]
    public void Compute_LineInsertedInTheMiddle_MarksOnlyThatLineAdded()
    {
        var diff = ScriptDiff.Compute("one\nthree", "one\ntwo\nthree");

        diff.Select(l => (l.Kind, l.Text)).Should().Equal(
            (ScriptDiffLineKind.Unchanged, "one"),
            (ScriptDiffLineKind.Added, "two"),
            (ScriptDiffLineKind.Unchanged, "three"));
    }

    [TestMethod]
    public void Compute_LineDeleted_MarksOnlyThatLineRemoved()
    {
        var diff = ScriptDiff.Compute("one\ntwo\nthree", "one\nthree");

        diff.Select(l => (l.Kind, l.Text)).Should().Equal(
            (ScriptDiffLineKind.Unchanged, "one"),
            (ScriptDiffLineKind.Removed, "two"),
            (ScriptDiffLineKind.Unchanged, "three"));
    }

    [TestMethod]
    public void Compute_LineChanged_ReportsItAsARemovalAndAnAddition()
    {
        var diff = ScriptDiff.Compute("var x = 1;", "var x = 2;");

        diff.Should().HaveCount(2);
        diff.Should().Contain(l => l.Kind == ScriptDiffLineKind.Removed && l.Text == "var x = 1;");
        diff.Should().Contain(l => l.Kind == ScriptDiffLineKind.Added && l.Text == "var x = 2;");
    }

    [TestMethod]
    public void Compute_EmptyOriginal_MarksEverythingAdded()
    {
        var diff = ScriptDiff.Compute("", "one\ntwo");

        diff.Where(l => l.Text.Length > 0).Should()
            .OnlyContain(l => l.Kind == ScriptDiffLineKind.Added);
        ScriptDiff.HasChanges(diff).Should().BeTrue();
    }

    [TestMethod]
    public void Compute_ProposalEmptiesTheScript_MarksEverythingRemoved()
    {
        var diff = ScriptDiff.Compute("one\ntwo", "");

        diff.Where(l => l.Text.Length > 0).Should()
            .OnlyContain(l => l.Kind == ScriptDiffLineKind.Removed);
    }

    [TestMethod]
    public void Compute_UnchangedLinesPreservedInOrder_ReconstructsTheProposal()
    {
        const string original = "using System;\n\nvar a = 1;\nvar b = 2;\nPrint(a);";
        const string proposed = "using System;\n\nvar a = 1;\nvar c = 3;\nPrint(a);\nPrint(c);";

        var diff = ScriptDiff.Compute(original, proposed);

        // Everything the proposal keeps or adds, in order, must be exactly the proposal.
        var rebuilt = diff
            .Where(l => l.Kind != ScriptDiffLineKind.Removed)
            .Select(l => l.Text);

        string.Join("\n", rebuilt).Should().Be(proposed);
    }

    [TestMethod]
    public void Compute_UnchangedAndRemovedLinesInOrder_ReconstructTheOriginal()
    {
        const string original = "one\ntwo\nthree\nfour";
        const string proposed = "one\nthree\nfive";

        var diff = ScriptDiff.Compute(original, proposed);

        var rebuilt = diff
            .Where(l => l.Kind != ScriptDiffLineKind.Added)
            .Select(l => l.Text);

        string.Join("\n", rebuilt).Should().Be(original);
    }

    [TestMethod]
    public void Compute_ScriptLargerThanTheLineDiffCeiling_FallsBackToWholesaleReplacement()
    {
        var original = string.Join("\n", Enumerable.Range(0, 2_500).Select(i => $"line {i}"));
        var proposed = string.Join("\n", Enumerable.Range(0, 2_500).Select(i => $"line {i}"));

        var diff = ScriptDiff.Compute(original, proposed);

        // Identical content, but past the ceiling it is reported as a full replacement rather
        // than paying for the quadratic comparison.
        diff.Should().NotContain(l => l.Kind == ScriptDiffLineKind.Unchanged);
        ScriptDiff.HasChanges(diff).Should().BeTrue();
    }

    [TestMethod]
    public void Compute_NullArgument_Throws()
    {
        var act = () => ScriptDiff.Compute(null!, "x");

        act.Should().Throw<ArgumentNullException>();
    }
}
