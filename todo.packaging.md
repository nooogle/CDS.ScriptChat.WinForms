# TODO — Packaging, versioning & release

Tracks the work to take `CDS.ScriptChat.Core` and `CDS.ScriptChat.WinForms` from
"builds in the solution" to "published on NuGet.org", via a local-feed stage so
host apps (Fable, OpenCvSharp Playground) can consume real packages before
anything goes public.

Reference implementation: `C:\dev\nooogle\CDS.CSharpScripting2` — its
`Directory.Build.props`, `.github/workflows/ci.yml` and
`.github/workflows/publish.yml` are the closest working example. Where this list
deviates from that repo, the reason is noted.

Order matters: phases 1–3 must land before the repo goes public, phases 4–6 after.

---

## ⚠ BLOCKER — content logging must be off before anything goes public *(closed 2026-08-14, see D17)*

**Prompts, responses, the user's script and proposed edits were originally written
to disk** by the test host, which ran at `Trace`. That was correct for a
diagnostic host and unacceptable in anything shipped or public (D3).

Resolved, not just gated: every content-bearing `[LoggerMessage]` this library
ever defined has been deleted outright, `ScriptChatSession` wraps every
`ILoggerFactory` it's given in `TraceSuppressingLoggerFactory` (closing the
`Microsoft.Extensions.AI` dependency-level leak too), and the test host's
`--trace` flag was removed since there's nothing left it could unlock. See D17
and the "Content-bearing logging — removed, not gated" section in
`cds.scriptchat.design.md` for the full account, including the two tests that
prove it (`ScriptChatSessionLoggingTests`).

Two things that make this live *now* rather than later:

- The design doc puts the due date at "before `CDS.ScriptChat` is consumed by any
  app other than the test host". Consuming the local packages from the
  OpenCvSharp Playground meets that condition — so the "consuming hosts never
  configure `Trace` for `CDS.ScriptChat.*`" item applies from the first
  integration, not from the first publish.
- The repo is currently **private** on GitHub. Any log file committed by accident
  before it flips to public becomes public with it, and stays in history. Check
  for stray logs as part of the phase 4 audit.

---

## Phase 1 — Make the libraries produce packages

Both `src` projects ship: `CDS.ScriptChat.Core` (provider-agnostic engine) and
`CDS.ScriptChat.WinForms` (the panel). The `ProjectReference` from WinForms to
Core is emitted as a package dependency automatically, so no extra wiring is
needed there — but the two versions must stay in lockstep, which MinVer gives us
for free (phase 2).

### 1a — Multi-target `net48` + `net10.0`

**Decision (2026-08-14): deferred past the first public release.** Going
public and shipping a first NuGet release doesn't require it — it's a
substantial, separate chunk of work (see the breakdown below) unrelated to the
CI/publish mechanics. Ship `net10.0`/`net10.0-windows`-only first; revisit if
a consuming host actually needs `net48`.

Do this **first**, before anything else in this phase, *if and when it's picked
back up*: it is the change most
likely to force API changes, and those are far cheaper now than after a public
package exists. The dependencies are all viable — `Anthropic` 12.39.0 ships
`netstandard2.0`, and `Microsoft.Extensions.AI` / `.Abstractions` 10.8.3 ship
both `netstandard2.0` and `net462`.

- [ ] Core: `<TargetFrameworks>net48;net10.0</TargetFrameworks>`.
      WinForms: `<TargetFrameworks>net48;net10.0-windows</TargetFrameworks>`
      with `UseWindowsForms` (mirrors `CDS.CSharpScript2.ScintillaEditor`).
- [ ] Expect compile breaks on the `net48` leg and fix them behind
      `#if NET48` / `<Condition="'$(TargetFramework)' == 'net48'">` rather than
      dropping down to the lowest common denominator on modern targets:
  - `ImplicitUsings` pulls in a different set — expect missing `using`s.
  - `init` accessors and `record` need an `IsExternalInit` shim on `net48`.
  - Nullable annotations compile but the BCL is unannotated, so expect a wave of
    CS86xx warnings; `TreatWarningsAsErrors` is off, so these will be noise
    rather than failures.
  - `CancellationToken`-honouring async paths and `IAsyncEnumerable` need
    `Microsoft.Bcl.AsyncInterfaces` on `net48`.
  - Any `System.Text.Json` / `HttpClient` behaviour differences on .NET
    Framework — particularly TLS defaults for the provider call.
