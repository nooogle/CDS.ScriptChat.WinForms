# TODO — Feature backlog

General-purpose backlog for provider and editing-experience work. Distinct from
`todo.packaging.md`, which tracks NuGet/CI/release mechanics.

Each job is independent and can be picked up in any order.

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
("Local/self-hosted provider... no base-URL override exists today") and
parked in `todo.packaging.md` under "Not packaging — API feedback from a
consuming host". This job is about scoping and landing that.

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

## Job 3 — Diff-based script edits (not full-file replacement) — Done

Shipped as D19 in `cds.scriptchat.design.md`. Checked what Claude Code's
`Edit` tool and GitHub Copilot's `replace_string_in_file` tool actually do
first, per the session that did this work — both use anchored find-and-replace
(`{oldText, newText}`), not line numbers or unified-diff text, which resolved
the reconciliation question below for free (no line numbers, nothing to
drift).

- [x] Diff representation: anchored find-and-replace hunks
  (`ScriptEditHunk { OldText, NewText }` in
  [ScriptEditHunk.cs](src/CDS.ScriptChat.Core/ScriptEditHunk.cs)), matching
  Claude Code/Copilot precedent — not unified diff text, not
  `{StartLine, EndLine, ReplacementText}` ranges.
- [x] New sibling tool `propose_script_patch`, alongside the unchanged
  `propose_script_edit` (full-replacement stays the fallback for large
  rewrites). See `ScriptChatSession.ProposeScriptPatch`.
- [x] Partial acceptance in the UI: **deliberately out of scope for this
  milestone** — a patch's hunks are accepted or rejected together via the
  single permanent Accept/Reject bar from D18. Per-hunk accept/reject
  remains a real future milestone if patches see enough hunk-count in
  practice to want it.
- [x] Reconciliation when a hunk no longer cleanly applies: fails closed with
  a clear error (`ScriptPatchApplyException`, via
  [ScriptPatchApplier.cs](src/CDS.ScriptChat.Core/ScriptPatchApplier.cs)) —
  not a fuzzy/context-based re-anchor. Checked twice: once inside
  `propose_script_patch` itself (so the model can retry within the same turn
  on a bad anchor), again on Accept against a fresh read of the buffer
  (catches drift if the user edited the buffer while the proposal sat
  pending).
- [x] Multi-turn disposition reconciliation: generalised, not duplicated —
  `SetEditDisposition`'s frozen-tool-result rewrite already worked from a
  captured `CallId` regardless of which tool produced it, so patch turns get
  the same UC2 bookkeeping as full-replacement turns for free.
- [x] Tests: `ScriptPatchApplierTests` (pure apply-logic unit tests),
  `ScriptChatSessionTests` (capture, rejection, disposition reconciliation),
  `PatchAcceptanceTests` (WinForms panel — accept against a changed buffer,
  reject, hunk-no-longer-matches-stays-pending).
- [x] `cds.scriptchat.design.md` updated: D19 added, D13 marked superseded,
  the "Line-level diffs" future-milestone entry marked done.

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

- None of these three jobs block each other, but Job 1 and Job 2 both touch
  `ScriptChatClientFactory` / `ScriptChatClientOptions` — worth sequencing
  them rather than working both in parallel branches to avoid merge friction.
- Per the "one milestone per session" project rule, each job (or a sensible
  sub-slice of one) should be its own session rather than mixed together.
