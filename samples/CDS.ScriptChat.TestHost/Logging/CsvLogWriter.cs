using System.Globalization;
using System.Text;

namespace CDS.ScriptChat.TestHost.Logging;

/// <summary>
/// Appends rows to one CSV file, safely for concurrent callers.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="CsvLoggerProvider"/> so the formatting rules — quoting, escaping,
/// and spreadsheet-formula neutering — can be exercised without standing up a logger factory.
/// </para>
/// <para>
/// Writes are flushed immediately. A log that loses its last few rows is worth very little when
/// the thing being diagnosed is a hang or a crash, and this file is small enough that the cost
/// does not matter.
/// </para>
/// </remarks>
internal sealed class CsvLogWriter : IDisposable
{
    /// <summary>The column order, written as the first row of every file.</summary>
    public static readonly string[] Columns =
    [
        "Timestamp",
        "Level",
        "Category",
        "EventId",
        "EventName",
        "ThreadId",
        "Scopes",
        "Message",
        "Exception",
    ];

    /// <summary>
    /// Characters that make a spreadsheet treat a cell as a formula. Model output is arbitrary
    /// text and can easily start with one, so such a cell is prefixed with an apostrophe.
    /// </summary>
    private static readonly char[] s_formulaLeadIns = ['=', '+', '-', '@', '\t', '\r'];

    private readonly Lock _gate = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    private CsvLogWriter(StreamWriter writer)
    {
        _writer = writer;
    }

    /// <summary>
    /// Creates or reopens the file at <paramref name="filePath"/>, writing the header row if it
    /// is new.
    /// </summary>
    /// <param name="filePath">Where to write. Missing directories are created.</param>
    /// <returns>A writer appending to that file.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or whitespace.</exception>
    public static CsvLogWriter Create(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var isNew = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;

        var writer = new StreamWriter(
            new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            AutoFlush = true,
        };

        var csvWriter = new CsvLogWriter(writer);
        if (isNew)
        {
            csvWriter.WriteRow(Columns);
        }

        return csvWriter;
    }

    /// <summary>
    /// Formats one field as a CSV cell: quoted when it has to be, with embedded quotes doubled,
    /// and with a leading apostrophe when a spreadsheet would otherwise read it as a formula.
    /// </summary>
    /// <param name="value">The raw field value. <see langword="null"/> becomes an empty cell.</param>
    /// <returns>The cell as it should appear in the file.</returns>
    public static string EscapeField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var field = value.IndexOfAny(s_formulaLeadIns) == 0 ? "'" + value : value;

        if (field.AsSpan().IndexOfAny(",\"\r\n") < 0)
        {
            return field;
        }

        return string.Concat("\"", field.Replace("\"", "\"\"", StringComparison.Ordinal), "\"");
    }

    /// <summary>
    /// Appends one row.
    /// </summary>
    /// <param name="fields">The cells, in <see cref="Columns"/> order.</param>
    public void WriteRow(IReadOnlyList<string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var line = new StringBuilder();
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                line.Append(',');
            }

            line.Append(EscapeField(fields[i]));
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _writer.WriteLine(line.ToString());
            }
            catch (IOException ex)
            {
                // Swallowed deliberately, and only for this one case: a log file that has become
                // unwritable — the disk filled, someone has it open exclusively — must not take
                // the host app down with it. Nothing else here catches anything.
                System.Diagnostics.Debug.WriteLine($"CSV log write failed: {ex.Message}");
            }
        }
    }

    /// <summary>Formats a timestamp the way every row records it: ISO 8601, local, with offset.</summary>
    /// <param name="timestamp">The moment to format.</param>
    /// <returns>The timestamp as it appears in the file.</returns>
    public static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Dispose();
        }
    }
}
