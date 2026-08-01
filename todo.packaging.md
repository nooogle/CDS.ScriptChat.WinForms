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

## Phase 1 — Make the libraries produce packages

Both `src` projects ship: `CDS.ScriptChat.Core` (provider-agnostic engine) and
`CDS.ScriptChat.WinForms` (the panel). The `ProjectReference` from WinForms to
Core is emitted as a package dependency automatically, so no extra wiring is
needed there — but the two versions must stay in lockstep, which MinVer gives us
for free (phase 2).

### 1a — Multi-target `net48` + `net10.0`

Do this **first**, before anything else in this phase: it is the change most
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
- [x] `MinVerTagPrefix` set to lowercase `v`, and `MinVerMinimumMajorMinor` to
      `0.1`. Both workflow triggers in phases 5–6 must match the `v` prefix —
      CDS.CSharpScripting2 gets this wrong (props say `V`, `ci.yml` says `v*`,
      `publish.yml` says `V*`); do not copy that.
- [x] Verified on the untagged repo: version resolves to `0.1.0-alpha.0.3`,
      `FileVersion` `0.1.0.0`, informational version carries the commit SHA.
      `AssemblyVersion` is `0.0.0.0` — that is MinVer's default of
      `{major}.0.0.0` with major 0, not a misconfiguration.
- [ ] Confirm CI checks out with `fetch-depth: 0` and `fetch-tags: true` — MinVer
      silently falls back to `0.0.0-alpha.0` on a shallow clone. (Phase 5.)
- [ ] Sanity-check a real tag before the first release: `git tag v0.1.0` should
      build as exactly `0.1.0`.

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

- [ ] Audit the history and working tree for anything BYOK-sensitive: API keys in
      test fixtures, sample settings files, logs under `TestResults`, `.vs`.
      Per D3 no key should ever have been committed — confirm rather than assume.
      A key found in *history* means rewriting history or starting a fresh repo,
      so check before the first public push.
- [ ] Confirm the logging removals listed under "What must be removed or turned
      off before release" in `cds.scriptchat.design.md` are done.
- [ ] Repo hygiene files, copying CDS.CSharpScripting2's set: `README.md`
      (badges, quick start, BYOK note), `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`,
      `SECURITY.md`. `LICENSE` (MIT) is already present. If the root `README.md`
      shows the package icon, it needs the Flaticon attribution too — see
      `assets/readme.md`.
- [ ] Create the GitHub repo `nooogle/CDS.ScriptChat.WinForms` — the URLs are
      already baked into `Directory.Build.props`, so they must match.
- [ ] Branch protection on `master` plus a required CI check (phase 5).

## Phase 5 — GitHub Actions: CI

- [ ] `.github/workflows/ci.yml` — build + test on every push and pull request.
      **Do not copy CDS.CSharpScripting2's trigger**: that file is named "CI" but
      only fires on `v*` tags, so it never guards a normal commit. Trigger on
      `push` to `master` and on `pull_request`.
- [ ] `runs-on: windows-latest` (non-negotiable — `net10.0-windows` + WinForms).
- [ ] `actions/setup-dotnet@v4` with `10.0.x`; NuGet package cache keyed on
      `**/*.csproj` + `**/*.slnx`.
- [ ] Test invocation: the test projects are **Microsoft.Testing.Platform**, so
      use `dotnet run --project tests/<Project>` per the house convention rather
      than `dotnet test` (which has VSTest integration issues on .NET 10).
      `TestingPlatformDotnetTestSupport` is set, so `dotnet test` *may* work —
      decide once, in CI, and use the same command locally.
- [ ] Publish the TRX results as a workflow artifact; consider a test-summary
      action so failures are readable from the PR page.
- [ ] Optional: a `pack` job on CI that packs but does not push, so packaging
      breakage is caught on every commit rather than at release time.

## Phase 6 — GitHub Actions: publish to NuGet.org

- [ ] `.github/workflows/publish.yml` triggered on `v*` tags (matching the MinVer
      prefix from phase 2), based on CDS.CSharpScripting2's version.
- [ ] Use **OIDC trusted publishing** (`NuGet/login@v1`) rather than a long-lived
      API key. Requires: `permissions: id-token: write`, a GitHub `environment`
      (the reference calls it `nuget`), a `NUGET_USER` secret, and a trusted
      publishing policy configured on nuget.org for this repo/workflow.
- [ ] First publish of a brand-new package ID cannot use a trusted publishing
      policy scoped to an existing package — either push the first version
      manually with an API key, or create the policy scoped to a package *pattern*
      (e.g. `CDS.ScriptChat.*`). Check which before tagging.

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
- [ ] Push both `.nupkg` and `.snupkg`, with `--skip-duplicate`.
- [ ] Create a GitHub Release from the tag (`softprops/action-gh-release@v2`,
      `generate_release_notes: true`) with the packages attached.
- [ ] Rehearse on a prerelease tag (e.g. `v0.1.0-preview.1`) before cutting
      `v0.1.0`.

---

## Decisions

- **Two packages.** `CDS.ScriptChat.Core` and `CDS.ScriptChat.WinForms` ship
  separately, keeping the Core/WinForms split of the design doc intact and
  leaving room for a future WPF/Avalonia panel.
- **Multi-target `net48` and `net10.0`**, matching CDS.CSharpScript2 — see
  phase 1 for the work this pulls in.
- **`CDS.` prefix reservation is off the table** — the prefix is already in use
  by unrelated publishers, so nuget.org will not reserve it. See phase 6 for
  what remains possible.

## Status

Phases 1b, 2 and 3 are **done** — `pack-local.ps1` produces both packages into
`C:\dev\localfeed` (registered as the `cds-local` NuGet source) and a scratch
WinForms app consumes them. Phase 1a (multi-targeting) is deliberately *not*
done: it is not needed for local testing and is a large enough change to deserve
its own session.
