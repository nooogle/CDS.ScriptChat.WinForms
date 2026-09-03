# TODO — Feature backlog

General-purpose backlog for provider and editing-experience work. Distinct from
`todo.packaging.md`, which tracks NuGet/CI/release mechanics, and `todo.bugs.md`.

**Scope, since 2026-08-24 (D21): this library is for C# script chat and nothing
else.** General in-app assistants, settings mutation, MCP transports and data
review were all considered and deliberately parked — Jobs 6 and 7 below keep the
reasoning. Read D21 and D22 in `cds.scriptchat.design.md` before reopening any of
it; the arguments were had properly and are recorded.

---

## ▶ Start here — next session

**`V1.4.2` is released** — both packages are on nuget.org, carrying the whole
Job 5 adoption API. Nothing is queued behind a release any more.

Next up, in rough order of value for effort:

- [ ] **Job 8, slice 8a** — restore the input box when a turn fails. A few lines,
  fixes a real loss of the user's typing. Details under Job 8 below.
- [ ] **Bump `CDS.OpenCvSharpPlayground` to `1.4.2`** — it still references the
  `1.4.1-alpha.0.4` local prerelease. See `todo.packaging.md`.
- [ ] The two Known Issues below, then the parked jobs. `todo.bugs.md` is empty.

> **Completed work has been removed from this file** (2026-08-24) so it shows
> only what is outstanding. Job 5 — the adoption path — and the Playground
> migration that proved it both shipped; their reasoning survives as **D20–D23**
> in `cds.scriptchat.design.md`, and the full write-ups are in git history
> (`git log -p -- todo.features.md`).

---

## Job 1 — Add support for Google Gemini

`ScriptChatProvider` is currently `Claude | OpenAI | Grok` (D2) —
`ScriptChatClientFactory` is the only place in the library allowed to know a
provider exists. Gemini would be a fourth case, following the same shape as
the existing Claude/OpenAI wiring in
[ScriptChatClientFactory.cs](src/CDS.ScriptChat.Core/ScriptChatClientFactory.cs).

- [ ] Research how to get an `IChatClient` for Gemini. Options to weigh:
  - A `Microsoft.Extensions.AI`-compatible adapter package, if/when Google
    ships or endorses one.
  - Google's own Gemini SDK wrapped by hand (mirrors how `AnthropicClient` and
    `OpenAIClient` are adapted today via `.AsIChatClient(...)`).
  - Gemini's OpenAI-compatibility endpoint, consumed through the existing
    `OpenAIClient` path with a custom base URL (would piggyback on Job 2's
    base-URL work below rather than needing its own factory branch).
- [ ] Add `Gemini` to the `ScriptChatProvider` enum
  ([ScriptChatProvider.cs](src/CDS.ScriptChat.Core/ScriptChatProvider.cs)).
- [ ] Add a `CreateGeminiClient` branch to `ScriptChatClientFactory.Create`,
  matching the pattern of `CreateClaudeClient` / `CreateOpenAIClient`.
- [ ] Confirm the BYOK story: Gemini API keys come from Google AI Studio /
  Vertex AI — same "never log, cache, or persist" rule as every other
  provider (D3), no exceptions for how the key is shaped.
- [ ] Decide the default model ID(s) to surface in the settings UI
  (`ScriptChatSettingsPanel`) and confirm `MaxOutputTokens` defaults make
  sense for Gemini's response limits.
- [ ] Tests: mirror the existing per-provider coverage in
  `CDS.ScriptChat.Core.Tests` (client construction, rejected-options cases).
- [ ] Update `cds.scriptchat.design.md`'s provider table / D2 note once this
  lands, so the design doc stays the source of truth.

## Job 2 — Support open-source / self-hosted models

Already flagged as a future milestone in `cds.scriptchat.design.md`
("Local/self-hosted provider... no base-URL override exists today"). This job is
about scoping and landing it.

- [ ] Decide which open-source / local runtimes to target first. Candidates:
  - **Ollama** — OpenAI-compatible REST API, easiest fit for the existing
    `OpenAIClient` adapter.
  - **LM Studio** — also exposes an OpenAI-compatible local server.
  - **llama.cpp** (`server` mode) — OpenAI-compatible endpoint as well.
  - Note all three converge on "OpenAI-compatible endpoint, different base
    URL" rather than needing bespoke SDKs — one implementation likely covers
    all of them.
