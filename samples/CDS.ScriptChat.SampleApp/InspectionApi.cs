namespace CDS.ScriptChat.SampleApp;

/// <summary>
/// What a script can do to the inspection station: measure the parts on the fixture, record a
/// verdict for each, and write to the operator's output pane.
/// </summary>
/// <remarks>
/// This is the sample's whole domain API, and it is all the assistant is told about. Note that
/// every member carries an XML documentation comment — those comments are what
/// <c>lookup_symbol</c> hands back, so they are the difference between an assistant that writes
/// correct scripts and one that guesses plausibly.
/// </remarks>
public sealed class InspectionApi(Action<string> writeOutput)
{
    private readonly Dictionary<string, double> _readings = new(StringComparer.Ordinal)
    {
        ["A-100"] = 12.02,
        ["A-101"] = 11.96,
        ["B-204"] = 12.51,
        ["B-205"] = 11.48,
    };

    /// <summary>Gets the names of the parts currently loaded on the fixture, in fixture order.</summary>
    public IReadOnlyList<string> Parts => [.. _readings.Keys];

    /// <summary>Gets the number of parts recorded as passing since the script started.</summary>
    public int PassCount { get; private set; }

    /// <summary>Gets the number of parts recorded as failing since the script started.</summary>
    public int FailCount { get; private set; }

    /// <summary>
    /// Measures one part and returns its dimension in millimetres.
    /// </summary>
    /// <param name="partName">A part name from <see cref="Parts"/>.</param>
    /// <returns>The measured dimension, in millimetres.</returns>
    /// <exception cref="ArgumentException">No part on the fixture has that name.</exception>
    public double Measure(string partName)
    {
        if (!_readings.TryGetValue(partName, out var reading))
        {
            throw new ArgumentException($"No part called '{partName}' is on the fixture.", nameof(partName));
        }

        return reading;
    }

    /// <summary>
    /// Records a pass or fail verdict against a part, and reports it to the operator.
    /// </summary>
    /// <param name="partName">The part the verdict applies to.</param>
    /// <param name="passed"><see langword="true"/> for a pass, <see langword="false"/> for a fail.</param>
    public void Record(string partName, bool passed)
    {
        if (passed)
        {
            PassCount++;
        }
        else
        {
            FailCount++;
        }

        Log($"{partName}: {(passed ? "PASS" : "FAIL")}");
    }

    /// <summary>
    /// Writes a line to the operator's output pane.
    /// </summary>
    /// <param name="message">What to write.</param>
    public void Log(string message) => writeOutput(message);
}
