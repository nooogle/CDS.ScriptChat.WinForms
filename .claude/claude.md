# CLAUDE.md

## Project

CDS.ScriptChat — a reusable script+chat panel library for .NET/WinForms host
apps (Fable, the OpenCvSharp Playground, potentially GroundTruth). Not
OpenCvSharp-specific; provider-agnostic (Claude/OpenAI/Grok via
`Microsoft.Extensions.AI.IChatClient`).

## Read first

`cds.scriptchat.design.md` (repo root) — architecture, decision log (D1–D17),
use cases, and current milestone scope. Read it before writing code. Don't
duplicate its content here; if a rule from there matters for every session,
add a one-line pointer below instead of copying it in full.

## Hard rules

- All WinForms UI is standard Designer classes (`.cs`/`.Designer.cs`/`.resx`,
  `InitializeComponent()`), editable in the VS 2026 Designer. No code-only
  layout. (D14)
- BYOK only. Never hardcode, log, or transmit an API key anywhere except the
  provider SDK call itself. No telemetry on prompt/response content. (D3)
- Logging is MEL via `ILoggerFactory`. Every message, at every level, carries
  only structure (lengths, counts, timings, IDs). API keys at no level. New log
  messages go in `ScriptChatLog` / `ScriptChatWinFormsLog`, not inline. (D16)
- **No content-bearing log message, cache, telemetry report, or diagnostic
  artifact may exist anywhere in this library or its samples, at any level —
  removed outright, never just gated behind an opt-in flag or a default.**
  Prompts, scripts, responses, summaries, symbol signatures, the orientation
  blurb, and API keys must never be logged, cached, or transmitted anywhere
  except the direct provider SDK call itself. An opt-in default is not a
  guarantee — a bad actor, a misconfigured host, or another library sharing
  the same logging pipeline can reconfigure it without touching this
  library's code. `ScriptChatSession` enforces this at a hard boundary
  (`TraceSuppressingLoggerFactory`) that also blocks `Microsoft.Extensions.AI`'s
  own `Trace`-level content logging, not just this library's own messages.
  Before adding any new logging, caching, or diagnostics — here or in any
  future feature — check it against this rule. (D17)
- Proposed code edits arrive only via the `propose_script_edit` tool call and
  are shown as a diff, never auto-applied, never parsed out of markdown
  fences. (D5)
- Nothing use-case-specific ships in the library: no Roslyn, no
  CDS.CSharpScripting2, no Scintilla, no OpenCvSharp. Symbol lookup is an
  interface the host implements; the script buffer is reached through
  host-supplied delegates. (D15)
- One milestone per session. Stay inside the current milestone's scope as
  stated in cds.scriptchat.design.md — don't implement later-milestone use
  cases opportunistically.
- British English in comments and docs.

## When in doubt

Flag disagreement rather than silently deviating from a decision already made
in the design doc's Decision Log.