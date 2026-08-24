These scripts run on a **widget inspection station**. Each script is executed once per job, with
the parts already loaded on the fixture. A script measures the parts, decides whether each is
within tolerance, and records a verdict for it.

The globals are available unqualified: `API` reaches the station, and `UpperLimitMm` /
`LowerLimitMm` are the tolerance the current job is running to. A script returns nothing — its
effect is the verdicts it records and the lines it logs.

Two conventions worth keeping to when proposing a change:

- Record a verdict for **every** part on the fixture. A part measured but not recorded is a hole
  in the traceability record, which is worse than a fail.
- Read the tolerance from `UpperLimitMm` / `LowerLimitMm` rather than writing numbers into the
  script. The limits change per job; the script should not.
