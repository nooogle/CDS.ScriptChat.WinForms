# TODO — Packaging, versioning & release

NuGet/CI/release mechanics. Distinct from `todo.features.md` (new capabilities)
and `todo.bugs.md` (defects).

**The pipeline is built and working.** Both packages are live on nuget.org
(`1.1.0` → `1.4.0`), published by `release.yml` on a `V*.*.*` tag push via OIDC
trusted publishing, with SBOMs and build-provenance attestations. CI, CodeQL,
Scorecard and Dependabot all run green, and `pack-local.ps1` stages prereleases
to the `cds-local` feed for host apps to try first.

> **Completed phases have been removed from this file** (2026-08-24) so it shows
> only what is outstanding. Phases 1b–6 — packaging, MinVer, the local feed,
> going public, CI, and the publish workflow — all shipped between 2026-08-14 and
> now. The full write-ups, including the trusted-publishing corrections and the
> private-repo Scorecard/CodeQL findings, are in git history
> (`git log -p -- todo.packaging.md`).

---

## Releasing now — `V1.4.2`

Two packages ship, always together and always on the same version (MinVer
derives both from the one tag):
**`CDS.ScriptChat.Core`** and **`CDS.ScriptChat.WinForms`**.

**Version: `V1.4.2`** (decided 2026-08-24). The last publish was `V1.4.0` —
nuget.org carries `1.1.0, 1.2.0, 1.3.0, 1.3.1, 1.4.0`, all five tags on the
remote. `1.4.1` is skipped deliberately: that number is already in use by the
`1.4.1-alpha.0.N` prereleases sitting in the `cds-local` feed, and reusing the
line invites confusion between a local build and a real release.

**How to cut it**: merge to `master`, then tag and push `V1.4.2`.
`release.yml` fires on the `V*.*.*` tag and does the rest — build, both MTP
suites, pack, SBOMs, attestations, GitHub Release, OIDC auth, push to
nuget.org. No manual API key, no separate pack step.

**What is in it** — seven commits since `V1.4.0`: the Job 5 adoption work
(Roslyn `XmlFileDocumentationProvider`, the sample app, per-script orientation
context files, docs), the status-line fix found by the Playground migration, and
a Dependabot bump of `Anthropic` 12.42.0 → 12.44.0.

**Note for the release notes**: this is a patch-numbered release carrying a
substantial *additive* API surface — `AddScript`, `UseStoredKey`, `ForHostApi`,
`HostApiIndex`, `RoslynSymbolResolver`, `RoslynSymbolLookupProvider`,
`MetadataCompilation`, `ScriptChatProviderPreference`, `SymbolLookedUpEventArgs`,
`HostOrientationResolver.ResolveForScript`, `ScriptChatPanel.ReadyStatus` — plus
a new dependency (D22). Nothing is breaking, so the number is safe; the notes
just have to carry the weight the version number doesn't.

**After publishing**: `CDS.OpenCvSharpPlayground` (both the app and the demo)
references `1.4.1-alpha.0.4` from the `cds-local` feed, not a released version.
Bump both `.csproj` files to `1.4.2`, or that repo stays pinned to a local
prerelease that exists on no other machine.

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
- [ ] `PackageReleaseNotes` or a CHANGELOG — open since the first publish; there
      is no `CHANGELOG.md` in the repo and neither csproj sets
      `PackageReleaseNotes` (verified 2026-08-24).

---

## Multi-target `net48` + `net10.0` — deferred, still wanted

**Decision (2026-08-14): deferred past the first public release.** Going
public and shipping a first NuGet release doesn't require it — it's a
substantial, separate chunk of work (see the breakdown below) unrelated to the
CI/publish mechanics. Ship `net10.0`/`net10.0-windows`-only first; revisit if
a consuming host actually needs `net48`.

Do this **first** within this job, *if and when it is picked back up*: it is the
change most likely to force API changes.

**Dependency viability, re-checked 2026-08-24** against what is actually
referenced today (the earlier note cited `Anthropic` 12.39.0 and
`Microsoft.Extensions.AI` 10.8.3, both since bumped). Every dependency still
offers a down-level target, so nothing here is blocked:

| Package | Version | Ships |
|---|---|---|
| `Anthropic` | 12.44.0 | `net8.0`, `net9.0`, **`netstandard2.0`** |
| `Microsoft.Extensions.AI` (+ `.OpenAI`) | 10.9.0 | `net10.0`, **`net462`**, `net8.0`, `net9.0`, **`netstandard2.0`** |
| `Microsoft.CodeAnalysis.CSharp` | 5.9.0 | `net10.0`, **`netstandard2.0`** |
| `CDS.Markdown.Lite` (WinForms only) | 1.5.5 | needs checking — not verified |

The Roslyn row is the new one: `Microsoft.CodeAnalysis.CSharp` arrived in Core
*after* this section was written (D22), so it had never been assessed for a
`net48` leg. It ships `netstandard2.0`, so it does not block one.

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

---

## ID prefix reservation

`CDS.` is already used by unrelated publishers on nuget.org, so it cannot be
reserved — reservation requires the prefix be unambiguously associated with one
owner, and an existing third-party `CDS.*` package rules that out. The `CDS.`
package IDs themselves are still fine to publish (first-come, first-served);
what is lost is the exclusivity and the blue "reserved prefix" tick.

**The prerequisites are met now, which they were not when this was written.**
Both IDs are published and owned, so the defensive "publish early so nobody else
takes the names" step is done, and the metadata a reviewer looks at (icon,
per-package readme, description, project URL, MIT licence) is all in place.

- [ ] Decide whether to pursue a **longer, distinctive prefix** that *is*
      reservable — e.g. `CarpeDiemSystems.` or `CDS.ScriptChat.` as a prefix in
      its own right. A reservation can be granted for a multi-segment prefix, so
      `CDS.ScriptChat.` may be obtainable even though `CDS.` is not, provided
      nobody else has published under it.
- [ ] If pursued: apply via "Reserved package ID prefixes" on the nuget.org
      account page (it opens a support request); expect a few days for human
      review, which assesses owner association and whether the packages look
      legitimate.

---

## Small leftovers

Survivors of the completed phases — each verified 2026-08-24 as still genuinely
open, rather than carried over on trust.

- [ ] **`CONTRIBUTING.md` / `CODE_OF_CONDUCT.md` / `SECURITY.md`.** None of the
      three exists, at the repo root or under `.github/` (checked). Not blocking
      anything, but the repo is public and Scorecard notices.
- [ ] **Open the *packaged* `ScriptChatHostPanel` in the VS 2026 Designer** from a
      consuming app, not just the in-solution project. The Playground does host it
      in `MainForm.Designer.cs` off the NuGet package and builds clean, which is
      good evidence — but actually opening the designer surface is the check that
      was never done by hand.

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
- **Multi-target `net48` and `net10.0`**, matching CDS.CSharpScript2 — see the
  multi-target section above for the work this pulls in.
- **`CDS.` prefix reservation is off the table** — the prefix is already in use
  by unrelated publishers, so nuget.org will not reserve it. See "ID prefix
  reservation" above for what remains possible.

---

