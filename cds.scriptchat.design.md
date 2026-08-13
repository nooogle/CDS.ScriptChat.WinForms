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
- **Outstanding from Milestone 1** (not scope gaps, but unfinished follow-through):
  - The pre-release logging checklist under "What must be removed or turned off
    before release" is still unaddressed — the test host still runs at
    `LogLevel.Trace` (`samples/CDS.ScriptChat.TestHost/Program.cs`). Must be
    resolved before Fable or the Playground consumes the library, per D3.
  - ~~`ScriptChatClientFactory` throws `NotSupportedException` for `OpenAI`~~ —
    closed by milestone 2. `Grok` still throws; remains deferred.
- **Milestone 2** (UC2, OpenAI wiring) — **build complete, tests passing**
  (58 Core + 76 WinForms, solution builds clean). `ScriptChatClientFactory` now
  wires `Microsoft.Extensions.AI.OpenAI` for real; `ScriptChatSession` rewrites
  the frozen `propose_script_edit` tool-result on accept/reject so a later
  turn's history matches what actually happened to the script. **Not yet
  exercised against the real OpenAI API** — covered so far only by unit tests
  against a fake key and a fake `IChatClient`; a live smoke test with a real
  key is still outstanding before calling this done in practice.

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
| D13 | Diff granularity is full-script replacement in v1, matching the typical size of scripts in these apps. Line-level diffs are an explicit future milestone, not a v1 concern (see Future Milestones below). |
| D14 | Every UI component (panel, settings sub-panel, any dialog) is built as a standard WinForms Designer class — `.cs` / `.Designer.cs` / `.resx` triplet, `InitializeComponent()`, no hand-rolled layout-in-code, no third-party designer-incompatible UI framework. Must open and edit cleanly in the VS 2026 WinForms Designer. Adding transcript items to a container at runtime is data binding, not layout, and is not covered by this rule. |
| D15 | **Nothing use-case-specific ships in the library.** No Roslyn, no CDS.CSharpScripting2, no Scintilla, no OpenCvSharp — no dependency that presumes a particular host app, scripting engine, or editor control. Symbol lookup is an interface the host implements (`ISymbolLookupProvider`); the script buffer is reached through host-supplied delegates. The library ships a no-op symbol provider so the tool-calling path works out of the box, and the test host app carries the worked example of a real one. Supersedes the `CDS.ScriptChat.Roslyn` project in the original architecture and the Scintilla dependency in D7. |
| D16 | **Logging is standard `Microsoft.Extensions.Logging` throughout, and content is confined to `Trace`.** Every component takes an `ILoggerFactory` (not a bare `ILogger`), so the whole `Microsoft.Extensions.AI` pipeline — function invocation and the provider round-trips — is instrumented from one property. Levels are split so the split is enforceable rather than a matter of care at each call site: `Information` and above carry **structure only** (names, lengths, counts, timings, event IDs, exceptions), while prompts, responses, proposed scripts, edit summaries, symbol signatures, and the orientation blurb appear at **`Trace` and nowhere else**. API keys appear at no level at all (D3) — only a key's length, which is what distinguishes a truncated paste from a wrong key. See "Logging" below. |

## Logging

Added after milestone 1's first end-to-end run, because a turn that "didn't obviously
work" left nothing behind to diagnose it with.

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

The test host supplies a small CSV `ILoggerProvider` (`samples/…/Logging/`), writing one
file per run under `%LOCALAPPDATA%\CDS.ScriptChat.TestHost\logs\`, with a link to it at
the bottom of the window. It lives in the sample, not the library, per D15 — a consuming
app brings its own logging.

### What must be removed or turned off before release

The test host deliberately runs at `Trace`, which means **its log file contains the user's
script, their messages, the model's replies, and any proposed edit**. That is right for a
diagnostic host and wrong for a shipping one; D3 rules out this kind of activity and
content recording in a product.

Because of the level discipline in D16, switching it off is a floor change rather than a
code edit — but it is not optional, and it is not done yet:

- [ ] **Test host**: drop `SetMinimumLevel(LogLevel.Trace)` in `Program.cs` to
      `LogLevel.Information`, or keep Trace only behind an explicit opt-in (a command-line
      switch or a debug build), so a casual run does not write content to disk.
- [ ] **Consuming hosts** (Fable, the OpenCvSharp Playground): never configure `Trace` for
      the `CDS.ScriptChat.*` categories. `Information` gives the full call/result/timing
      picture with no user content in it.
- [ ] **Re-check before the first release** that no content-bearing message has drifted up
      out of `Trace`. `ScriptChatSessionLoggingTests` covers this for the session
      (`SendAsync_AtInformation_RecordsNoScriptOrPromptOrResponseContent`); extend it if new
      content-bearing messages are added.

Owner: Jon. Due: before `CDS.ScriptChat` is consumed by any app other than the test host.

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
2. **Disposition reconciliation in `ScriptChatSession`** — **done.** A new
   `_turnProposalResults` list (parallel to `_turns`) keeps a reference to each
   turn's `propose_script_edit` `FunctionResultContent`. `SetEditDisposition`
   mutates that object's `Result` in place — it's the same instance already
   sitting in `_history`, so no extra history-editing step is needed — to "The
   user accepted/rejected this edit..." once the user decides, replacing the
   frozen "not applied yet" text.
3. **Verification** — unit-level done: 58 Core + 76 WinForms tests pass,
   solution builds clean. **Not done**: a live smoke test against the real
   OpenAI API (needs a real key — BYOK, so that's down to whoever runs the test
   host next) and a manual UC2 walkthrough in the test host UI.

#### Acceptance criteria (milestone 2)

- A configured OpenAI API key and model can complete a real UC1-style turn
  (text-only answer, and a turn that proposes an edit shown as a diff) —
  matching Claude's behaviour, not a stub. **Unverified against the real API**
  — only covered by unit tests against a fake key so far.
- After a multi-turn conversation where one proposed edit is **rejected** and a
  later one is **accepted**, the model's replies stay consistent with the
  script's actual state — it doesn't act as though a rejected edit took effect.
  **Covered by unit tests** (`SetEditDisposition_Accepted/Rejected_RewritesTheFrozenToolResultForTheNextTurn`)
  asserting the rewritten tool-result text reaches the next provider call;
  not yet walked through manually against a real model's actual replies.
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

One scrolling panel. User turns and assistant turns appear in order, each
lightly formatted (code blocks monospaced, a small label distinguishing
"answered" turns from "proposed an edit" turns). A proposed edit renders as a
diff inline in its turn, with Accept/Reject buttons; accepting hands the new
script to the host's setter delegate and marks that turn's `Disposition`. No separate
transcript/preview split — the whole point is one continuous read-top-to-bottom
history, matching how the conversation actually happened.

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

- **Line-level diffs** (post-D13): once full-script replacement has proven
  itself in real use, revisit whether line-level hunks are worth the added
  complexity for accept/reject at finer grain.
- **Wider-scope `ISymbolLookupProvider`** (post-D11): workspace-wide symbol
  search, "find references", related-file awareness — moving the interface
  closer to what VS/VS Code's integrated chat can already do, once the v1
  narrow scope shows where it actually falls short in practice. Added as new
  members with default implementations so existing host implementations keep
  compiling.
- **Multi-script host support**: real feedback from extracting the OpenCvSharp
  Playground app, parked in `todo.packaging.md` ("Not packaging — API feedback
  from a consuming host"). A host with more than one script has to build its
  own selector/session-per-target scaffolding today; worth a `SetTargets(...)`
  shape in the library once a second consuming host confirms the need.
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