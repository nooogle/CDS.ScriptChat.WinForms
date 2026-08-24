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

## Job 5 — Make adoption easy for a host that has C# scripts *(the current milestone)*

**The goal.** An existing WinForms app with a C# script editor and some API types
should get a working AI script assistant in **two calls and no adapter classes**.
Everything below is measured against that, not against internal tidiness.

**Scope (D21).** C# script chat only. Not general chat, not settings, not MCP, not
data review. Jobs 6 and 7 are parked below with their reasoning intact.

### The measured baseline (2026-08-24)

What the OpenCvSharp Playground — the **best case**, since the workbench had already
built the Roslyn tooling — actually writes to consume this library:

| File | Lines |
|---|---|
| `ScriptChatOrientation.cs` | 185 |
| `RoslynSymbolLookupProvider.cs` | 86 — **duplicated verbatim in `demo/UseCasesDemo`** |
| `MainForm.Chat.cs` (chat wiring only) | 202 |
| **Adopter total** | **~473** |
| Prerequisite tooling built first: `ScriptSymbolLookup` 393 + `ScriptApiIndex` 208 + `ScriptSymbolInfo` 35 | 636 |

~1,100 lines, plus two markdown context files. The adapter's own doc comment names
the problem: *"This adapter is the entire cost of choosing your own chat library."*

### Target quickstart

Design backwards from this. If it is the README's first code block, the job is done:

```csharp
// One Type drives BOTH the orientation index and lookup_symbol, so what the model
// is told exists and what it can ask about cannot drift apart.
chatPanel.AddScript(
    name:  "Processing",
    read:  () => _scriptBox.Text,
    write: text => _scriptBox.Text = text,
    api:   typeof(ProcessingGlobals));

// Key store + settings dialogue + restore-on-startup, in one line.
chatPanel.UseStoredKey("MyApp");
```

---

- [x] **1 — Roslyn in the box (D22).** *Done 2026-08-24.* `Microsoft.CodeAnalysis.CSharp`
  5.9.0 added to Core — the same version the consuming scripting hosts already load,
  so no diamond. 114 Core + 87 WinForms tests green, solution builds with 0 warnings.
  Landed: `RoslynSymbolResolver`, `RoslynSymbolLookupProvider`,
  `MetadataCompilation`, `HostApiIndex`, `SymbolLookedUpEventArgs`, and one piece
  that was not planned — see below.
  - **`XmlFileDocumentationProvider` (unplanned).** Roslyn's own
    `XmlDocumentationProvider.CreateFromFile` — which the plan named — lives in
    `Microsoft.CodeAnalysis.Workspaces`, not in Common or CSharp. Taking the whole
    Workspaces package for one class was the wrong trade on a package a host adopts
    purely to answer `lookup_symbol`, so Core carries a ~50-line
    `DocumentationProvider` of its own instead. Covered by
    `MetadataCompilationTests.FromTypes_AttachesXmlDocumentation`, which is the test
    that would have caught the silent no-documentation failure.
  - **`HostApiIndex` was pulled forward from item 4**, because the resolver needs
    `ScriptFacingTypes` to know which types to search a bare member name on. Its
    flat-API bug and the hardcoded `"API"` facade name are both fixed and pinned by
    tests; the rest of item 4 (orientation composition) is untouched.
  - **Migration note for the Playground**: its adapter took a
    `ScriptEnvironment`; the shipped resolver takes `IEnumerable<string>` instead,
    so `new RoslynSymbolResolver(editor.API.Environment.NamespaceNames, globalsType,
    componentTypes)` is the replacement. `ScriptSymbolInfo` and both copies of
    `RoslynSymbolLookupProvider` can then be deleted.
  - Original plan, kept for reference:
  - `RoslynSymbolResolver` — the 393-line resolver (`ScriptSymbolLookup.cs`):
    metadata arity for generics, namespace-prefix candidates, inheritance walk,
    `cref` resolution, doc flattening. Must return `SymbolLookupResult` **directly**
    — `ScriptSymbolInfo` is a four-property twin of it and gets deleted rather than
    moved, along with every mapping of it.
  - `RoslynSymbolLookupProvider : ISymbolLookupProvider` — the adapter both existing
    consumers hand-wrote. Two overloads: `Func<CancellationToken, Task<Compilation?>>`
    for a live editor, and a plain `Compilation` for a host whose compilation is static.
  - `MetadataCompilation.FromTypes(...)` / `.FromAssemblies(...)` — the path for a
    host that runs scripts but never exposes a Roslyn `Compilation` (e.g. plain
    `CSharpScript.EvaluateAsync`). Carries the non-obvious part: XML docs must be
    attached explicitly via `XmlDocumentationProvider.CreateFromFile`, because
    Roslyn does not look for the `.xml` on its own. Miss it and every lookup returns
    a correct signature with no documentation.
  - `ISymbolLookupProvider` stays public and unchanged — a host with its own engine
    still implements it. The shipped provider is the answer for everyone else.
  - Tests: port `ScriptSymbolLookupTests` (143 lines) from the donor repo.

