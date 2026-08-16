# CDS.ScriptChat.Core

Provider-agnostic conversation engine behind the CDS.ScriptChat script+chat
panel. Built on `Microsoft.Extensions.AI.IChatClient`, so the provider
(Claude, OpenAI, Grok) is a configuration choice rather than a dependency.
No WinForms, no Roslyn.

What it gives you:

- `ScriptChatSession` — the conversation, including the script the assistant is
  reasoning about.
- `ScriptEditProposal` / `ScriptDiff` — proposed edits arrive as structured tool
  calls and are surfaced as a diff for the host to accept or reject. They are
  never applied automatically and never parsed out of markdown fences.
- `ISymbolLookupProvider` — the hook by which a host exposes its own API surface
  to the assistant. The library ships only the interface.
- `IScriptChatHostContext` — host-supplied description of the app the script
  runs in.
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