- [ ] Add a base-URL override to `ScriptChatClientOptions`
  ([ScriptChatClientOptions.cs](src/CDS.ScriptChat.Core/ScriptChatClientOptions.cs)),
  optional and defaulting to the provider's normal cloud endpoint.
- [ ] Wire the override through `CreateOpenAIClient` in
  `ScriptChatClientFactory` (the `OpenAIClient` constructor accepts an
  `OpenAIClientOptions` with an `Endpoint`).
- [ ] Decide how BYOK applies when there's no real key (many local servers
  accept any placeholder string, or none at all) — don't force a fake key
  requirement onto a host that doesn't need one.
- [ ] Settings UI: let the user enter a base URL when a "local" provider mode
  is selected, alongside the existing provider/model/key fields.
- [ ] Write down how to test this by hand: install Ollama, pull a small model
  (e.g. `llama3.2`), point the panel at `http://localhost:11434/v1`, confirm
  a round-trip chat turn and a `propose_script_edit` call both work.
- [ ] Tests: at minimum, unit-test that the base URL is threaded through to
  the constructed client's options. A live integration test against a real
  local server is optional/manual, not CI (no local model in CI).
- [ ] Update `cds.scriptchat.design.md` once this lands — this is currently
  listed under "Future milestones", not a formal `D`-numbered decision yet.

## Job 4 — Multi-modal input (images alongside text)

