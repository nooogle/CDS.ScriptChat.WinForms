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
- Logging is MEL via `ILoggerFactory`. Prompts, responses, proposed scripts and
  symbol signatures go at `Trace` and nowhere else; `Information` and above carry
  structure only (lengths, counts, timings, IDs). API keys at no level. New log
  messages go in `ScriptChatLog` / `ScriptChatWinFormsLog`, not inline. (D16)
- **`Trace` — or any other logging, telemetry, cache, crash report, or
  diagnostic artifact that could carry a user's prompt, script, response, or
  API key — must never be the default in this repo or anything it ships.**
  Content-bearing capture is opt-in only, deliberately triggered by whoever
  is diagnosing something, never silently on. Before adding any new logging,
  caching, or diagnostics — here or in any future feature — check it against
  this rule, not just against D16's *where*. (D17)
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