- [ ] DPAPI: the WinForms csproj notes that `System.Security.Cryptography.ProtectedData`
      is in-box for `net10.0-windows`. On `net48` it lives in `System.Security.dll`
      and needs an explicit `<Reference Include="System.Security" />`, or the
      `System.Security.Cryptography.ProtectedData` package for a uniform API
      surface. Verify the key store round-trips on both legs.
- [ ] Confirm the `LoggerMessage` source generator in
      `Microsoft.Extensions.Logging.Abstractions` runs on the `net48` leg —
      `ScriptChatLog` / `ScriptChatWinFormsLog` depend on it (D16). If it does
      not emit for `netstandard2.0`, that is a blocking finding, not a detail.
- [ ] Confirm the WinForms Designer still opens the panel with two target
      frameworks in play — VS designs against the first-listed TFM, so the order
      in `TargetFrameworks` matters. Put whichever TFM you want the Designer to
      use first and note the choice here.
- [ ] Tests: decide whether the test projects also multi-target. Running the
      suite on `net48` is the only real proof the down-level leg works; MTP on
      `net48` needs checking before committing to it.

### 1b — Packaging *(done)*

- [x] Packing turned on for the two `src` projects via `IsPackable` (defaulted to
      `false` in the root `Directory.Build.props`, opted back in per project, so
      new samples and tests stay out of the packing run by default).
      `IncludeSymbols` + `SymbolPackageFormat=snupkg` are set once at root.
- [x] `GeneratePackageOnBuild` deliberately **not** used, unlike
      CDS.CSharpScripting2 — it packs on every build in every configuration.
      `pack-local.ps1` packs explicitly instead.
- [x] Artifacts output layout: **not adopted**. `UseArtifactsOutput` moves every
      `bin`/`obj` path, which risks the WinForms Designer and the TestHost for no
      gain here — `pack-local.ps1` already puts packages somewhere predictable via
      `--output`, which is also what CDS.CSharpScripting2 does. Revisit only if
      the scattered `bin` folders become a nuisance.
- [x] Per-package `readme.md` for each `src` project, wired up with
      `PackageReadmeFile` + a packed `None` item. (A root `README.md` is still
      outstanding — see phase 4. NuGet needs the readme *inside* the package, so
      a link to the root one would not have worked anyway.)
- [x] SourceLink: `PublishRepositoryUrl` and `EmbedUntrackedSources` at root,
      with `ContinuousIntegrationBuild` under a `GITHUB_ACTIONS` condition so
      deterministic source paths do not break local debugging. Verified — the
      nuspec carries the repo URL and commit SHA.
- [x] Verified: `dotnet pack -c Release` produces exactly 2 × `.nupkg` +
      2 × `.snupkg`, nothing from `samples`/`tests`. Package contents checked —
      XML doc file present, readme present, and `CDS.ScriptChat.WinForms` depends
      on `CDS.ScriptChat.Core` at the matching version.
- [x] Package icon: `assets/icon.png` (256×256, 13.8 KB), shared by both packages
      via `PackageIcon` + a packed `None` item pointing at the one file rather
      than a copy per project. Verified present inside both `.nupkg`s.
      **Flaticon licence requires attribution** — carried in both package readmes
      and documented in `assets/readme.md`. If the icon is replaced, the
      attribution goes with it.
- [ ] Confirm `PackageId` — currently defaults to the assembly name
      (`CDS.ScriptChat.Core`, `CDS.ScriptChat.WinForms`). Fine unless the
      nuget.org naming decision changes it.
- [ ] `PackageReleaseNotes` or a CHANGELOG — deferred to first publish.

## Phase 2 — MinVer versioning *(done)*

- [x] MinVer 7.0.0 `PackageReference` in the root `Directory.Build.props` with
      `PrivateAssets=all`.
- [x] `MinVerTagPrefix` set to uppercase **`V`**, matching the house convention
      already used in CDS.CSharpScripting2. Git tags are case-sensitive, so both
      workflow triggers in phases 5–6 must be `V*` — note that
      CDS.CSharpScripting2's `ci.yml` uses `v*` and so never fires; do not copy
      that half of it.
- [x] `MinVerMinimumMajorMinor` set to `1.0`.
- [x] Verified against the `V1.0.0` tag: packages version as exactly `1.0.0`,
      `AssemblyVersion` `1.0.0.0`, informational version carries the commit SHA.