`Microsoft.Extensions.AI.ChatMessage.Contents` is already `IList<AIContent>`
(`TextContent`, `DataContent`, `UriContent`, …), so `IChatClient` (D2) already
normalizes image input across providers — this is additive, not a new
abstraction. `SendAsync` currently only accepts `string userMessage` and
always builds a single-`TextContent` `ChatMessage`
([ScriptChatSession.cs:212](src/CDS.ScriptChat.Core/ScriptChatSession.cs#L212)).

- [ ] Add an overload/parameter to `SendAsync` (e.g. an optional
  `IReadOnlyList<DataContent>? attachments`) rather than changing the
  existing string signature — keep the common text-only path unchanged.
- [ ] Confirm which providers/models Anthropic/OpenAI/Grok support for image
  input today and what happens (clear error, not silent drop) if a host sends
  an image to a model that doesn't support it.
- [ ] D17 applies unchanged: image bytes are content like any prompt/script —
  never logged, cached, or persisted; only passed through the direct provider
  SDK call. No new exception needed, just extend the existing discipline to
  the new content type.
- [ ] Host-side responsibility (D15): the library takes image bytes/URI via
  the new parameter; it does not know how a host picks or produces an image
  (file dialog, clipboard paste, screenshot, etc.) — that stays out of the
  library.
- [ ] WinForms UI: an attach-image affordance in the chat panel (Designer-based
  per D14), plus a way to show what's attached to a pending turn.
- [ ] Tests: `ScriptChatSessionTests` coverage for a turn with an attachment
  (history shape sent to `IChatClient`), and a WinForms test for the attach
  affordance if one is added.
- [ ] Update `cds.scriptchat.design.md` (new `D`-numbered decision) once this
  lands.

## Parked, not deleted

- **Job 6 (host-registered tools)** and **Job 7 (settings chat)** — below, with
  evidence. Out of scope per D21; revisit only with a real customer.
- **Job 1 (Gemini)**, **Job 4 (multi-modal)** — no current need.
- **Job 2 (local/self-hosted models)** — parked, but it is the cheapest route to a
  demo a stranger can run without a cloud key, and the only answer if inspection or
  customer data must not leave site.
- **Job 8 (prompt history)** — *not* parked: small, self-contained, blocked on
  nothing, and inside D21's scope. Its slice **8a** (restore the input box after a
  failed turn) is a few lines and fixes a real loss of the user's typing — the
  cheapest worthwhile thing in this file.

## Job 6 — Host-registered tools

> **PARKED (D21, 2026-08-24).** Out of scope. One note if it is ever revived: if
> the extension point is `IReadOnlyList<AIFunction>`, **MCP support comes free** —
> the official C# MCP SDK surfaces a server's tools as `AIFunction`, so Core would
> never need to know MCP exists. Two rules that came out of that discussion and
> should hold: the *client* decides what needs human approval, never the server
> (`readOnlyHint`/`destructiveHint` are advisory inputs, so gate anything not
> explicitly read-only); and MCP clients bring their own logging pipeline, which
> `TraceSuppressingLoggerFactory` does not cover — a new D17 leak path to close
> before any such work lands.

The tool list is built in `ScriptChatSession`'s constructor and is not
extensible by a host. Since D20 it is conditional — `lookup_symbol` only when a
provider can answer it — but `ScriptChatSessionOptions` still has no way to add
a tool of its own.
`ScriptChatSessionOptions` has no extension point, so a host cannot add a tool of
its own. That is the single blocker for Job 7, and probably for most "chat that
does something in my app" scenarios.

- [ ] Add host-supplied tools to `ScriptChatSessionOptions` — most naturally a
  collection of `Microsoft.Extensions.AI.AIFunction`, since that is what the
  session already builds internally and it keeps D2's "the abstraction is
  `Microsoft.Extensions.AI`" rule intact.
- [ ] Decide whether host tools can be *mutating*, and if so what the approval
  story is. D5's principle — a change is proposed, shown, and requires explicit
  accept before it takes effect — should extend to host tools rather than being
  bypassed by them. A read-only tool needs no gate; a write does.
- [ ] D17 applies unchanged: tool arguments and results are content. Never logged.
- [ ] Tests: a host tool is offered to the model, invoked, and its result reaches
  the transcript.

## Job 7 — Chat that queries and configures application settings

> **PARKED (D21, 2026-08-24).** Out of scope: this library is for C# script chat.
> Kept in full because the use case is genuinely strong and the analysis was
> done properly — revive it only with a real customer behind it, and expect to
> revisit D21 rather than work around it. Note the line below about "most
> existing apps have no script editor" is the premise D21 *cut*; the settings
> case would have to re-argue it.

**The use case.** Let an end user ask an app about its own configuration, and
change it, in plain language — *"what's the exposure set to?"*, *"increase the
blob-detection threshold a bit"*, *"why is this camera returning dark frames?"*.
Aimed squarely at **complex vision systems**, where the settings surface is large,
interdependent and unapproachable, and where the person who needs to change a
setting is often not the person who understands the settings dialogue.

**Why this is a good fit for this library specifically.** An app's settings object
*is* a host API — `HostApiIndex.Describe(typeof(AppSettings))` describes it with
no extra work, and `MetadataCompilation.FromTypes(typeof(AppSettings))` answers
`lookup_symbol` about it including the XML documentation already written on each
property. So Job 5 delivers the *querying* half almost for free, with no script
editor anywhere in the picture. This is the strongest validation of Job 5's
"most existing apps have no script editor" premise.

**What is genuinely missing is the acting half.** Reading settings is a lookup;
*changing* one is not, and the library's only mutation mechanism today is
`propose_script_edit` — script-text-shaped, and useless for "set property X to Y".
So this job depends on **Job 6**.

- [ ] Depends on Job 6 (host-registered tools). Don't start before it.
- [ ] Decide the tool shape: a generic `get_setting` / `set_setting` pair over a
  host-supplied settings object, versus letting the host register one tool per
  meaningful operation. The generic pair is less work for an adopter (which is
  the whole point) but gives the model a much wider blast radius.
- [ ] **Safety is the hard part, not the plumbing.** In an industrial vision
  system a wrong setting can quietly invalidate inspection results rather than
  failing loudly. At minimum: a read-only mode that is the default; explicit
  human accept for every write (D5's principle, not a bypass); a clear record of
  what changed, from what, to what.
- [ ] Consider validation and range awareness — a settings property often has a
  legal range the model can't infer from its type. Worth checking whether
  `[Range]`/`[Description]` annotations or similar can feed the index and the
  tool's schema, since an adopter has often already written them.
- [ ] Consider an undo/revert affordance for a settings change made through chat.
- [ ] Audit trail: who changed what, when, via chat. Likely a hard requirement
  for regulated or validated vision installations, and cheaper to design in than
  to retrofit.
- [ ] Sample: extend Job 5's ordinary-app sample with a settings object, so the
  "query and configure" story is demonstrated rather than described.

## Job 8 — Prompt history: see and re-use earlier prompts in the current conversation

**The use case.** A user types a careful prompt, gets a result, and wants that
prompt back — to send it again after a change, to tweak one clause and retry, or
just to paste it somewhere else. Today there is no way to get it back into the
input box, and one specific case loses it outright (finding 2 below).
Conversation-scoped recall, not a saved prompt library.

### Measured first, not assumed (2026-08-24)

Five findings, all checked against the code. Three of them change what this job
should be.

1. **The data already exists and needs no new store.**
   `ScriptChatSession.Turns` is a public `IReadOnlyList<ChatTurn>`, and a user
   prompt is exactly `ChatTurn { Role = ChatTurnRole.User, Text = "…" }`. Prompt
   history is a `Where(...).Select(t => t.Text)` over something already public.
   **Do not add a parallel history list** — a second copy could disagree with the
   transcript, and it would be new content-bearing state to justify against D17.
2. **A failed turn already loses the user's text, and that is the sharpest pain
   here.** `SendCurrentInputAsync` calls `_inputTextBox.Clear()` *before*
   `SendAsync`, and the `catch` does not put it back — it appends "That turn
   failed: …" to the transcript and leaves the box empty. So a provider error, a
   network blip or a bad key means retyping the prompt from memory. The text is
   not truly gone (`ScriptChatSession` records the user turn *before*
   `GetResponseAsync`, so it survives in `Turns`), it is just unreachable from
   the UI. **Worth fixing on its own, ahead of any history UI, and it is a
   handful of lines.**
3. **Select-and-copy already works.** `_transcriptTextBox` is a
   `CDS.Markdown.MarkdownTextBox`, which derives from `RichTextBox` and defaults
   to `ReadOnly = true` — verified by reflection, because the Designer file never
   sets it either way. So the "or at least clipboard them" half of the request is
   *already possible*: drag-select the prompt and Ctrl+C. What is missing is
   precision (freehand selection in a long transcript is fiddly and catches the
   role caption) and re-use (no way back into the input box).
4. **History is naturally per-script, for free.** `ScriptChatHostPanel` keeps a
   `ScriptChatSession` per target, so reading history off the selected session
   gives each script its own with no extra work.
5. **New conversation deliberately destroys it.** `Session.Reset()` clears
   `_turns`, and `StartNewConversation` builds a whole new session — so history
   dies with the conversation. Correct for the transcript, questionable for
   prompt recall: "start fresh but let me re-send that prompt" is a plausible
   want. Left as an open question below rather than silently decided.

### D17 — the constraint that shapes this, and why it is satisfiable

A prompt is content. D17 forbids content-bearing **logs, caches, telemetry and
diagnostic artifacts** outright rather than behind a flag, so prompt history is
close enough to a cache that it has to be argued explicitly, not waved through:

- **In-memory, for the lifetime of the session, is already the status quo.**
  `Turns` holds every prompt today and the transcript renders them on screen.
  Projecting a list over that adds no new residency and no new lifetime.
- **Nothing may be written to disk. Ever.** No history file, no MRU list beside
  `ProviderPreferenceFile`, no `Properties.Settings`, no registry — that is a
  persistent content cache and a straight D17 violation.
- **Nothing may be logged.** Counts and lengths only, per the existing
  `ScriptChatLog` discipline. Never the prompt text, at any level.
- **The clipboard is the user's own deliberate act** and is fine — that is the
  user moving their own text, not the library retaining it.

If any of the above starts to feel negotiable, stop and re-read D17: "just a
convenience cache" is exactly the shape that rule exists to refuse.

### Recommendation — three slices, smallest first, each shippable alone

- [ ] **8a — Restore the input box when a turn fails.** In
  `SendCurrentInputAsync`'s `catch`, put `userMessage` back into `_inputTextBox`
  (only when the user has not already typed something else) so a failed send is
  one keypress from a retry rather than a retype. No new API, no new state, no
  history UI. **This is most of the real-world value in this job and should not
  wait for the rest of it.** Test: a `FakeChatClient` that throws leaves the
  prompt in the box.
- [ ] **8b — Up/Down arrow recall in the input box.** Terminal-style: Up walks
  back through this conversation's user prompts, Down forward. Costs no screen
  space, needs no Designer change, and is what people already expect from a
  prompt box. The details that decide whether it feels right rather than
  annoying:
  - Only intercept Up when the caret is on the **first** line (Down on the last),
    so arrows still navigate normally inside a multi-line prompt.
    `_inputTextBox` is `Multiline = true`, so this matters.
  - Keep a transient draft of whatever was typed before the first Up and restore
    it when walking forward past the newest entry — losing an in-progress prompt
    to a stray Up would be worse than having no history at all.
  - Reset the recall index on send and on target switch; skip consecutive
    duplicates.
  - Extend the existing `OnInputTextBoxKeyDown`, which already owns
    Enter/Shift+Enter/Ctrl+Enter — the arrow keys belong in the same place.
- [ ] **8c — A visible history affordance, only if 8a+8b prove insufficient.** A
  small button beside Send opening a list of this conversation's prompts, each
  with "Insert into input box" and "Copy". The only slice that touches the
  Designer (D14: real `.Designer.cs`, no code-only layout) and the only one
  costing screen space in a panel typically ~380px wide. **Do not build this
  first.** If Up/Down covers it, this is UI nobody needs; a `ContextMenuStrip`
  on the input box is the cheaper middle ground if a discoverable entry point
  turns out to be wanted.

### Open questions — decide before building 8b, not during

- **Should history outlive "New conversation"?** Finding 5. Reading straight off
  the session is the clean implementation and answers "no". Answering "yes" means
  the panel holding prompt strings *outside* any session — new content-bearing
  state needing its own D17 argument and its own clearing rule. Recommendation:
  **start with "no"**; it is honest, free, and "New conversation" reads as a
  deliberate reset. Revisit only if it bites in use.
- **Should the panel expose history as public API?** `Turns` is already public,
  so a host wanting to render its own can do it today in two lines of LINQ; a
  `ScriptChatPanel.PromptHistory` property would be a second way to say the same
  thing. Recommendation: **no new API** unless a host asks.

### Not in scope

- **Persisting prompts across app restarts** — D17, unambiguously. Not "later",
  not "opt-in": the capability does not belong in this library.
- **A curated prompt/snippet library.** Reasonable product idea, different
  feature, and arguably outside D21. If ever wanted it is the *host's* to own —
  the host already controls the input box's contents through the panel.
- **Editing or re-running a previous turn in place.** That is transcript
  mutation and conflicts with the one-pending-proposal rule; a recalled prompt is
  a new turn at the end, like anything else.

---

## Known issues (small, not full jobs — tracked so they don't get lost)

- [ ] **`InputBoxScrollTests.MouseWheel_OverAnOverflowingInputBox_ScrollsIt` is
  flaky.** Drives the real OS mouse cursor and compares pixel bitmaps
  before/after a scroll; sensitive to the host machine's pointer
  precision/acceleration settings and to whatever else has focus at the
  moment it runs. Already documented as such in its own file and
  deliberately not part of the CI-required check — a local failure is a
  prompt to re-verify by hand, not a release blocker. Consider replacing the
  bitmap-diff assertion with something that reads the actual scroll position
  (e.g. via a UIA value/scroll pattern) instead of comparing screenshots, if
  it keeps being noisy enough to be worth the effort.
- [ ] **The `--demo=markdown` table renders badly at the chat panel's default
  width.** `MarkdownTextBox`'s "best-effort monospaced grid" table technique
  (`CDS.Markdown.Lite`) doesn't fit typical column content within a
  narrow panel — columns wrap mid-cell rather than staying aligned. Currently
  worked around by using `--demo=patch` for the README screenshot instead
  and noting the limitation in `assets/readme.md`, not fixed. Options if it's
  worth revisiting: a host-width-aware column layout in the table renderer
  (upstream, in `CDS.Markdown.Lite`), or falling back to a non-tabular
  (key/value list) rendering for tables past some column-count/width
  threshold.

## Notes

- Jobs 1, 2 and 4 don't block each other, but Job 1 and Job 2 both touch
  `ScriptChatClientFactory` / `ScriptChatClientOptions` — worth sequencing
  them rather than working both in parallel branches to avoid merge friction.
- **Job 7 cannot start before Job 6** — it needs host-registered tools to have
  anywhere to hang a settings write. Both are parked (D21), so neither is queued
  behind anything today; the dependency only matters if they are revived.
- **Job 2 is worth pulling forward for the public launch**, out of numeric order.
  "Works with a local model, no cloud key needed" removes the single biggest
  barrier to a stranger trying the library at all — and it needs no new
  framework, just a base-URL override on the `Microsoft.Extensions.AI` path
  already in place.
- Per the "one milestone per session" project rule, each job (or a sensible
  sub-slice of one) should be its own session rather than mixed together.
