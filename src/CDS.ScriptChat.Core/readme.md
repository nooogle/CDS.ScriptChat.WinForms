# CDS.ScriptChat.Core

Provider-agnostic conversation engine behind the CDS.ScriptChat script+chat
panel, with Roslyn-backed symbol lookup so the assistant answers from your real
API rather than from recall. Built on `Microsoft.Extensions.AI.IChatClient`, so
the provider (Claude, OpenAI, Grok) is a configuration choice rather than a
dependency. No WinForms.

What it gives you:

- `ScriptChatSession` — the conversation, including the script the assistant is
  reasoning about.
- `ScriptEditProposal` / `ScriptDiff` — a full-script rewrite, surfaced as a
  diff for the host to accept or reject.
- `ScriptEditHunk` / `ScriptPatchApplier` — a targeted find/replace patch: one
  or more anchored old-text/new-text hunks, for a small, localised change
  instead of rewriting the whole script. A hunk applies only if its anchor
  matches the current script exactly once; otherwise it fails closed with a
  clear reason rather than guessing.
- Either way, proposals arrive as structured tool calls and are never applied
  automatically or parsed out of markdown fences.
- `ScriptChatSessionOptions.ForHostApi(typeof(MyGlobals))` — the batteries-included
  path. Name one type and you get both halves: an orientation index generated
  from it by reflection, and a working `lookup_symbol` resolved against your own
  assemblies. Because both come from the same type, what the assistant is told
  exists and what it can ask about cannot drift apart.
- `HostApiIndex` / `RoslynSymbolResolver` / `MetadataCompilation` — those two
  halves on their own, for a host that wants one and not the other.
- `RoslynSymbolLookupProvider` — also takes a live
  `Func<CancellationToken, Task<Compilation?>>`, if your editor already produces
  a compilation.
- `ISymbolLookupProvider` — still the hook by which a host with its own symbol
  engine answers instead. `lookup_symbol` is offered to the model only when
  something can actually answer it.

> Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in your app.
> Roslyn only finds an assembly's documentation when the `.xml` is deployed
> beside the `.dll`; without it every lookup returns a correct signature with no
> prose, which looks fine and isn't.
- `ScriptChatTarget` — describes one of a host's scripts (name, read/write
  delegates, session-options factory) for `CDS.ScriptChat.WinForms`'s
  `ScriptChatHostPanel` to drive a conversation per script.

For a ready-made WinForms UI on top of this, see **CDS.ScriptChat.WinForms**.

**Bring your own key.** The library never stores, logs or transmits an API key
anywhere except the provider SDK call itself.

MIT licensed.

Full docs, a screenshot, and the design record: [GitHub repo](https://github.com/nooogle/CDS.ScriptChat.WinForms).

## Credits

Package icon: [Seo and web icons created by Yogi Aprelliyanto — Flaticon](https://www.flaticon.com/free-icons/seo-and-web)
