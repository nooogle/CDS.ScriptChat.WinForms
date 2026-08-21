# CDS.ScriptChat

[![CI](https://github.com/nooogle/CDS.ScriptChat.WinForms/actions/workflows/ci.yml/badge.svg)](https://github.com/nooogle/CDS.ScriptChat.WinForms/actions/workflows/ci.yml)
[![CodeQL](https://github.com/nooogle/CDS.ScriptChat.WinForms/actions/workflows/codeql.yml/badge.svg)](https://github.com/nooogle/CDS.ScriptChat.WinForms/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/nooogle/CDS.ScriptChat.WinForms/badge)](https://securityscorecards.dev/viewer/?uri=github.com/nooogle/CDS.ScriptChat.WinForms)
[![NuGet: CDS.ScriptChat.WinForms](https://img.shields.io/nuget/v/CDS.ScriptChat.WinForms?label=CDS.ScriptChat.WinForms)](https://www.nuget.org/packages/CDS.ScriptChat.WinForms)
[![NuGet: CDS.ScriptChat.Core](https://img.shields.io/nuget/v/CDS.ScriptChat.Core?label=CDS.ScriptChat.Core)](https://www.nuget.org/packages/CDS.ScriptChat.Core)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)

**TL;DR:** A drop-in WinForms `UserControl` that lets a user chat with an LLM
about a script they're editing. The assistant answers questions and proposes
edits; edits always show up as a reviewable diff and never touch the buffer
until the user clicks Accept. Not tied to any scripting engine, editor
control, or AI provider — you supply two delegates and an API key, and it
works with Claude or OpenAI today.

![The CDS.ScriptChat test host, mid-review: a plain-TextBox editor on the left, the chat panel on the right showing a user turn and an assistant reply that proposed a one-line patch, rendered as a green/red diff with Accept edit and Reject edit enabled below it](assets/screenshot-diff-review.png)

*The bundled test host, caught mid-review. The assistant proposed a
one-line change — an anchored find/replace patch, not a full rewrite (see
[What it can do](#what-it-can-do)) — rendered as a diff with Accept/Reject
enabled until the user decides. `ScriptChatSettingsPanel` (top right) handles
BYOK onboarding; `ScriptChatPanel` (the transcript, diff, and input below it)
is the conversation itself. Every consuming app wires the same two controls.*

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
  actually exist instead of the model's best guess.
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
| [`CDS.ScriptChat.Core`](src/CDS.ScriptChat.Core/readme.md) | The provider-agnostic conversation engine. Built on `Microsoft.Extensions.AI.IChatClient`; no WinForms, no Roslyn. |
| [`CDS.ScriptChat.WinForms`](src/CDS.ScriptChat.WinForms/readme.md) | The ready-made WinForms UI: chat panel, settings panel, DPAPI key store. |

Each package's own readme (linked above) is what nuget.org shows — kept short
and focused on that one package. This document is the wider picture: what the
library is for, how the pieces fit together, and how to get started from a
clean checkout.

## Quick start

Add the panel to a form in the WinForms Designer (it's a standard
`UserControl`, so it drops in and resizes like any other), then wire it up in
code:

```csharp
// 1. Give the panel a way to read and write your script. This is the whole
//    editor contract — there's no interface to implement.
_chatPanel.ScriptTextProvider = () => _scriptTextBox.Text;
_chatPanel.ScriptTextSetter = script => _scriptTextBox.Text = script;

// 2. Configure it with a provider, model, and the user's own API key.
_chatPanel.Configure(new ScriptChatClientOptions
{
    Provider = ScriptChatProvider.Claude,
    ApiKey = apiKey,          // BYOK — never logged, cached, or stored by this library
    ModelId = "claude-opus-5",
});
```

That's enough for questions and diff-reviewed edits to work. For the BYOK
onboarding flow shown in the screenshot above — provider/model dropdowns, key
entry, a test-connection button, and persistence between runs — drop a
`ScriptChatSettingsPanel` alongside it and feed its `ConfigurationApplied`
event into `Configure`:

```csharp
_settingsPanel.KeyStore = DpapiApiKeyStore.ForApplication(appName, logger);
_settingsPanel.ConfigurationApplied += (_, e) => _chatPanel.Configure(e.ClientOptions);
```

To let the assistant answer questions about your own API accurately, implement
`ISymbolLookupProvider` and pass it in via `ScriptChatSessionOptions` — a
single `LookupAsync(symbolName, containingType)` method, answered however
suits your app (Roslyn, reflection, a hand-written table, a remote service).
Skip it and the library falls back to a no-op provider so everything else
still works.

The full worked example — including a concrete `ISymbolLookupProvider` and a
`scriptchat.context.md` orientation file — is in
[`samples/CDS.ScriptChat.TestHost`](samples/CDS.ScriptChat.TestHost/); it's
the fastest way to see every wiring point in one place. Launch it with
`--demo=patch` or `--demo=markdown` to see a seeded conversation without a
real API key — the same fixtures used to capture the screenshot above.

## How it works

```
┌────────────────────────────┐
│  Your host app             │  supplies: script get/set delegates,
│  (any editor, any engine)  │  ISymbolLookupProvider, orientation blurb
└──┬─────────────────────────┘
   │
┌──▼─────────────────────────┐
│  CDS.ScriptChat.WinForms   │  ScriptChatPanel (transcript, diff/accept UI)
│                            │  ScriptChatSettingsPanel (BYOK onboarding)
│                            │  DpapiApiKeyStore
└──┬─────────────────────────┘
   │
┌──▼─────────────────────────┐
│  CDS.ScriptChat.Core       │  ScriptChatSession (conversation, tool calls)
│                            │  ScriptChatClientFactory (Claude / OpenAI)
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
- **Nothing use-case-specific ships in the library.** No Roslyn, no
  Scintilla, no particular scripting engine — the script buffer is two
  delegates, and API knowledge is an interface you implement.
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

Milestones 1 and 2 are complete: single- and multi-turn conversations, Q&A,
symbol lookup, diff/accept edits, and BYOK onboarding all work end to end
against both Claude and OpenAI. Since then, targeted patch edits
(`propose_script_patch`) and a single continuously-scrolling transcript have
shipped too (D18, D19). Grok, mid-session provider switching, streaming
responses, and per-hunk accept/reject remain deliberately deferred — see
"Future milestones" in the design doc.

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
