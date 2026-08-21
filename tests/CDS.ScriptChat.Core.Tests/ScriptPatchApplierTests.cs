using AwesomeAssertions;

namespace CDS.ScriptChat.Core.Tests;

[TestClass]
[TestCategory("ScriptPatchApplier")]
public sealed class ScriptPatchApplierTests
{
    [TestMethod]
    public void Apply_NoHunks_ReturnsTheScriptUnchanged()
    {
        var result = ScriptPatchApplier.Apply("var x = 1;", []);

        result.Should().Be("var x = 1;");
    }

    [TestMethod]
    public void Apply_SingleHunk_ReplacesTheMatchedText()
    {
        var result = ScriptPatchApplier.Apply(
            "var x = 1;\nvar y = 2;",
            [new ScriptEditHunk("var x = 1;", "var x = 10;")]);

        result.Should().Be("var x = 10;\nvar y = 2;");
    }

    [TestMethod]
    public void Apply_SeveralHunks_AreAppliedInOrder()
    {
        var result = ScriptPatchApplier.Apply(
            "var x = 1;\nvar y = 2;\nvar z = 3;",
            [
                new ScriptEditHunk("var x = 1;", "var x = 10;"),
                new ScriptEditHunk("var z = 3;", "var z = 30;"),
            ]);

        result.Should().Be("var x = 10;\nvar y = 2;\nvar z = 30;");
    }

    [TestMethod]
    public void Apply_LaterHunkTargetsTextIntroducedByAnEarlierHunk_Applies()
    {
        // Each hunk is matched against the result of the previous one, not the original script.
        var result = ScriptPatchApplier.Apply(
            "var x = 1;",
            [
                new ScriptEditHunk("var x = 1;", "var x = 10;"),
                new ScriptEditHunk("var x = 10;", "var x = 100;"),
            ]);

        result.Should().Be("var x = 100;");
    }

    [TestMethod]
    public void Apply_OldTextNotPresent_Throws()
    {
        var act = () => ScriptPatchApplier.Apply(
            "var x = 1;",
            [new ScriptEditHunk("var x = 2;", "var x = 20;")]);

        act.Should().Throw<ScriptPatchApplyException>()
            .Which.HunkIndex.Should().Be(0);
    }

    [TestMethod]
    public void Apply_OldTextAmbiguous_Throws()
    {
        var act = () => ScriptPatchApplier.Apply(
            "var x = 1;\nvar x = 1;",
            [new ScriptEditHunk("var x = 1;", "var x = 2;")]);

        act.Should().Throw<ScriptPatchApplyException>()
            .Which.HunkIndex.Should().Be(0);
    }

    [TestMethod]
    public void Apply_SecondHunkFails_ReportsItsOwnIndexNotTheFirsts()
    {
        var act = () => ScriptPatchApplier.Apply(
            "var x = 1;",
            [
                new ScriptEditHunk("var x = 1;", "var x = 10;"),
                new ScriptEditHunk("var y = 99;", "var y = 100;"),
            ]);

        act.Should().Throw<ScriptPatchApplyException>()
            .Which.HunkIndex.Should().Be(1);
    }

    [TestMethod]
    public void Apply_NullScript_Throws()
    {
        var act = () => ScriptPatchApplier.Apply(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Apply_NullHunks_Throws()
    {
        var act = () => ScriptPatchApplier.Apply("var x = 1;", null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
