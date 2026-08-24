# CDS.ScriptChat.WinForms

A drop-in WinForms script+chat panel: the user talks to an LLM about the C#
script they are editing, and the assistant can propose edits to it.

Two calls to adopt it:

```csharp
_chatPanel.AddScript(
    name:  "Inspection",
    read:  () => _scriptTextBox.Text,
    write: script => _scriptTextBox.Text = script,
    api:   typeof(ScriptGlobals));   // drives the orientation index AND lookup_symbol

_chatPanel.UseStoredKey("MyApp");    // key store, settings dialogue, and restore
```

- `ScriptChatHostPanel` — the control to drop in. `AddScript` once per script,
  `UseStoredKey` once. It reaches the host's script buffer through
  caller-supplied delegates, so it works with any editor control (Scintilla, a
  plain `TextBox`, anything) and depends on none of them.
- `ScriptChatPanel` — the inner chat UI, for a host that wants to drive
  sessions itself.
- `ScriptChatSettingsPanel` / `ScriptChatSettingsForm` — provider, model and API
  key configuration, as a panel or a ready-made dialogue. `UseStoredKey` wires
  these up for you.
- `DpapiApiKeyStore` — per-user API key storage via Windows DPAPI. Swap in your
  own via `IApiKeyStore`.

> Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in your app,
> or `lookup_symbol` returns correct signatures with no documentation.

Proposed edits — a full rewrite or a targeted find/replace patch — are shown
as a diff and applied only when the user accepts. Only one proposal is ever
awaiting a decision at a time.

**Bring your own key.** The key is stored per-user under DPAPI and is never
logged or transmitted anywhere except the provider SDK call itself.

Builds on **CDS.ScriptChat.Core**, which holds the provider-agnostic
conversation engine.

MIT licensed.

Full docs, a screenshot, and the design record: [GitHub repo](https://github.com/nooogle/CDS.ScriptChat.WinForms).

## Credits

Package icon: [Seo and web icons created by Yogi Aprelliyanto — Flaticon](https://www.flaticon.com/free-icons/seo-and-web)