- [x] **2 — `UseStoredKey` — the boilerplate nobody was tracking.** *Done 2026-08-24.*
  ~70 lines of load-key / show-settings / persist-choice replaced by one call on
  `ScriptChatHostPanel`. 105 WinForms tests green.
  - Three overloads, narrowing as the host needs more control:
    `UseStoredKey("MyApp")` (DPAPI store + preferences in a file beside it — the
    whole story, one line); `UseStoredKey("MyApp", load, save)` for a host that
    keeps provider/model in its own settings file; and
    `UseStoredKey(IApiKeyStore, load, save)` for a host with its own key store.
  - `ScriptChatProviderPreference(Provider, ModelId)` is the public record the
    load/save pair trades in. It carries **no key** — that stays in `IApiKeyStore`
    (D3), and the internal `ProviderPreferenceFile` never sees one.
  - `ProviderPreferenceFile` uses plain `key=value`, not JSON: two values do not
    justify a serialiser, and it avoids reflection-serialisation trimming warnings
    in a library. Every failure path degrades to "no preference recorded" — falling
    back to the default provider is survivable, refusing to open the panel is not.
  - New `ApiKeyStore` property so a host can still reach the store.
  - **Known gap, deliberate**: this is on `ScriptChatHostPanel` only, because
    `ScriptChatPanel` has no settings button and so no `SettingsRequested` to hang
    it off. That makes `ScriptChatHostPanel` the control an adopter actually drops
    in, with `ScriptChatPanel` the inner one — worth confirming when item 3 lands,
    since `AddScript` should follow the same choice.
  - **Second gap, minor**: the host panel's status reads just `Ready.` where
    `ScriptChatPanel` shows `Ready · {provider} · {model}`. A host-panel user
    cannot see which provider is live. Not fixed here to keep the diff focused.

- [x] **3 — `AddScript` — where "easy" actually lands.** *Done 2026-08-24.*
  122 Core + 112 WinForms tests green. The two-call quickstart is now real and is
  pinned by `AddScriptTests.Quickstart_TwoCalls_ProducesAWorkingPanel`, so it cannot
  quietly stop being true.
  - `ScriptChatHostPanel.AddScript(name, read, write, api, additionalTypes)` is the
    easy path; `AddScript(name, read, write, createSessionOptions)` keeps the
    factory shape for a host that needs it (the Playground's counterpart-script
    snapshot). The capability stays, it is just no longer the only shape.
  - **`ScriptChatSessionOptions.ForHostApi(api, additionalTypes)` in Core** does the
    real work, and `AddScript` is a thin wrapper over it. Extracted because a test
    was reaching into `_targets` by reflection to observe the wiring — a smell that
    said this belonged in public API. It also serves a host that drives
    `ScriptChatPanel` directly and so never touches the host panel.
  - `HostApiLookup` (internal) holds the two halves: a lazily-built
    `MetadataCompilation` over the host's assemblies, and the orientation
    composition (prose from `scriptchat.context.md` if deployed, then the generated
    index). The compilation is deferred to the first lookup — walking loaded
    assemblies is not work to do while a host is still building its main form.
  - Namespaces for bare-name resolution default to the namespaces of the API types
    themselves, so a host that never said what its scripts import still gets bare
    type names resolving.
  - **Fixed an ordering trap while here**: `SetTargets` created no sessions, so
    calling it *after* `Configure`/`UseStoredKey` left every target dead. Both
    `SetTargets` and `AddScript` now create sessions when a client already exists,
    so wiring order no longer matters. Two tests pin it.

