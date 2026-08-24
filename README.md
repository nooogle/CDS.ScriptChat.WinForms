# CDS.ScriptChat

[![CI](https://github.com/nooogle/CDS.ScriptChat.WinForms/actions/workflows/ci.yml/badge.svg)](https://github.com/nooogle/CDS.ScriptChat.WinForms/actions/workflows/ci.yml)
[![CodeQL](https://github.com/nooogle/CDS.ScriptChat.WinForms/actions/workflows/codeql.yml/badge.svg)](https://github.com/nooogle/CDS.ScriptChat.WinForms/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/nooogle/CDS.ScriptChat.WinForms/badge)](https://securityscorecards.dev/viewer/?uri=github.com/nooogle/CDS.ScriptChat.WinForms)
[![NuGet: CDS.ScriptChat.WinForms](https://img.shields.io/nuget/v/CDS.ScriptChat.WinForms?label=CDS.ScriptChat.WinForms)](https://www.nuget.org/packages/CDS.ScriptChat.WinForms)
[![NuGet: CDS.ScriptChat.Core](https://img.shields.io/nuget/v/CDS.ScriptChat.Core?label=CDS.ScriptChat.Core)](https://www.nuget.org/packages/CDS.ScriptChat.Core)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)

**TL;DR:** A drop-in WinForms `UserControl` that lets a user chat with an LLM
about a C# script they're editing. The assistant answers questions and proposes
edits; edits always show up as a reviewable diff and never touch the buffer
until the user clicks Accept. **Two calls to adopt it** — point it at your
script and name the type your scripts are written against, and it answers
questions about *your* API using real signatures and your own XML docs, not the
model's recall. Not tied to any scripting engine, editor control, or AI
provider; works with Claude or OpenAI today.

![The CDS.ScriptChat test host, mid-review: a plain-TextBox editor on the left, the chat panel on the right showing a user turn and an assistant reply that proposed a one-line patch, rendered as a green/red diff with Accept edit and Reject edit enabled below it](assets/screenshot-diff-review.png)

*The bundled test host, caught mid-review. The assistant proposed a
one-line change — an anchored find/replace patch, not a full rewrite (see
[What it can do](#what-it-can-do)) — rendered as a diff with Accept/Reject
enabled until the user decides. `ScriptChatSettingsPanel` (top right) handles
BYOK onboarding; `ScriptChatPanel` (the transcript, diff, and input below it)
is the conversation itself. This host wires both controls by hand to show them
separately; in an ordinary app `UseStoredKey` puts the settings dialogue behind
a button for you — see [Quick start](#quick-start).*

## Why

Any WinForms app with a script or code editing surface eventually gets the
same request: "let the user ask an AI about this." Building that well is more
work than it looks — prompt design, tool-call plumbing, rendering a diff
safely, an accept/reject flow that can't silently corrupt the buffer, BYOK
key handling, and keeping up with more than one provider. None of that is
specific to any one app or scripting engine, so `CDS.ScriptChat` builds it
once and stays host-agnostic: a consuming app supplies only what's genuinely
its own — how to read and write its script buffer, and how to answer "what
does this symbol look like" against its own API surface.

## What it can do

- **Ask questions** about the script open in the editor, with no code change
  implied — a plain text answer.
- **Propose an edit**, either as a full-script rewrite or as one or more
  targeted find/replace patches for a small, localised change — the model
  picks whichever fits. A patch's anchor text must match the script exactly;
  if it doesn't (or matches more than once), the tool call fails closed and
  the model retries with a better-scoped patch, rather than guessing at the
  wrong occurrence. Either way, the result renders inline as a diff and is
  applied only when the user clicks Accept — never parsed out of a markdown
  code fence, never applied silently.
- **Look up a real symbol** from your app's own API while reasoning about the
  script (`lookup_symbol`), so suggestions use APIs and signatures that
  actually exist instead of the model's best guess. This works out of the box
  from your own assemblies — you name one type and the library resolves against
  it, XML documentation included. The tool is only offered to the model when
  something can actually answer it (D20).
- **Carry a multi-turn conversation**, where each accepted edit becomes the
  baseline for the next turn, and a rejected edit doesn't quietly poison the
  model's memory of what the script currently looks like. Only one proposal
  is ever awaiting a decision at a time — sending another message is disabled
  until the current one is accepted or rejected, so a proposal can't get
  silently buried by later chat.
- **Onboard a user's own API key** (BYOK) — provider/model choice, key entry,
  a "test connection" button, and per-user storage via Windows DPAPI.
- **Drive more than one script from one panel**, via `ScriptChatHostPanel` —
  a target selector plus a shared conversation surface, for a host with
  several scripts open at once.

## Packages

This repository ships two NuGet packages, split so nothing AI/provider-related
ever leans on WinForms:

| Package | What it is |
|---|---|
| [`CDS.ScriptChat.Core`](src/CDS.ScriptChat.Core/readme.md) | The provider-agnostic conversation engine, plus Roslyn-backed symbol lookup so the assistant answers from your real API. Built on `Microsoft.Extensions.AI.IChatClient`; no WinForms. |
| [`CDS.ScriptChat.WinForms`](src/CDS.ScriptChat.WinForms/readme.md) | The ready-made WinForms UI: chat panel, settings panel, DPAPI key store. |

Each package's own readme (linked above) is what nuget.org shows — kept short
and focused on that one package. This document is the wider picture: what the
library is for, how the pieces fit together, and how to get started from a
clean checkout.

## Install

```
dotnet add package CDS.ScriptChat.WinForms
```

`CDS.ScriptChat.Core` comes with it — you don't reference it separately unless
you're building a non-WinForms host. Targets **.NET 10** (`net10.0-windows`).

You also need one line in your `.csproj`, and it is the single easiest thing to
get wrong:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

Roslyn only finds an assembly's documentation when the `.xml` is deployed beside
the `.dll`. Without it, every symbol lookup returns a correct signature with **no
prose** — which looks like it's working, and isn't.

## Quick start

### 1. Have a type your scripts are written against

Most apps that run C# scripts already have one — the "globals" object a script
sees without qualifying anything. If yours is a single flat API class, that
works too. Here's the one from the bundled sample, in full:

```csharp
/// <summary>
/// What a script sees without qualifying anything: the station's API, plus the
/// tolerance the current job is running to.
/// </summary>
public sealed class ScriptGlobals
{
    /// <summary>Gets the inspection station's API.</summary>
    public required InspectionApi API { get; init; }

    /// <summary>Gets the largest dimension, in mm, that still counts as a pass.</summary>
    public double UpperLimitMm { get; init; } = 12.5;

    /// <summary>Gets the smallest dimension, in mm, that still counts as a pass.</summary>
    public double LowerLimitMm { get; init; } = 11.5;
}
```

`InspectionApi` behind it is just ordinary code with ordinary XML docs —
`Measure(string partName)`, `Record(string partName, bool passed)`,
`Log(string message)`, `Parts`, `PassCount`, `FailCount`. **You register none
of it.** No attributes, no catalogue, no tool schema.

### 2. Drop the panel on a form and wire it up

`ScriptChatHostPanel` is a standard `UserControl`, so it goes on in the WinForms
Designer like anything else. Then:

```csharp
// 1. Point it at your script, and name the type from step 1.
//    That one type does two jobs — see below.
_chatPanel.AddScript(
    name:  "Inspection",
    read:  () => _scriptTextBox.Text,
    write: script => _scriptTextBox.Text = script,
    api:   typeof(ScriptGlobals));

// 2. Hand it the whole API-key story: load on startup, settings dialogue,
//    and remembering the provider and model the user chose. BYOK — the key is
//    encrypted under the current Windows account and never logged or cached.
_chatPanel.UseStoredKey("MyApp");
```

That's the integration. The panel starts switched off with a pointer at its
Settings button until the user enters their own key.

### 3. What the user sees

They type a request in plain English about the script that's open:

> *Log how far out of tolerance each failing part is.*

Behind that, the assistant already knows `Measure`, `Record` and `UpperLimitMm`
exist — the index generated from `ScriptGlobals` is in its system prompt. Before
using anything it isn't sure of, it calls `lookup_symbol` and gets back the real
signature **and your XML doc comment**. Then it proposes a change.

The proposal renders inline as a red/green diff with **Accept edit** /
**Reject edit** beneath it. Nothing touches `_scriptTextBox` until Accept is
clicked — and sending another message is disabled until the user decides, so a
proposal can't get buried under later chat.

### What `api: typeof(ScriptGlobals)` buys you

That single type is used twice, and the two uses cannot drift apart:

- **The orientation index.** Reflection over the type produces the list of what
  a script can reach, which goes into the system prompt. It can't fall behind
  your code, because it *is* your code.
- **`lookup_symbol`.** A metadata-only Roslyn compilation over your own
  assemblies answers the assistant's questions with real signatures and the
  XML documentation you already wrote — so it uses APIs that exist rather than
  ones that sound plausible.

Add a method to `InspectionApi` and the assistant knows about it on the next
run. There is nothing to keep in sync.

### Optional: a `scriptchat.context.md` beside your executable

The generated index says *what exists*. It can't say **why** — what these
scripts are for, house conventions a change should keep to, or the traps
particular to your app. Drop a markdown file beside your executable and it's
picked up automatically and placed above the index:

```
scriptchat.context.md              ← shared by every script
scriptchat.inspection.context.md   ← just the script named "Inspection"
```

A per-script file wins where it exists and falls back to the shared one, so a
host with several scripts writes one file until a script actually needs its own.
The name comes from what you passed to `AddScript`, lowercased with spaces
removed. Skip all of this and everything still works — the assistant just knows
less. It's a plain file, so you can tune the wording without a rebuild.

### The worked example

[`samples/CDS.ScriptChat.SampleApp`](samples/CDS.ScriptChat.SampleApp/) is an
ordinary app — a widget inspection station with a script editor, a documented
domain API, and exactly the two calls above. It runs its scripts too, so you
can see the whole loop: ask, review the diff, accept, run.

[`samples/CDS.ScriptChat.TestHost`](samples/CDS.ScriptChat.TestHost/) is a
different thing — a diagnostic harness with CSV logging and seeded
conversations. Launch it with `--demo=patch` or `--demo=markdown` to see a
conversation without a real API key (the fixtures behind the screenshot above).

### The manual path

If your host already has its own symbol engine — a live editor compilation,
say — implement `ISymbolLookupProvider` yourself and pass it via
`ScriptChatSessionOptions`, using the `AddScript` overload that takes a
session-options factory. `RoslynSymbolLookupProvider` also accepts a
`Func<CancellationToken, Task<Compilation?>>` if you have a live compilation
but would rather not write the adapter.

## How it works

```
┌────────────────────────────┐
│  Your host app             │  supplies: script get/set delegates,
│  (any editor, any engine)  │  and the type your scripts are written against
└──┬─────────────────────────┘
   │
┌──▼─────────────────────────┐
│  CDS.ScriptChat.WinForms   │  ScriptChatHostPanel (AddScript, UseStoredKey)
│                            │  ScriptChatPanel (transcript, diff/accept UI)
│                            │  ScriptChatSettingsPanel (BYOK onboarding)
│                            │  DpapiApiKeyStore
└──┬─────────────────────────┘
   │
┌──▼─────────────────────────┐
│  CDS.ScriptChat.Core       │  ScriptChatSession (conversation, tool calls)
│                            │  ScriptChatClientFactory (Claude / OpenAI)
│                            │  HostApiIndex + RoslynSymbolResolver
│                            │  MetadataCompilation (lookup_symbol)
│                            │  built on Microsoft.Extensions.AI.IChatClient
└────────────────────────────┘
```

A few decisions worth knowing before you integrate:

- **Edits are structured, not scraped.** Proposed code arrives via a
  `propose_script_edit` (full rewrite) or `propose_script_patch` (anchored
  find/replace hunks) tool call, rendered as a diff either way. The library
  never parses code out of the model's free-text response, and never touches
  your editor buffer until the user clicks Accept.
- **A patch is re-applied at accept time, not proposal time.** Accepting a
  patch reads your buffer fresh and re-anchors the hunks against it, so if
  the user edited the buffer while the proposal sat pending, a hunk that no
  longer matches fails with a clear message instead of silently overwriting
  that edit.
- **Nothing host-specific ships in the library.** No Scintilla, no particular
  scripting engine, no assumption about your editor — the script buffer is two
  delegates. Roslyn *is* included, but only to answer `lookup_symbol` out of
  your own assemblies; `ISymbolLookupProvider` stays an interface you can
  implement instead if you have your own engine (D22).
- **Switching provider or model resets the conversation.** There's no
  cross-provider history carryover — each configuration change starts a
  fresh session.
- **No prompt, script, response, or key is ever logged, cached, or sent
  anywhere except the direct provider SDK call.** This isn't an opt-in default
  — the capability doesn't exist anywhere in the library, including inside
  its own dependencies (see the design doc's D17 for how that's enforced).

For the full architecture, the decision log behind choices like these, and
current milestone status, see
[`cds.scriptchat.design.md`](cds.scriptchat.design.md) — it's the living
design record for this project and the best next read after this file.

## Status

**Working end to end, against both Claude and OpenAI**: multi-turn
conversations, Q&A about the open script, symbol lookup from your own
assemblies, full-rewrite and targeted-patch edits with diff/accept review, and
BYOK onboarding.

**Proven on a real adopter, not just the sample.** Before this library shipped
Roslyn support, the first consuming app (an OpenCvSharp image-processing
playground) hand-wrote ~473 lines of adapter and wiring — and had to build ~636
lines of Roslyn symbol tooling *first* to have anything to adapt. A new adopter
now writes neither. Migrating that app onto the current API deleted 213 net
lines and left only what was genuinely its own, and it is what shook out the
last round of API fixes.

**Not there yet**, and deliberately so:

| | |
|---|---|
| Grok | Enum value exists; the factory throws. Claude and OpenAI are wired. |
| Gemini, local/self-hosted models | No base-URL override yet. Local models are the most-wanted of these — see `todo.features.md`. |
| Streaming responses | A turn arrives complete, not token by token. |
| Per-hunk accept/reject | A patch is accepted or rejected whole. |
| Image/multi-modal input | Text only. |
| Non-WinForms UI | Core has no WinForms dependency, but no WPF/Avalonia panel exists. |

The scope is deliberately **C# script chat and nothing else**. General in-app
assistants, settings mutation, and MCP transports were each considered and
parked with the reasoning written down — see [`todo.features.md`](todo.features.md)
if you want to argue with it.

## Building from source

```
dotnet build CDS.ScriptChat.WinForms.slnx
```

Tests use the modern MSTest runner (Microsoft.Testing.Platform), so run them
as executables rather than via `dotnet test`:

```
dotnet run --project tests/CDS.ScriptChat.Core.Tests
dotnet run --project tests/CDS.ScriptChat.WinForms.Tests
```

To try the panel from another app on this machine before it's on NuGet.org,
`pack-local.ps1` packs both projects to a local feed — see
[`todo.packaging.md`](todo.packaging.md) for the current packaging and release
status.

## License

MIT — see [LICENSE](LICENSE).

Package icon: [Seo and web icons created by Yogi Aprelliyanto — Flaticon](https://www.flaticon.com/free-icons/seo-and-web)
