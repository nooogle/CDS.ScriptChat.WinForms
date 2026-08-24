# CDS.ScriptChat — Design & Kickoff

## Purpose

A reusable "script + chat" capability: a panel that lets a user hold a conversation
with an AI assistant about a script open in an editor, where the assistant can
answer questions and propose code edits. Not specific to OpenCvSharp or to any one
host app — the same library should work for Fable's processing scripts, a future
GroundTruth scripting surface, or the OpenCvSharp Playground app, differing only in
which host app supplies the API context and symbol lookups.

This document was the kickoff input for the Claude Code session that built Milestone
1; it now doubles as the running design record. Milestone 1 is **complete** — see
Status below — and later milestones are named but not detailed until they're
scoped, in keeping with the one-milestone-at-a-time approach used for GroundTruth
and Fable.

## Status

- **Milestone 1** (UC1, UC3, UC4, UC6) — **complete**. All build-order steps 1–6
  done, acceptance criteria met, tests passing. Packages published as `1.0.0`
  (see `todo.packaging.md`).
- **Outstanding from Milestone 1** — both items now closed:
  - ~~The pre-release logging checklist... test host still runs at `Trace`~~ —
    closed 2026-08-14, then hardened the same day. See D17 and "Content-bearing
    logging — removed, not gated" under Logging: content-bearing logging isn't
    just off by default any more, the capability has been removed from the
    library entirely, including the equivalent risk inside
    `Microsoft.Extensions.AI`'s own function-invocation logging.
  - ~~`ScriptChatClientFactory` throws `NotSupportedException` for `OpenAI`~~ —
    closed by milestone 2. `Grok` still throws; remains deferred, not a gap.