- [x] **4 — Orientation composition.** *Done 2026-08-24.* `HostApiIndex` landed with
  item 1; the composition and the resolver gap closed here.
  - [x] **`Describe` never listed the root type's own members** — `Describe(typeof(MyAppApi))`
    on a flat API class returned `""`, silently. Fixed: the root is now the first
    entry, which also surfaces the plain data a globals type carries (an `int`
    threshold) that nothing else in the walk mentioned. Its facade is deliberately
    *not* followed at the root, so the property walk still labels it `API` — what a
    script author types — rather than `MyGlobals.API`.
  - [x] Facade property name is now an optional parameter defaulting to `"API"`,
    and `null` follows no facade at all.
  - [x] Dead `cref`s to `ScriptRunner<TGlobals>`, `Control`/`UserControl` etc. removed.
  - [x] **`HostOrientationResolver` was bypassed by the only real adopter**, which
    hand-rolled its own `ReadContext` because it needed per-script filenames. Fixed:
    `FileNameFor(scriptName)` and `ResolveForScript(...)` add a
    `scriptchat.<name>.context.md` convention that falls back to the shared
    `scriptchat.context.md`, so a host with several scripts writes one file until a
    script actually needs its own. `AddScript` passes the script's name through
    automatically, so this costs an adopter nothing. Composition with the generated
    index now lives in `HostApiLookup.BuildOrientation`, which is the other half the
    adopter was hand-writing.
  - [x] **`IScriptChatHostContext` reviewed and deliberately kept.** It is thin — one
    string property — but it is a legitimate way for a host to supply the blurb from
    code rather than a file, it is public in a package already live on nuget.org, and
    removing it would be a breaking change that buys nothing. Dropped from the
    documented path (the readmes now lead with `ForHostApi`) rather than retired.
    Not everything untidy is worth churning public API over.
  - [x] **Stale `LoggerFactory` XML doc fixed** (was under "Known issues" below): it
    still claimed `Trace` records prompt and response content, which D17 removed
    outright. It shipped in a public package's IntelliSense telling adopters
    something untrue about how their data is handled.

- [x] **6 — Point the docs at the easy path.** *Done 2026-08-24.* Root `README.md`
  quick start is now the two-call version, with the hand-written
  `ISymbolLookupProvider` route demoted to "The manual path". Both package readmes
  updated. Several statements had become false and were corrected rather than left:
  "no Roslyn" in the Core description and the packages table (D22), and "nothing
  use-case-specific ships in the library". The
  `GenerateDocumentationFile` warning is called out in all three readmes and the
  sample's `.csproj` — it is the easiest thing for an adopter to get wrong, and it
  fails by looking like it worked.

- [x] **5 — An ordinary-app sample.** *Done 2026-08-24.*
  `samples/CDS.ScriptChat.SampleApp` — a widget inspection station with a script
  `TextBox`, a documented domain API (`InspectionApi` / `ScriptGlobals`), and the
  two-call wiring. Verified three ways: 8 acceptance tests in `SampleAppTests`, a
  Designer smoke test that constructs `MainForm` (its Designer file is hand-written
  per D14), and an actual process launch confirming the window opens.
  - **The sample runs its scripts** via `Microsoft.CodeAnalysis.CSharp.Scripting`
    (5.9.0, matching the Roslyn Core already brings). An editor whose contents never
    execute is unconvincing; this shows the whole loop — ask, accept, run.
  - **`SampleAppTests` is the real acceptance test for the easy path**, because it
    runs against an ordinary app rather than purpose-built fixtures. It pins the two
    silent-failure modes: the context-file prose actually reaching the blurb, and
    XML documentation actually reaching `lookup_symbol`.
  - The orientation blurb was reviewed by eye, not just asserted on. It reads:
    host prose, then `- \`ScriptGlobals\`: API, LowerLimitMm, UpperLimitMm` and
    `- \`API\`: FailCount, Log, Measure, Parts, PassCount, Record`.
  - **Finding worth carrying into the docs**: an adopting app must set
    `GenerateDocumentationFile`, or every lookup returns a correct signature with no
    documentation and nothing looks wrong. Called out in the sample's `.csproj`
    comment and its readme; belongs in the main README too (item 6).

