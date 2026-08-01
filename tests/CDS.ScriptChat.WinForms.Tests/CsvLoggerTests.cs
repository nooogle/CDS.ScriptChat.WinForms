using AwesomeAssertions;

using CDS.ScriptChat.TestHost.Logging;

using Microsoft.Extensions.Logging;

namespace CDS.ScriptChat.WinForms.Tests;

/// <summary>
/// Covers the sample host's CSV logging provider. A log that quietly mangles its own rows is
/// worse than no log, so the escaping rules are pinned here.
/// </summary>
[TestClass]
[TestCategory("Logging")]
public sealed class CsvLoggerTests
{
    private string _directory = string.Empty;

    [TestInitialize]
    public void CreateTempDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "scriptchat-csv-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveTempDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void EscapeField_PlainText_IsLeftAlone()
    {
        CsvLogWriter.EscapeField("Turn 3 completed").Should().Be("Turn 3 completed");
    }

    [TestMethod]
    public void EscapeField_Null_BecomesAnEmptyCell()
    {
        CsvLogWriter.EscapeField(null).Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("a,b", "\"a,b\"")]
    [DataRow("line\nbreak", "\"line\nbreak\"")]
    [DataRow("carriage\rreturn", "\"carriage\rreturn\"")]
    public void EscapeField_ContainsADelimiter_IsQuoted(string input, string expected)
    {
        CsvLogWriter.EscapeField(input).Should().Be(expected);
    }

    [TestMethod]
    public void EscapeField_ContainsQuotes_DoublesThemInsideQuotes()
    {
        CsvLogWriter.EscapeField("say \"hello\"").Should().Be("\"say \"\"hello\"\"\"");
    }

    [TestMethod]
    [DataRow("=cmd|'/c calc'!A1")]
    [DataRow("+1234")]
    [DataRow("-SUM(A1)")]
    [DataRow("@import")]
    public void EscapeField_LooksLikeASpreadsheetFormula_IsNeutered(string input)
    {
        // Model output is arbitrary text, and this file is meant to be opened in a spreadsheet.
        CsvLogWriter.EscapeField(input).Should().StartWith("'");
    }

    [TestMethod]
    public void EscapeField_FormulaLeadInLaterInTheField_IsLeftAlone()
    {
        CsvLogWriter.EscapeField("x = 1").Should().Be("x = 1");
    }

    [TestMethod]
    public void Create_NewFile_WritesTheHeaderRowFirst()
    {
        var path = Path.Combine(_directory, "log.csv");

        using (CsvLogWriter.Create(path))
        {
        }

        File.ReadAllLines(path)[0].Should().Be(string.Join(',', CsvLogWriter.Columns));
    }

    [TestMethod]
    public void Create_MissingDirectory_IsCreated()
    {
        var path = Path.Combine(_directory, "nested", "deeper", "log.csv");

        using (CsvLogWriter.Create(path))
        {
        }

        File.Exists(path).Should().BeTrue();
    }

    [TestMethod]
    public void Provider_LoggedMessage_WritesOneRowWithTheEventIdAndCategory()
    {
        var path = Path.Combine(_directory, "log.csv");

        WriteThroughProvider(path, logger =>
            logger("Some.Category").Log(
                LogLevel.Warning, new EventId(4242, "SomethingHappened"), "the message", null, (s, _) => s));

        var row = File.ReadAllLines(path)[1];
        row.Should().Contain("Warning")
            .And.Contain("Some.Category")
            .And.Contain("4242")
            .And.Contain("SomethingHappened")
            .And.Contain("the message");
    }

    [TestMethod]
    public void Provider_MessageContainingCommasAndNewlines_StaysOnOneParsableRow()
    {
        var path = Path.Combine(_directory, "log.csv");

        // What a Trace-level script dump actually looks like.
        WriteThroughProvider(path, logger =>
            logger("Script").LogTrace("Script: {Script}", "var a = 1;\r\nvar b = \"two, three\";"));

        var text = File.ReadAllText(path);
        text.Should().Contain("\"\"two, three\"\"");

        // The embedded newline must live inside a quoted cell, so the whole entry is one record.
        CountUnquotedLineBreaks(text).Should().Be(2, "the header and the single log record each end one line");
    }

    [TestMethod]
    public void Provider_LoggedException_KeepsTheTypeAndMessage()
    {
        var path = Path.Combine(_directory, "log.csv");

        WriteThroughProvider(path, logger =>
            logger("Failures").LogError(new InvalidOperationException("the provider said no"), "Turn failed."));

        File.ReadAllText(path).Should()
            .Contain("InvalidOperationException").And.Contain("the provider said no");
    }

    [TestMethod]
    public void Provider_Disposed_ReleasesTheFile()
    {
        // The provider owns the file handle and nothing else disposes it, so a caller that
        // forgets leaves the log locked for the life of the process.
        var path = Path.Combine(_directory, "log.csv");

        WriteThroughProvider(path, logger => logger("Anything").LogInformation("something"));

        var act = () => File.Delete(path);

        act.Should().NotThrow();
    }

    /// <summary>
    /// Runs <paramref name="write"/> against a factory backed by a CSV provider at
    /// <paramref name="path"/>, disposing both afterwards so the file can be read back.
    /// </summary>
    private static void WriteThroughProvider(string path, Action<Func<string, ILogger>> write)
    {
        using (var provider = new CsvLoggerProvider(path))
        {
            using var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(provider);
            });

            write(factory.CreateLogger);
        }
    }

    /// <summary>
    /// Counts the line breaks that actually terminate a CSV record, ignoring those inside a
    /// quoted cell.
    /// </summary>
    private static int CountUnquotedLineBreaks(string text)
    {
        var inQuotes = false;
        var count = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (text[i] == '\n' && !inQuotes)
            {
                count++;
            }
        }

        return count;
    }
}
