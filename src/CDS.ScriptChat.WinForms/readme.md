# CDS.ScriptChat.WinForms

A drop-in WinForms script+chat panel: the user talks to an LLM about the script
they are editing, and the assistant can propose edits to it.

- `ScriptChatPanel` — the chat UI. It reaches the host's script buffer through
  caller-supplied delegates, so it works with any editor control (Scintilla,
  a plain `TextBox`, anything) and depends on none of them.
- `ScriptChatSettingsPanel` — provider, model and API key configuration.
- `DpapiApiKeyStore` — per-user API key storage via Windows DPAPI. Swap in your
  own via `IApiKeyStore`.

Proposed edits are shown as a diff and applied only when the user accepts.

**Bring your own key.** The key is stored per-user under DPAPI and is never
logged or transmitted anywhere except the provider SDK call itself.

Builds on **CDS.ScriptChat.Core**, which holds the provider-agnostic
conversation engine.

MIT licensed.

## Credits

Package icon: [Seo and web icons created by Yogi Aprelliyanto — Flaticon](https://www.flaticon.com/free-icons/seo-and-web)