### Parked, not deleted

- **Job 6 (host-registered tools)** and **Job 7 (settings chat)** — below, with
  evidence. Out of scope per D21; revisit only with a real customer.
- **Job 1 (Gemini)**, **Job 4 (multi-modal)** — no current need.
- **Job 2 (local/self-hosted models)** — parked, but it is the cheapest route to a
  demo a stranger can run without a cloud key, and the only answer if inspection or
  customer data must not leave site.

## Job 6 — Host-registered tools

Today the tool list is **hardcoded**: `lookup_symbol`, `propose_script_edit` and
`propose_script_patch`, built unconditionally in `ScriptChatSession`'s constructor
([ScriptChatSession.cs:135-137](src/CDS.ScriptChat.Core/ScriptChatSession.cs#L135)).
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

---

## Known issues (small, not full jobs — tracked so they don't get lost)

- [x] **`ScriptChatSessionOptions.LoggerFactory`'s XML documentation contradicted
  D17** — it still claimed `Trace` records prompt and response content, a
  capability D17 removed outright. *Fixed 2026-08-24* as part of Job 5 item 4;
  it now states that no content is logged at any level and that this is enforced
  rather than defaulted.

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
- **Jobs 5 → 6 → 7 are a chain**, in that order. Job 7 cannot start before Job 6
  (host tools), and Job 6 is only worth much without Job 5 if something other
  than a settings-style use case needs it.
- **Job 2 is worth pulling forward for the public launch**, out of numeric order.
  "Works with a local model, no cloud key needed" removes the single biggest
  barrier to a stranger trying the library at all — and it needs no new
  framework, just a base-URL override on the `Microsoft.Extensions.AI` path
  already in place.
- Per the "one milestone per session" project rule, each job (or a sensible
  sub-slice of one) should be its own session rather than mixed together.

## Context for jobs 5–7 (from the OpenCvSharp Workbench review, 2026-08-23)

These jobs came out of reviewing whether the Workbench's Roslyn script tooling
should be donated here. The full evidence lives in that repo's `docs/todo.md`;
the parts that matter here:

- **D15 was reconsidered and holds, but not for the reason usually given.** Size
  is a weak objection: `Microsoft.CodeAnalysis.dll` is **2.96 MB** deployed
  against the **11.90 MB** of AI SDKs `CDS.ScriptChat.Core` already ships (+25%),
  and it has exactly one transitive dependency. The real reasons are **version
  diamonds** (three Roslyn majors were cached on one dev machine: 4.14.0, 5.6.0,
  5.9.0 — pinning in Core imposes that on consumers who never call a lookup) and
  **irreversibility** (Core is live on nuget.org at `V1.1.0`; adding a dependency
  later is non-breaking, removing one is not). A satellite package satisfies D15
  rather than relaxing it.
- **Microsoft Agent Framework was checked and is not a threat.** It overlaps the
  bottom of `ScriptChat.Core` — provider plumbing and session state, both on the
  same `Microsoft.Extensions.AI` foundation — and nothing else. Its UI
  integrations (AG-UI, ChatKit, DevUI) are **all web**: no WinForms, no WPF, no
  code-editor or C# code-intelligence story anywhere. Its **CodeAct** is the
  inverse premise — agent-authored throwaway code in a sandbox, versus this
  library's human-authored artifact that the user reviews and accepts. Worth
  knowing; nothing to act on.