- [ ] Confirm CI checks out with `fetch-depth: 0` and `fetch-tags: true` — MinVer
      silently falls back to `0.0.0-alpha.0` on a shallow clone. (Phase 5.)

## Phase 3 — Local NuGet feed *(done)*

Height-based prerelease versions are what make this work: every commit produces a
different version, so a host app never picks up a stale `~/.nuget/packages` entry.

- [x] Feed location: `C:\dev\localfeed`, registered machine-wide as the
      `cds-local` source. Deliberately **not** the existing `Ours` source
      (`D:\Dropbox\CDS\Dev\NuGet`) — that folder is Dropbox-synced, and packing on
      every commit would push a stream of throwaway prerelease packages through
      sync. `pack-local.ps1 -Feed` switches target if that judgement is wrong.
- [x] `pack-local.ps1` at the repo root: restore → build → test → pack → copy to
      the feed. Test projects are run as MTP executables (`dotnet run --project`),
      not via `dotnet test`.
- [x] Stale-cache escape hatch: `pack-local.ps1 -Force` evicts the packed
      versions from `%UserProfile%\.nuget\packages` for the case where the same
      commit is repacked. `dotnet nuget locals http-cache --clear` does *not*
      cover this — a folder feed does not go through the HTTP cache.
- [x] Verified end to end: a scratch WinForms app referencing
      `CDS.ScriptChat.WinForms` `0.1.0-*` from the feed (no `ProjectReference`)
      restores, compiles against types from both packages, and runs.
- [ ] Remaining: open the packaged `ScriptChatPanel` in the VS 2026 Designer from
      a consuming project. The command-line check above proves compile and
      runtime, not design-time — and a panel that will not design is broken for
      the intended use.

## Phase 4 — Prepare the repo for going public

Do this consciously, not as a side effect of pushing.

- [x] **Audited** the full history and working tree for anything BYOK-sensitive
      (2026-08-14): `git log --all -p` grepped for API-key-shaped strings and
      known provider key prefixes (`sk-ant-`, `sk-proj-`, `AIzaSy`, `ghp_`,
      private-key PEM headers). Every hit is an obvious fake test fixture
      (`"sk-ant-not-a-real-key"`, `"sk-ant-super-secret-key-value"`, etc.) —
      nothing real. Also confirmed no `.csv`/`.log`/`TestResults`/`.vs` file was
      ever committed (`git log --diff-filter=A --name-only` across all history,
      filtered for those patterns — zero matches). Clean.
- [x] **Confirmed the logging removals** are done — see the blocker note above
      and D17. Nothing further needed here.