- **Milestone 2** (UC2, OpenAI wiring) — **complete**, including live verification
  (66 Core + 77 WinForms, solution builds clean, zero warnings). `ScriptChatClientFactory`
  wires `Microsoft.Extensions.AI.OpenAI` for real; `ScriptChatSession` rewrites
  the frozen `propose_script_edit` tool-result on accept/reject so a later
  turn's history matches what actually happened to the script (UC2).
  - **8-angle code review run against the diff** (correctness, efficiency, reuse,
    simplification, altitude, CLAUDE.md conventions, removed-behaviour, cross-file
    tracing). Three independent angles converged on the same root issue — the
    original `FindProposalResultContent` re-derived "last call wins" by scanning
    `response.Messages` for the tool name after the fact, instead of using the
    `CallId` already known at the moment `propose_script_edit` was called — and
    `SetEditDisposition` failed silently if that re-derivation ever missed.
    Fixed: `ProposeScriptEdit` now captures its own `CallId` via
    `FunctionInvokingChatClient.CurrentContext` at invocation time; the lookup is
    a single-pass match on a known ID instead of a two-pass scan-and-guess; and a
    missed reconciliation now logs `EditDispositionReconciliationMissed`
    (Warning, event 1033) instead of failing silently.
  - A genuine thread-safety gap was also found and fixed: `SendAsync` awaits the
    provider with `ConfigureAwait(false)`, so `AddTurn` can resume on a
    thread-pool thread while a host's UI thread calls `SetEditDisposition` for an
    *earlier*, still-pending turn — an unguarded `List<T>` race (pre-existing for
    `_turns`, widened by this milestone's new `_turnProposalResults` list). Fixed
    with a single `Lock` guarding the three turn-parallel lists.
  - A minor closure-capture nit (the OpenAI `ConfigureOptions` delegate closed
    over the whole `ScriptChatClientOptions`, including the API key, instead of
    just the one `int` it needed) was also fixed, per D3's spirit.
  - **New test coverage added post-review**: `OpenAIWireFormatTests` (a fake
    `HttpMessageHandler`-backed transport proves `MaxOutputTokens` actually
    reaches the real OpenAI SDK's request body, that an explicit per-call
    ceiling — e.g. the settings panel's cheap "test connection" probe — isn't
    overridden by the configured default, and that a real tool-call response
    parses correctly); `ScriptChatSessionOpenAIIntegrationTests` (the big one —
    a full `ScriptChatSession` wired to a fake-transport-backed real OpenAI
    client, proving the entire `CallId` capture → wire round-trip → disposition
    rewrite chain works end to end, not just against the hand-rolled
    `FakeChatClient` used elsewhere); OpenAI-parameterised guard-clause tests;
    a panel-level "`Configure` with OpenAI succeeds" test.
  - **Verified live against the real OpenAI API** (2026-08-14, Jon, own key —
    BYOK, D3). Everything above was covered by tests against a fake key and a
    fake HTTP transport only up to this point; the automated suite still never
    touches a real key, by design.

## Non-goals (v1)

- Multi-file / multi-script context in a single conversation
- Automatic application of proposed edits without explicit user accept
- Telemetry or analytics on prompt/response content
- Streaming token-by-token display (deferred — see D9 and UC7)
- Any bundled or shared API key — this is BYOK only, always

## Architecture

Three pieces, split so that no AI/SDK dependency ever leans on WinForms, no
WinForms dependency ever leans on a specific AI provider, and nothing in the
library leans on any particular host app's scripting stack (D15):

**1. `CDS.ScriptChat.Core`** (no WinForms, no Roslyn, no editor)
- `ScriptChatSession` — holds conversation state, sends turns, interprets results.
  Built on `Microsoft.Extensions.AI.IChatClient`, provider-agnostic.
- `ScriptChatClientFactory` — the only place that knows Claude/OpenAI/Grok exist.
  Constructs an `IChatClient` per provider; everything else consumes the interface.
- `ISymbolLookupProvider` — abstraction the host app implements to answer
  "what does this symbol look like" (see below). Core defines the interface and a
  no-op implementation; it never ships a real one.
- `AssistantTurnResult`, `ChatTurn` — result/display types (see Data Model).

**2. `CDS.ScriptChat.WinForms`**
- The panel `UserControl`: single scrolling transcript (see UI section), input
  box, send button, diff/accept UI for proposed edits.
- Reads and writes the script through host-supplied delegates, so the library
  takes no dependency on any specific editor control (D15).
- Key storage (Windows DPAPI) and a settings sub-panel: provider/model choice,
  key entry, test-connection button.
- Built as standard Designer-generated classes throughout (D14) — every
  control here must be manually editable in the WinForms Designer, not
  assembled programmatically in a constructor.

**3. Test host app**
- Minimal editor + panel wiring in the same solution, playing the same role
  for `CDS.ScriptChat` that Fable plays for the CV pipeline libraries: a place to
  develop and verify the panel in isolation before other apps consume it. This is
  also where a concrete `ISymbolLookupProvider` gets written, as a worked example
  of what a consuming app must supply.

### `lookup_symbol` tool

```
lookup_symbol(symbolName: string, containingType: string?) -> {
  signature: string,
  xmlDocSummary: string?,
  namespace: string,
  overloads: string[]
}
```

**The library does not implement this — the host app does** (D15). `CDS.ScriptChat`
defines `ISymbolLookupProvider`, exposes it to the model as the `lookup_symbol`
tool, and ships a no-op implementation so the tool-calling path works before a
host wires anything up. Where the answers come from is entirely the host's
choice: a Roslyn `SemanticModel`, a reflection pass over loaded assemblies, a
hand-maintained table, or a remote service.

Returning `null` is an ordinary outcome meaning "not found", not an error.

**v1 scope** (D11): whatever the host can answer cheaply and synchronously for a
single named symbol. That is deliberately narrower than what VS/VS Code's
integrated chat can do (workspace-wide symbol search, "find references",
related-file awareness); `ISymbolLookupProvider` is shaped so those can be added
as new methods later without changing v1 callers, rather than trying to match
IDE-chat parity on day one.

### Host app context

Each host app supplies a short curated orientation blurb (two or three
sentences: what kind of scripts these are, the top-level shape of the API, e.g.
"scripts use a pull-based two-script model; components are fetched via
`GetWorkspaceComponent<T>`; this app wraps OpenCvSharp4"). This goes in the
system prompt alongside the `lookup_symbol` tool — orientation up front, detail
on demand. Per D12, `CDS.ScriptChat` checks for a conventional text/YAML file
first, falling back to a host-supplied property if no file is present — a host
app can use whichever fits its own project layout better.

## Decision Log

| # | Decision |
|---|----------|
| D1 | `CDS.ScriptChat` is a standalone library/solution, not folded into CDS.CSharpScripting2, keeping AI/SDK packages out of the scripting library entirely. *(Superseded in part by D15: the library takes no dependency on CDS.CSharpScripting2 at all, not even via an adapter.)* |
| D2 | Provider abstraction is `Microsoft.Extensions.AI.IChatClient`, not a bespoke interface. Provider-specific code lives only in `ScriptChatClientFactory`. |
| D3 | BYOK, client-side only, no proxy/backend, no telemetry on prompt or response content. Feature is fully inert with no key configured. |
| D4 | Library knowledge is tool-based and on-demand (`lookup_symbol`, implemented by the host), not front-loaded API docs in the system prompt. Keeps the prompt small and the answers accurate regardless of which library the script uses. |
| D5 | Code changes arrive via a structured tool call (`propose_script_edit`), shown as a diff, and require explicit accept before touching the editor buffer. Never parsed from free-text code fences. |
| D6 | Single-panel UI: one scrolling transcript interleaving user input and AI output in order, not separate input/output panes. |
| D7 | ~~Direct dependency on Scintilla, no `IScriptSurface` abstraction.~~ **Superseded by D15.** The library must not reference Scintilla; the panel reads and writes the script through host-supplied delegates. This is not the `IScriptSurface` abstraction D7 rejected — there is no editor interface to implement, just a getter and a setter the host wires to whatever it uses. |
| D8 | This design doc is a standalone markdown file, independent of any one host app's `CLAUDE.md`, since the library is meant to be consumed by multiple apps. |
| D9 | Streaming is deferred past v1. Tool-call proposals need the full response to parse reliably, and mixing streamed prose with a non-streamed tool result added complexity not worth it for milestone 1. |
| D10 | Switching provider or model mid-session resets the conversation. No cross-provider `ChatMessage` history carryover in v1. |
| D11 | `lookup_symbol` v1 stays scoped to resolving a single named symbol, not full workspace/project-wide search. `ISymbolLookupProvider` is designed as an extension point so a later milestone can grow it toward IDE-chat-level context (references, workspace symbol search, related-file awareness) without breaking v1 callers. |
| D12 | Host-app orientation context supports two sources: a text/YAML file (checked first, if present) or a host-supplied property/delegate as fallback (e.g. `IHostContext.OrientationBlurb`). Either produces the same short string for the system prompt — a host app isn't forced into a file if a property is more natural for it. |
| D13 | Diff granularity is full-script replacement in v1, matching the typical size of scripts in these apps. Line-level diffs are an explicit future milestone, not a v1 concern (see Future Milestones below). *(Superseded by D19: full-script replacement stays as the fallback for large rewrites, but is no longer the only granularity — `propose_script_patch` adds targeted hunks alongside it.)* |
| D14 | Every UI component (panel, settings sub-panel, any dialog) is built as a standard WinForms Designer class — `.cs` / `.Designer.cs` / `.resx` triplet, `InitializeComponent()`, no hand-rolled layout-in-code, no third-party designer-incompatible UI framework. Must open and edit cleanly in the VS 2026 WinForms Designer. Adding transcript items to a container at runtime is data binding, not layout, and is not covered by this rule. |
| D15 | **Nothing use-case-specific ships in the library.** No Roslyn, no CDS.CSharpScripting2, no Scintilla, no OpenCvSharp — no dependency that presumes a particular host app, scripting engine, or editor control. Symbol lookup is an interface the host implements (`ISymbolLookupProvider`); the script buffer is reached through host-supplied delegates. The library ships a no-op symbol provider so the tool-calling path works out of the box, and the test host app carries the worked example of a real one. Supersedes the `CDS.ScriptChat.Roslyn` project in the original architecture and the Scintilla dependency in D7. *(Superseded in part by D22: Roslyn now ships in Core, because "define the abstraction, never implement it" was measured to cost every adopter 500+ lines of identical code. The rest of D15 — no CDS.CSharpScripting2, no Scintilla, no OpenCvSharp, host-supplied script delegates — still holds.)* |
| D16 | **Logging is standard `Microsoft.Extensions.Logging` throughout, and carries structure only, never content.** Every component takes an `ILoggerFactory` (not a bare `ILogger`), so the whole `Microsoft.Extensions.AI` pipeline — function invocation and the provider round-trips — is instrumented from one property. Every message, at every level, carries only structure — names, lengths, counts, timings, event IDs, exceptions. No message anywhere logs prompt text, response text, proposed scripts, edit summaries, symbol signatures, or the orientation blurb. API keys appear at no level at all (D3) — only a key's length, which is what distinguishes a truncated paste from a wrong key. *(Superseded in part by D17: an earlier version of this decision logged content at `Trace`; that capability has been removed outright, not just gated.)* See "Logging" below. |
| D17 | **No content-bearing log message, cache, telemetry report, or diagnostic artifact may exist anywhere in this library or its samples, at any level — not gated behind an opt-in, removed outright.** Milestone 2's logging review initially fixed the test host defaulting to `Trace` by making `Trace` an explicit opt-in flag; that was rejected as insufficient (2026-08-14) because an opt-in is still a lever something else can pull — a misconfigured host, or another library sharing the same logging pipeline reconfiguring the same provider to `Trace` — with no code change on this library's part. That includes leaks this library doesn't write itself: `Microsoft.Extensions.AI`'s own `FunctionInvokingChatClient` logs full function arguments and results at `Trace` (the entire proposed script, for `propose_script_edit`), and `LoggingChatClient` logs full message and option content at `Trace` — both independent of anything in `ScriptChatLog`. `ScriptChatSession` closes both by wrapping every `ILoggerFactory` it's given in `TraceSuppressingLoggerFactory`, an internal decorator that reports `Trace` as disabled to every logger it hands out, including to those dependencies, regardless of how the underlying provider is configured. This is a hard boundary enforced by the type system at the one chokepoint every logger in this pipeline passes through, not a convention or a default that could be reconfigured back on. |
| D18 | **The transcript is one continuously-appended `MarkdownTextBox`-derived control, not one `ChatTurnView` `UserControl` per turn; Accept/Reject moved from per-turn inline buttons to a single permanent pair below the transcript.** `ChatTurnView` (per-turn `UserControl`, each hosting its own richedit-backed `MarkdownTextBox` and `RichTextBox` for the diff) inside a `FlowLayoutPanel` with `AutoScroll = true` broke mouse-wheel scrolling: a richedit control consumes `WM_MOUSEWHEEL` unconditionally and never bubbles it to its parent, so hovering over any turn's text — not just the diff box — stalled the outer scroll. A single control sidesteps this entirely, since there is nothing above it to chain to; it needed `CDS.Markdown.Lite`'s `MarkdownTextBox` to grow two capabilities it didn't have (`AppendMarkdown` for rendered prose, `AppendPlainText` for unparsed monospaced diff lines with a background colour), added upstream in `CDS.Markdown` 1.5.5 since nothing else consumed the type yet and the interface was ours to change freely. Consequence: a diff can no longer word-wrap independently from prose (one control, one `WordWrap` setting) — accepted, since Markdown code fences already wrapped the same way in the old per-turn control. Moving Accept/Reject off individual turns required a new invariant the panel enforces: **at most one proposal is ever `PendingReview` at a time** — `SendCurrentInputAsync` now refuses to start a new turn while one is outstanding (previously unconstrained; the model could in principle emit a second proposal before the user acted on the first). The decision bar is enabled exactly while that invariant holds a pending turn, disabled otherwise; deciding it appends a short follow-up line to the transcript rather than rewriting the original diff's caption in place. |
| D19 | **Job 3 — targeted patch edits use anchored find-and-replace hunks (`{OldText, NewText}`), not a unified-diff format or line-number ranges, via a new `propose_script_patch` tool alongside the unchanged `propose_script_edit`.** Checked what Claude Code's `Edit` tool and GitHub Copilot's `replace_string_in_file`/`multi_replace_string_in_file` tools actually do before designing this: both converge on exact-text anchored search/replace, never line numbers or diff-hunk parsing. Adopting the same technique resolves Job 3's hardest open question for free — "reconciliation when the model's line numbers drift" doesn't apply, because there are no line numbers; the only failure modes are a hunk's `OldText` no longer being present, or being ambiguous (matches more than once), and both fail closed with a clear error rather than attempting a fuzzy re-anchor, matching both tools' precedent. `ScriptPatchApplier.Apply` (`CDS.ScriptChat.Core`) implements this and is used twice: once inside `propose_script_patch` itself, to validate against the script the model was shown and let it retry within the same turn on a bad anchor; again on Accept, against a **fresh** read of the buffer via `ScriptTextProvider` rather than the frozen baseline the diff was rendered against — so a hunk that no longer applies (the user edited the buffer while the proposal sat pending) is caught at apply time instead of silently overwriting that edit. `ChatTurn` gained `ProposedHunks` (nullable, mutually exclusive with `ProposedCode`) rather than replacing `ProposedCode`, so a turn proposes at most one kind of edit and existing full-replacement code paths are untouched. Accept/Reject stays all-or-nothing per proposal via the single permanent bar from D18 — a patch can carry several hunks, but they are accepted or rejected together; per-hunk accept/reject was explicitly scoped out for this milestone. |
| D20 | **A tool is offered to the model only when this host can actually answer it.** `lookup_symbol` was advertised unconditionally, including when `SymbolLookup` was the default `NullSymbolLookupProvider` — which resolves nothing. The model was therefore told an accurate API lookup existed, called it, was told "not found" every single time, and spent turns concluding the host's API wasn't real. That is silent degradation sitting on the default path: worse than having no lookup, because a model with no lookup falls back on recall and says so, while a model with a broken one distrusts the host. Fixed by building the tool list from what the host can back up — `lookup_symbol` is included only when `SymbolLookup` is not `NullSymbolLookupProvider` — and by assembling the system prompt from the same condition, so the prompt never instructs the model to lean on a tool it wasn't given. `NullSymbolLookupProvider` is kept, but its meaning changes from "answers nothing" to "there is no lookup here". Establishes the general rule for any future tool: advertise a capability only where it is real. *(An earlier draft of this decision also introduced a `ScriptChatMode` enum with a `ChatOnly` member, for hosts with no script at all. Withdrawn before commit on 2026-08-24 along with the scope that motivated it — see D21.)* |
| D21 | **Scope narrowed, deliberately: this library is for C# script chat and nothing else.** Milestone-5 planning had drifted toward a general in-app AI assistant — chat with no script, settings query and mutation via host-registered tools, MCP transports, data review. Each step was individually defensible and the sum was not: it produced a package named `ScriptChat` whose headline feature was a mode for having no script, and an architecture whose seams were being drawn for use cases with no customer. Rejected on 2026-08-24 in favour of the original premise. The decisive argument was capacity rather than architecture — this is a single-maintainer library, and a design its maintainer cannot hold in their head is a liability whatever its factoring. Consequences: `ScriptChatMode` withdrawn; Jobs 6 (host-registered tools) and 7 (settings chat) parked with their evidence in `todo.features.md` rather than deleted; the `CDS.ScriptChat` name is correct rather than a compromise; and **D15 is superseded by D22**, because with the audience narrowed to hosts that already have a C# scripting engine, the objections to shipping Roslyn no longer apply to anyone actually being served. |
| D22 | **Supersedes D15 for Roslyn specifically: `CDS.ScriptChat.Core` ships a working symbol lookup rather than only the interface for one.** D15's rule — define the abstraction, never implement it — produced exactly the outcome it was meant to prevent. Measured on the real adopter (2026-08-24): the OpenCvSharp Playground writes ~473 lines to use this library (`ScriptChatOrientation.cs` 185, `RoslynSymbolLookupProvider.cs` 86, `MainForm.Chat.cs` wiring 202), on top of ~636 lines of Roslyn tooling it had to build first (`ScriptSymbolLookup` 393, `ScriptApiIndex` 208, `ScriptSymbolInfo` 35). The 86-line adapter exists **verbatim twice** in that one repo — once in the app, once in its demo — and its own doc comment names the problem: *"This adapter is the entire cost of choosing your own chat library."* Every adopter writes the same thing, because there is only one sane implementation: obtain a `Compilation`, resolve, and map a four-property record onto `SymbolLookupResult`. So Core gains `RoslynSymbolResolver`, `RoslynSymbolLookupProvider` and `MetadataCompilation`, taking `Microsoft.CodeAnalysis.CSharp`. `ISymbolLookupProvider` stays public and unchanged — a host with its own engine still implements it, and the shipped provider is simply the answer for everyone else. The three objections in the original D15 note are all answered by D21's narrowed scope rather than waved away: **size** (~10 MB against the 11.90 MB of AI SDKs already shipped) is moot when every consumer is a scripting host that already loads Roslyn; **version diamonds** likewise, since those hosts already pin a Roslyn major via CDS.CSharpScripting2; and **irreversibility** cuts the other way now — adding a dependency is non-breaking, and the status quo is costing every adopter 500+ lines today. D15 still holds for everything else: no CDS.CSharpScripting2, no Scintilla, no OpenCvSharp, and the script buffer is still reached through host-supplied delegates. |

## Logging

Added after milestone 1's first end-to-end run, because a turn that "didn't obviously
work" left nothing behind to diagnose it with. Revised twice since (2026-08-14, see below):
first to stop the test host defaulting to `Trace`, then — on the reasonable objection that a
default is just a lever something else could pull — to remove content-bearing logging from
the library outright rather than gate it.

Message templates and event IDs live in one file per assembly — `ScriptChatLog` in Core,
`ScriptChatWinFormsLog` in WinForms — as source-generated `[LoggerMessage]` methods, so
IDs are stable and a log can be filtered by ID rather than by matching on prose. ID bands:

| Band | Area |
|---|---|
| 1000–1099 | `ScriptChatSession` — turns, tool calls, proposals, reset |
| 1100–1199 | `ScriptChatClientFactory` — client construction and rejected options |
| 1200–1299 | `HostOrientationResolver` — which of D12's two sources won |
| 2000–2099 | `ScriptChatPanel` — configure, send, accept/reject |
| 2100–2199 | `ScriptChatSettingsPanel` — provider changes, apply, connection test |
| 2200–2299 | `DpapiApiKeyStore` — load, save, clear |

Six event IDs (1002, 1011, 1013, 1023, 1031, 1203) were content-bearing `Trace` messages and
have been retired, not reused — a log captured before 2026-08-14 may reference them; nothing
after that date will.

The test host supplies a small CSV `ILoggerProvider` (`samples/…/Logging/`), writing one
file per run under `%LOCALAPPDATA%\CDS.ScriptChat.TestHost\logs\`, with a link to it at
the bottom of the window. It lives in the sample, not the library, per D15 — a consuming
app brings its own logging.

### Content-bearing logging — removed, not gated (2026-08-14)

The test host originally ran at `Trace` unconditionally from milestone 1 onward, meaning
**its log file contained the user's script, their messages, the model's replies, and any
proposed edit** on every run. The first fix made `Trace` an explicit `--trace` opt-in
instead of the default. That was the wrong stopping point: a bad actor — or an intermediate
library sharing the same logging pipeline, or a simple host misconfiguration — can
reconfigure a provider's minimum level without touching this library's code at all, so an
opt-in default is not a guarantee, only a convention. It also wouldn't have been enough on
its own: `Microsoft.Extensions.AI`'s own `FunctionInvokingChatClient` and `LoggingChatClient`
log full function arguments/results and full message/option content at `Trace`,
independently of anything `ScriptChatLog` defines — removing this library's own content
messages would have left that dependency-level leak completely open.

What's actually in place now:

- **Every content-bearing `[LoggerMessage]` this library ever defined has been deleted**, not
  demoted — `SystemPromptContent`, `TurnRequestContent`, `TurnResponseContent`,
  `SymbolLookupContent`, `EditProposalContent`, `OrientationContent`. There is no message left
  anywhere in `ScriptChatLog` or `ScriptChatWinFormsLog` that carries a prompt, script,
  response, summary, symbol signature, or the orientation blurb, at any level.
- **`ScriptChatSession` wraps every `ILoggerFactory` it's given in
  `TraceSuppressingLoggerFactory`** before using it for anything — its own logger, and the
  factory handed to `UseFunctionInvocation`. That wrapper reports `Trace` as disabled to every
  logger it creates, unconditionally, regardless of what the underlying provider's minimum
  level is configured to. This is what closes the `Microsoft.Extensions.AI` dependency-level
  leak: `FunctionInvokingChatClient` and any future Trace-level logging a dependency adds
  simply never sees `Trace` reported as enabled, so it never writes.
- **`UseLogging(...)` was removed from the session's pipeline entirely** — it only ever logged
  at `Trace` (per its own documentation, "not logged at other levels"), so under the wrapper
  above it would have been a permanently inert stage.
- **The test host's `--trace` flag was removed again**, since there is nothing left it could
  meaningfully unlock; keeping it would have been misleading rather than protective. The host
  now simply runs at `Information`, unconditionally.
- **Proven, not just asserted**: `ScriptChatSessionLoggingTests` includes
  `SendAsync_EvenWhenTheUnderlyingProviderAllowsTrace_RecordsNoContent` and
  `SendAsync_ProposesAnEditWithTraceAllowed_DoesNotLeakViaFunctionInvocationLogging` — both run
  a turn against a logger provider explicitly configured to accept `Trace` (standing in for a
  reconfigured pipeline) and assert nothing sensitive is recorded, including via
  `Microsoft.Extensions.AI`'s own internals.

Owner: Jon. Closed 2026-08-14. If a future dependency upgrade or a new feature ever seems to
need content at `Trace` again for diagnosis, that is a decision to bring back to this log,
not a default to quietly re-enable — see D17.

## Use Cases

- **UC1** — Single-turn edit request against the script open in the host app's
  editor (e.g. "add denoising then find contours"). **In scope, milestone 1.**
- **UC2** — Multi-turn conversation refining a script across several requests,
  each accepted edit becoming the new baseline. **In scope, milestone 2.** Most
  of the plumbing already exists — `ScriptChatSession` never clears `_history`
  between turns, and `ScriptTextProvider`/`ScriptTextSetter` mean an accepted
  edit is read fresh on the next send. The actual gap: `propose_script_edit`'s
  tool-result message is frozen as "not applied until they accept it" in
  history forever, so after a **rejected** edit the model's own memory of what
  happened can drift out of step with the script it's shown on a later turn.
- **UC3** — Pure Q&A/explanation turn producing no code change (e.g. "why did
  the contour count drop after this change?"). **In scope, milestone 1** — the
  session already distinguishes "text only" from "tool call" responses, so this
  falls out of UC1's plumbing for free.
- **UC4** — On-demand symbol lookup mid-conversation via `lookup_symbol`, answered
  by the host's `ISymbolLookupProvider`. **In scope, milestone 1** — needed for UC1
  to produce reliably correct code.
- **UC5** — Provider/model switch mid-session. **Deferred past milestone 2** —
  resets to a fresh conversation on switch, per D10. Milestone 2 wires up a
  second working provider (OpenAI) but does not scope or test switching to it
  mid-session; that remains for a later milestone.
- **UC6** — BYOK onboarding: enter key, choose provider/model, test connection.
  **In scope, milestone 1.**
- **UC7** — Streaming display. **Explicitly deferred**, see D9.

### Milestone 1 scope

UC1, UC3, UC4, UC6. Single provider wired end-to-end first (Claude, since the
factory and tool-call handling are already proven from earlier prototyping),
then OpenAI/Grok added once the shape is confirmed working. **Complete.**

### Milestone 2 scope

UC2, plus finishing the provider half of D2/Milestone 1: wiring `ScriptChatProvider.OpenAI`
in `ScriptChatClientFactory` so it constructs a real `IChatClient` instead of throwing
`NotSupportedException`. Deliberately **not** in scope:

- **Grok** — stays unwired; nothing has asked for it yet, and doing one provider
  properly (OpenAI, since it has the most obvious host demand) is a cleaner unit of
  work than doing two half-verified ones.
- **UC5 (mid-session provider switch)** — a second working provider makes this
  possible to build, but exercising and testing that workflow is separate work
  left for a later milestone.
- **UC7 (streaming)**, **multi-script host support**, and **local/self-hosted
  provider support** (both raised as parked feedback in `todo.packaging.md`),
  and the two "Future milestones" items below — all explicitly deferred, not to
  be picked up opportunistically during milestone 2.

#### Build order (milestone 2)

1. **OpenAI wiring** — **done.** `CreateOpenAIClient` added alongside
   `CreateClaudeClient` in `ScriptChatClientFactory`, using
   `Microsoft.Extensions.AI.OpenAI` 10.9.0 (`OpenAIClient(...).GetChatClient(modelId).AsIChatClient()`).
   Since that adapter has no constructor overload to bake in a default token
   ceiling the way Anthropic's does, the ceiling is applied via
   `.AsBuilder().ConfigureOptions(o => o.MaxOutputTokens ??= options.MaxOutputTokens)`
   instead. `ScriptChatProvider.Grok` still throws `NotSupportedException`.
2. **Disposition reconciliation in `ScriptChatSession`** — **done, revised after review.**
   A `_turnProposalResults` list (parallel to `_turns`) keeps a reference to each
   turn's `propose_script_edit` `FunctionResultContent`. `SetEditDisposition`
   mutates that object's `Result` in place — it's the same instance already
   sitting in `_history` — to "The user accepted/rejected this edit..." once the
   user decides, replacing the frozen "not applied yet" text. The initial version
   found that content by re-scanning `response.Messages` for the tool name
   after the fact; an 8-angle code review converged on this being fragile
   (re-deriving "last call wins" instead of using the `CallId` already known at
   the point of the call, with a silent failure mode if it ever mismatched), so
   `ProposeScriptEdit` now captures its own `CallId` via
   `FunctionInvokingChatClient.CurrentContext` at invocation time and the lookup
   is a single keyed match. A missed reconciliation now logs a Warning
   (`EditDispositionReconciliationMissed`) instead of failing silently. The
   review also found a genuine `List<T>` thread-safety gap (`SendAsync`'s
   `ConfigureAwait(false)` continuation can run `AddTurn` on a thread-pool
   thread concurrently with the host's UI thread calling `SetEditDisposition`
   for an earlier turn) — fixed with a `Lock` around the three turn-parallel lists.
3. **Verification** — **done**, including a real-pipeline check the initial pass
   didn't have: `ScriptChatSessionOpenAIIntegrationTests` builds a full
   `ScriptChatSession` around a real OpenAI SDK client pointed at a fake HTTP
   transport, proving the `CallId` capture → wire round-trip → disposition
   rewrite chain works end to end, not just against the hand-rolled
   `FakeChatClient` used elsewhere. 65 Core + 77 WinForms tests pass, solution
   builds clean with zero warnings. **Live smoke test against the real OpenAI
   API: done** (2026-08-14, Jon, own key).

#### Acceptance criteria (milestone 2)

- A configured OpenAI API key and model can complete a real UC1-style turn
  (text-only answer, and a turn that proposes an edit shown as a diff) —
  matching Claude's behaviour, not a stub. **Met** — covered by wire-format
  tests against a fake transport (`OpenAIWireFormatTests`) and confirmed
  against the real API by Jon on 2026-08-14.
- After a multi-turn conversation where one proposed edit is **rejected** and a
  later one is **accepted**, the model's replies stay consistent with the
  script's actual state — it doesn't act as though a rejected edit took effect.
  **Met** — covered by unit tests against `FakeChatClient`
  (`SetEditDisposition_Accepted/Rejected_RewritesTheFrozenToolResultForTheNextTurn`),
  by a real-OpenAI-wire-format integration test (`ScriptChatSessionOpenAIIntegrationTests`),
  and by Jon's live walkthrough against the real API on 2026-08-14.
- All milestone 1 acceptance criteria still pass (regression bar) — **met**,
  full suite green.

## Data Model

`ScriptChatSession` sends `IList<ChatMessage>` to the `IChatClient` as before.
Separately, for the panel to render, each turn is captured as:

```csharp
public sealed record ChatTurn(
    ChatTurnRole Role,           // User | Assistant
    string? Text,
    string? ProposedCode,        // null unless this turn proposed an edit
    string? EditSummary,
    EditDisposition Disposition  // None | PendingReview | Accepted | Rejected
);
```

This is the sequence the single panel renders top-to-bottom — it's a display
projection over the raw API message history, not a replacement for it.

## UI shape

One scrolling transcript — a single `MarkdownTextBox`-derived control that
turns are continuously appended into (D18), not one WinForms control per
turn. User turns and assistant turns appear in order, each lightly formatted
(code blocks monospaced, a bold caption distinguishing "answered" turns from
"proposed an edit" turns). A proposed edit renders as a diff inline in its
turn (added/removed lines colour-coded), with Accept/Reject as a single
permanent pair of buttons below the transcript rather than inline per-turn
controls — enabled only while exactly one proposal is awaiting a decision,
disabled otherwise (D18). Accepting hands the new script to the host's setter
delegate and marks that turn's `Disposition`; both accept and reject append a
short follow-up line to the transcript rather than rewriting the original
diff's caption. No separate transcript/preview split — the whole point is one
continuous read-top-to-bottom history, matching how the conversation actually
happened.

## Build order

1. `CDS.ScriptChat.Core` — `ScriptChatSession`, `ScriptChatClientFactory`,
   `AssistantTurnResult`, `ChatTurn`. No Roslyn, no WinForms, no editor.
2. `ISymbolLookupProvider` interface + a no-op implementation, wire
   `lookup_symbol` into `ScriptChatSession` so the tool-calling path is
   exercised without any host wiring.
3. `CDS.ScriptChat.WinForms` — panel skeleton: transcript rendering, input box,
   send button. No diff/accept yet — proposed edits just display as text.
4. Diff/accept UI, wired to replace the script through the host-supplied setter
   on accept.
5. Key storage (DPAPI) + settings sub-panel (provider/model, key entry, test
   connection).
6. Test host app tying it all together, including a concrete
   `ISymbolLookupProvider` as the worked example for consuming apps.
7. Consume from Fable (and later, the OpenCvSharp Playground app), each
   supplying its own `ISymbolLookupProvider`.

## Acceptance criteria (milestone 1)

- Given a script and an instruction implying a code change, the panel shows a
  diff and the buffer is unchanged until Accept is clicked.
- Given a question with no implied code change, the panel shows only text —
  no diff, no phantom edit.
- `lookup_symbol` is observably used (verifiable via a debug log or test) when
  the model's response depends on an API detail not in the orientation blurb,
  and reaches whatever `ISymbolLookupProvider` the host supplied.
- No API key present → panel clearly indicates the feature is unavailable,
  rather than failing silently or throwing.
- Switching provider in settings and sending a new message works without
  restarting the app.

## Open questions

- **File-vs-property convention for D12**: exact file name/path convention
  for the host-app orientation file (e.g. `scriptchat.context.yaml` at the
  host app's root?), and the exact shape of the fallback property interface,
  are left for build-order step 1 to propose rather than fixed here.
- ~~**Milestone 2 — OpenAI adapter package**~~ — resolved: `Microsoft.Extensions.AI.OpenAI`
  10.9.0, matching the `Microsoft.Extensions.AI` version already in use.
- ~~**Milestone 2 — disposition reconciliation mechanism**~~ — resolved: mutate
  the stored `FunctionResultContent.Result` in place (it's settable, and the
  same object instance already sits in `_history`), rather than injecting a
  separate note.

## Future milestones (deferred by design, not gaps)

- ~~**Line-level diffs**~~ **Done** (D19). Shipped as `propose_script_patch` —
  anchored find-and-replace hunks, not line numbers — alongside the unchanged
  `propose_script_edit`. Per-hunk accept/reject (as opposed to accepting or
  rejecting a whole patch proposal together) remains unscoped; revisit once
  patches have proven themselves in real use.
- **Wider-scope `ISymbolLookupProvider`** (post-D11): workspace-wide symbol
  search, "find references", related-file awareness — moving the interface
  closer to what VS/VS Code's integrated chat can already do, once the v1
  narrow scope shows where it actually falls short in practice. Added as new
  members with default implementations so existing host implementations keep
  compiling.
- ~~**Multi-script host support**~~ **Done.** Real feedback from extracting the
  OpenCvSharp Playground app, parked in `todo.packaging.md` ("Not packaging —
  API feedback from a consuming host"). Shipped as `ScriptChatTarget` (Core) and
  `ScriptChatHostPanel` (WinForms, `SetTargets(params ScriptChatTarget[])`),
  generalised to any number of targets rather than the Playground's original
  two — see `todo.packaging.md` for the detail. The Playground has migrated
  onto it.
- **Local/self-hosted provider** (Ollama, LM Studio, llama.cpp): no base-URL
  override exists today. Also parked in `todo.packaging.md`. Realistically
  rides on top of the OpenAI wiring landing in milestone 2, once that's proven.

---

## Kickoff prompt (historical — Milestone 1, complete)

> Read this document in full before writing any code. Scope this session to
> **Milestone 1** only (build-order steps 1–3, plus enough of step 4 to display
> a diff — full accept/reject wiring can be a follow-up session). Do not
> implement UC2, UC5, or UC7. All UI must be standard WinForms Designer classes
> (`.cs`/`.Designer.cs`/`.resx`, `InitializeComponent()`) that open cleanly in
> the VS 2026 Designer — no code-only layout, per D14. Ask before making any
> architecture decision not already settled by the Decision Log above; flag
> disagreement rather than silently deviating. British English in comments and
> docs.

## Kickoff prompt (Milestone 2)

> Read this document in full before writing any code, especially the Status
> section and "Milestone 2 scope" under Use Cases. Scope this session to
> **Milestone 2 only**: build-order steps 1–3 there (OpenAI wiring in
> `ScriptChatClientFactory`, disposition reconciliation in `ScriptChatSession`
> for UC2, and end-to-end verification in the test host). Do not implement
> Grok, UC5 (mid-session provider switch), UC7 (streaming), multi-script host
> support, or local/self-hosted provider support — all explicitly deferred, see
> "Milestone 2 scope" and "Future milestones" above. The two open questions
> flagged for milestone 2 (OpenAI adapter package choice; disposition
> reconciliation mechanism) are yours to propose and confirm, not pre-decided.
> All UI must be standard WinForms Designer classes (`.cs`/`.Designer.cs`/`.resx`,
> `InitializeComponent()`) per D14 — this milestone shouldn't need new UI, but if
> it does, the same rule applies. Ask before making any architecture decision not
> already settled by the Decision Log; flag disagreement rather than silently
> deviating. British English in comments and docs.