- [x] `README.md` added at repo root (quick start, architecture, screenshot,
      BYOK note — done in an earlier session). `LICENSE` (MIT) already present.
      Icon shown in the root readme carries the Flaticon attribution.
      **Still open**: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md` —
      not yet created; revisit before or shortly after the public flip.
- [x] GitHub repo `nooogle/CDS.ScriptChat.WinForms` already exists and matches
      the URLs baked into `Directory.Build.props` (confirmed via `gh repo view`).
      Currently private.
- [ ] Branch protection on `master` plus a required CI check — do this once
      `ci.yml` (phase 5) has run at least once so the `CI` check exists to
      select. Not yet done.

## Phase 5 — GitHub Actions: CI *(done, 2026-08-14)*

Superseded the draft plan that used to be here — built directly against
`C:\dev\CI-CD-STANDARDS.md`, the fleet-wide convention finalised after this repo
was deliberately excluded from the August rollout ("none are ready for public
release yet. Revisit once they are.") This is that revisit.

- [x] `.github/workflows/ci.yml` — `push`/`pull_request` to `master` +
      `workflow_dispatch`. `runs-on: windows-latest` (`net10.0-windows` +
      WinForms). `actions/setup-dotnet@v6` with `10.0.x`.
- [x] Test invocation confirmed **locally** against the actual MTP test
      projects before committing to the workflow shape: `dotnet run --project
      tests/<Project>.csproj --no-build --configuration Release -- --report-trx
      --results-directory TestResults`, run from the repo root, lands the
      `.trx` files at `./TestResults/` as expected — verified by actually
      running both (`CDS.ScriptChat.Core.Tests`: 66 passed;
      `CDS.ScriptChat.WinForms.Tests`: 77 passed) rather than assuming the path
      semantics of `dotnet run --project`.
- [x] TRX results uploaded as a workflow artifact (`actions/upload-artifact@v7`).
- [x] Added beyond the original phase 5 scope, per the standards doc:
      `dependency-review` job (PR-only; needs the "Dependency graph" repo
      setting enabled — **done**, confirmed by the user), plus separate
      `.github/workflows/codeql.yml`, `.github/workflows/scorecard.yml`, and
      `.github/dependabot.yml` (nuget + github-actions ecosystems).
- [ ] Optional `pack`-but-don't-push CI job — not added; `release.yml`'s pack
      step already runs on every tag push, judged sufficient for a two-package
      repo this size.

## Phase 6 — GitHub Actions: publish to NuGet.org *(workflow done, nuget.org side still open)*

- [x] `.github/workflows/release.yml` — triggered on `V*.*.*` tags (matches the
      `MinVerTagPrefix` already set in `Directory.Build.props`). **Named
      `release.yml`, not `publish.yml`** — the standards doc explicitly
      deprecates the latter name. Multi-package variant (packs, tests, SBOMs,
      and attests both `CDS.ScriptChat.Core` and `CDS.ScriptChat.WinForms`),
      adapted from `CDS.CSharpScripting2`'s worked two-package reference.
      Filename/version-extraction regex verified locally against a real
      `dotnet pack` output before committing (`CDS.ScriptChat.Core.1.0.1-*.nupkg`
      / `CDS.ScriptChat.WinForms.1.0.1-*.nupkg` — anchored patterns, no
      prefix-collision risk between the two package names).
- [x] **OIDC trusted publishing** wired (`NuGet/login@v1`,
      `permissions: id-token: write`, `environment: nuget`). The GitHub-side
      prerequisites — `nuget` environment and `NUGET_USER` secret — are
      **done** (created by the user, 2026-08-14).
- [x] SBOM generation (`dotnet-CycloneDX`, with the `-sv` version fix) and
      build-provenance/SBOM attestations, per the standards doc's supply-chain
      additions — not in the original phase 6 draft, added because they're free
      and the fleet-wide convention now includes them.
- [x] **Corrected (2026-08-14), checked against the real docs
      (`learn.microsoft.com/nuget/nuget-org/trusted-publishing`) rather than
      assumed**: the "chicken-and-egg" problem this item used to describe
      isn't real. A Trusted Publishing policy is scoped to
      **{repository owner, repository, workflow file, environment}**, not to a
      package ID — nuget.org's own docs: "The policy will apply to all
      packages owned by the selected owner." **No manual first-publish with a
      classic API key is needed.** Register the policy — Repository Owner
      `nooogle`, Repository `CDS.ScriptChat.WinForms`, Workflow File
      `release.yml`, Environment `nuget` — via nuget.org → username →
      **Trusted Publishing**, and the very first tag push can publish through
      OIDC directly. One nuance: policies against a still-private repo start
      in a 7-day "pending" state (restartable) until a successful publish
      confirms the repo/owner IDs — moot once the repo is public.
- [ ] Rehearse on a prerelease tag (e.g. `V1.1.0-preview.1`) before cutting a
      real one. Note: `V1.0.0` already exists as a tag (local and pushed) but
      is 8 commits behind current `master` (predates milestone 2 and the D17
      logging work) — the real release needs a fresh tag, not a re-push of
      `V1.0.0`.

### ID prefix reservation

`CDS.` is already used by unrelated publishers on nuget.org, so it cannot be
reserved — reservation requires the prefix be unambiguously associated with one
owner, and an existing third-party `CDS.*` package rules that out. The `CDS.`
package IDs themselves are still fine to publish (first-come, first-served);
what is lost is the exclusivity and the blue "reserved prefix" tick.

- [ ] Decide whether to pursue a **longer, distinctive prefix** that *is*
      reservable — e.g. `CarpeDiemSystems.` or `CDS.ScriptChat.` as a prefix in
      its own right. A prefix reservation can be granted for a multi-segment
      prefix, so `CDS.ScriptChat.` may be obtainable even though `CDS.` is not,
      provided no one else has published under it.
- [ ] If pursued: at least one package must already be published under the prefix
      before applying, so this happens *after* the first successful publish.
      Apply via "Reserved package ID prefixes" from the nuget.org account page
      (it opens a support request); expect a few days for human review. They
      assess owner association and whether the packages look legitimate — readme,
      description, project URL, licence — which is another reason to finish the
      icon and metadata items in phase 1b first.
- [ ] Either way, the practical defence without a reservation is to publish the
      IDs early (even as a prerelease) so nobody else takes
      `CDS.ScriptChat.Core` / `CDS.ScriptChat.WinForms`.
- [x] ~~Push both `.nupkg` and `.snupkg`~~ / ~~Create a GitHub Release from the
      tag~~ / ~~Rehearse on a prerelease tag~~ — leftovers from the original
      phase-6 draft, all done by the real `V1.1.0` publish on 2026-08-14 (see
      Status below). Ticked off 2026-08-24 so the list stops implying the release
      pipeline is unfinished.

---

## Next release — outstanding

**The last publish was `V1.4.0`** — nuget.org carries `1.1.0, 1.2.0, 1.3.0,
1.3.1, 1.4.0`, and all five tags are on the remote. *(This section previously
said `V1.1.0`, which was stale by four releases; corrected 2026-08-24 by querying
the nuget.org flat container and `git ls-remote --tags`.)*

**Everything unreleased is the Job 5 adoption work**: four commits since the
`V1.4.0` tag, plus the Playground-migration fix. Nothing else is outstanding.

**Cut this as `V1.5.0`, not `V1.4.1`.** It is additive rather than breaking, but
it is a large additive surface — `AddScript`, `UseStoredKey`, `ForHostApi`,
`HostApiIndex`, `RoslynSymbolResolver`, `RoslynSymbolLookupProvider`,
`MetadataCompilation`, `ScriptChatProviderPreference`, `SymbolLookedUpEventArgs`,
`HostOrientationResolver.ResolveForScript`, `ScriptChatPanel.ReadyStatus` — plus a
new package dependency (D22). A patch bump would understate all of it.

**`CDS.OpenCvSharpPlayground` (app and demo) currently reference
`1.4.1-alpha.0.4` from the `cds-local` feed**, not a released version. Bump both
`.csproj` files to `1.5.0` once this release goes out, or that repo stays pinned
to a local prerelease nobody else has.

- [ ] **`CDS.ScriptChat.Core` now depends on `Microsoft.CodeAnalysis.CSharp` 5.9.0**
      (D22). Non-breaking — no existing API changed — but it is a new transitive
      dependency and roughly +10 MB deployed, so it belongs in release notes
      rather than arriving unannounced. Version pinned to match what the
      consuming scripting hosts already load, so no diamond is introduced.
- [ ] **Behaviour change worth a release note**: a host that never wired a symbol
      provider is no longer offered `lookup_symbol` at all (D20). That is the fix,
      but it changes what the model is told.
- [ ] **New public API to mention**: `ScriptChatHostPanel.AddScript` /
      `UseStoredKey`, `ScriptChatSessionOptions.ForHostApi`, `HostApiIndex`,
      `RoslynSymbolResolver`, `RoslynSymbolLookupProvider`, `MetadataCompilation`,
      `ScriptChatProviderPreference`, `SymbolLookedUpEventArgs`,
      `HostOrientationResolver.ResolveForScript`,
      `ScriptChatPanel.ReadyStatus`.
- [ ] **Behaviour change worth a release note**: the status line now keeps
      `Ready · {provider} · {model}` instead of dropping to `Ready.` after the
      first turn or on a target switch, and `ScriptChatHostPanel` shows it at all
      (it previously only ever read `Ready.`). A host asserting on the literal
      text `"Ready."` would see this.
- [x] ~~**Do the Playground migration first**~~ — **done 2026-08-24**, against
      `1.4.1-alpha.0.4` from the local feed. 213 net lines deleted from the
      adopter; findings and the one API change they produced are written up in
      `todo.features.md`. Nothing found that blocks publishing.
- [ ] `PackageReleaseNotes` or a CHANGELOG (still open from phase 1b below).

## Decisions

- **Two packages.** `CDS.ScriptChat.Core` and `CDS.ScriptChat.WinForms` ship
  separately, keeping the Core/WinForms split of the design doc intact.
  **Reaffirmed 2026-08-24** after explicitly considering merging them into one.
  The case for merging was that nobody installs Core directly — both consumers
  reference `CDS.ScriptChat.WinForms` and get Core transitively — so the
  single-package *experience* already exists and the split is invisible at install
  time. It was rejected because the cost is real (moving ~26 files, a namespace
  choice that is either confusing or breaks every consumer's
  `using CDS.ScriptChat.Core;`, retargeting `Core.Tests` to `net10.0-windows`,
  reworking `release.yml`, and deprecating a live nuget.org package) and the gain
  is one fewer package name to think about. The split also earns its keep as a
  fence: Core cannot reference WinForms, so engine logic cannot drift into the
  panel. Revisit only if a headless, console, or non-WinForms consumer appears.
  (The original justification — "leaving room for a future WPF/Avalonia panel" —
  is *not* the reason to keep it, and was rejected as speculative.)
- **Multi-target `net48` and `net10.0`**, matching CDS.CSharpScript2 — see
  phase 1 for the work this pulls in.
- **`CDS.` prefix reservation is off the table** — the prefix is already in use
  by unrelated publishers, so nuget.org will not reserve it. See phase 6 for
  what remains possible.

---

## Not packaging — API feedback from a consuming host

Parked here because this is the file that gets read, not because it belongs to the
phases above. Raised 2026-08-11 from the OpenCvSharp Playground / Workbench
extraction; nothing has been acted on in this repo.

- [x] **A host with more than one script has to build the multi-target panel
      itself** *(done)*. Lifted into the library: `ScriptChatTarget` (in
      `CDS.ScriptChat.Core` — display name, the two delegates, and a
      `Func<ScriptChatSessionOptions>` factory), `ScriptChatHostPanel` (in
      `CDS.ScriptChat.WinForms` — `SetTargets(params ScriptChatTarget[])`, a
      `ComboBox` selector rather than the Playground's fixed pair of
      `RadioButton`s so it scales past two targets, one session per target
      sharing a single `IChatClient`), and `ScriptChatSettingsForm` (a `Form`
      wrapper around `ScriptChatSettingsPanel`, ported near-verbatim). The
      Playground migrated onto all three, deleting its local copies — see its
      `CLAUDE.md`. 9 new tests (`HostPanelTests.cs`, plus two in
      `DesignerSmokeTests.cs`), 86 in the WinForms suite.

- [ ] **No local or self-hosted provider.** `ScriptChatProvider` is
      `Claude | OpenAI | Grok`, and `ScriptChatClientOptions` has no base-URL
      override, so an OpenAI-compatible local endpoint (Ollama, LM Studio,
      llama.cpp) cannot be pointed at. Not a blocker for any current host — noted
      because a demo that runs without the user supplying a cloud key would be
      worth having, and D2 already confines provider knowledge to the enum and
      `ScriptChatClientFactory`, so the change is contained.

~~**Explicitly not a complaint about the design.** `ISymbolLookupProvider`'s D15
rule — define the abstraction, never implement it in-library — is exactly right…~~

**Superseded 2026-08-24 by D22.** That judgement was wrong, and the evidence was
already in this file: "a host with more than one script has to build the multi-target
panel itself" was fixed by lifting it into the library, and the symbol-lookup
adapter was exactly the same shape of problem — measured at ~473 lines per adopter
plus ~636 lines of Roslyn tooling to build first, with the 86-line adapter
duplicated verbatim inside one repo. Core now ships `RoslynSymbolResolver`,
`RoslynSymbolLookupProvider`, `MetadataCompilation` and `HostApiIndex`.
`ISymbolLookupProvider` stays public and unchanged for a host with its own engine,
and `SymbolLookupResult` being four plain strings is still what makes that work.

---

## Status

Phases 1b, 2 and 3 are **done** — `pack-local.ps1` produces both packages into
`C:\dev\localfeed` (registered as the `cds-local` NuGet source) and a scratch
WinForms app consumes them. Phase 1a (multi-targeting) is deliberately *not*
done: it is not needed for local testing, deferred past the first public
release (see decision under phase 1a above), and is a large enough change to
deserve its own session.

**2026-08-14**: Phases 4 and 5 are done, and phase 6 is done on the CI side —
the audit is clean, all five workflow files exist
(`ci.yml`/`release.yml`/`codeql.yml`/`scorecard.yml`/`dependabot.yml`), and the
`nuget` GitHub environment + `NUGET_USER` secret + Dependency graph are set up.

**First real run, verified against actual GitHub Actions output, not assumed**:
- [x] `ci.yml` on push to `master`: **green** — Build & Test job passed for
      real (66 Core + 77 WinForms tests, matching the local run exactly).
- [x] Dependabot opened its first PR within minutes (`Microsoft.NET.Test.Sdk`
      18.8.1 → 18.9.0) — its `ci.yml` Build & Test job also passed. Confirms
      the pipeline works end-to-end, not just on the first commit.
- [x] **Fixed one real bug, uncovered a second, unfixable-until-public one** in
      `scorecard.yml`. (1) The job's `permissions:` block omitted
      `contents: read` — public repos never surface this (anonymous checkout
      works regardless of token scope), which is presumably why it went
      unnoticed across the whole fleet rollout, since every repo there was
      already public by the time Scorecard ran. Fixed, and confirmed by
      re-running: `actions/checkout` now succeeds. (2) With checkout fixed, the
      action itself still fails — `scorecard had an error: ... githubv4.Query:
      Resource not accessible by integration`, and its own log confirms
      `Private repository: true`. Scorecard's checks (branch protection, etc.)
      need more GitHub API scope than the default `GITHUB_TOKEN` gets on a
      private repo; this is a genuine tool limitation, not something a workflow
      tweak fixes. Expect it to start working once the repo goes public — a
      third thing to confirm after the flip, not assume. **Both findings worth
      feeding back into `CI-CD-STANDARDS.md`** for any future repo that goes
      through this same private-repo-first sequence — flagged for the user, not
      changed unilaterally since it's a shared cross-repo file.
- [ ] `codeql.yml` and the `dependency-review` job in `ci.yml` both failed with
      the same root cause, and it isn't a workflow bug: *"Code scanning is not
      enabled for this repository"* / *"Please ensure that Dependency graph is
      enabled along with GitHub Advanced Security"*. Both features are free for
      **public** repos but gated behind (paid, per-seat) GitHub Advanced
      Security on private ones. Dependency graph is already on; expect both to
      start working once the repo goes public — but confirm after the flip
      (Settings → Security → Code security and analysis → GitHub Advanced
      Security may still need an explicit enable even on a public repo) rather
      than assuming.

**2026-08-14, later the same day — repo is public, all checks verified green
for real**:
- [x] Repo flipped to public by the user (confirmed via `gh repo view`).
- [x] Branch protection added on `master` (required check: `Build & Test`,
      force-push and deletion blocked) — API call that was denied by the auto
      mode classifier while private now succeeded once public.
- [x] Re-ran CodeQL and Scorecard against the same commit that failed while
      private (`49058c9`, the merged Dependabot PR): **both now succeed**.
      Confirms the diagnosis was right — pure GHAS/private-repo gating, no
      workflow bug. Dependency Review wasn't re-tested directly (no open PR at
      the time) but shares the identical GHAS gate CodeQL just cleared, so it's
      expected to clear on the next Dependabot PR.
- [x] First real version picked for the public launch: **`V1.1.0`** — minor
      bump from the existing (pre-public) `V1.0.0` tag, since milestone 2 added
      real backward-compatible functionality (OpenAI provider, UC2 multi-turn
      edit reconciliation).
- [x] nuget.org trusted publishing policy created (user).
- [x] **`V1.1.0` tagged, pushed, and released — first real publish succeeded
      end to end on the first attempt.** Every `release.yml` step green: build,
      both MTP test suites, pack, SBOM generation, build-provenance + SBOM
      attestations, GitHub Release (confirmed live with all 6 assets —
      `CDS.ScriptChat.Core`/`.WinForms` `.nupkg` + `.snupkg` + both `bom-*.json`),
      NuGet OIDC authentication, and the push to nuget.org itself. No manual
      API key, no partial-failure bootstrap run — the corrected understanding
      of trusted publishing (see the phase 6 entry above) held up in practice.
      This is the actual finish line for the whole packaging effort — Core and
      WinForms are real, installable NuGet packages now.
- [ ] `CONTRIBUTING.md`/`CODE_OF_CONDUCT.md`/`SECURITY.md` still outstanding
      from phase 4 — not blocking, worth doing soon.
- [ ] Consider the `CDS.ScriptChat.` prefix reservation now that a package is
      actually published under it (see the "ID prefix reservation" section
      above — needs at least one live package first, which is now true).
