# Sample — adding AI script chat to an ordinary app

A widget inspection station with a C# script editor. It exists to show what adopting
`CDS.ScriptChat` actually costs in an app that was not built around it.

**The whole integration is two calls**, in `MainForm.InitializeChat`:

```csharp
_chatPanel.AddScript(
    name:  "Inspection",
    read:  () => _scriptTextBox.Text,
    write: script => _scriptTextBox.Text = script,
    api:   typeof(ScriptGlobals));

_chatPanel.UseStoredKey("CDS.ScriptChat.SampleApp");
```

Everything else in the sample is the app's own business — running the script, showing its output.

## What those two calls do

`AddScript` takes **one** type, `ScriptGlobals`, and uses it twice:

- **The orientation index.** Reflection over the type produces the list of what a script can
  reach, which goes into the system prompt. It cannot fall behind the code.
- **`lookup_symbol`.** A metadata-only compilation over this assembly answers the assistant's
  questions with real signatures and the XML documentation on `InspectionApi`.

Because both come from the same type, what the assistant is told exists and what it can ask about
cannot drift apart.

`UseStoredKey` handles the whole API-key story: loading the user's key at startup, opening the
settings dialogue when they ask for it, and remembering the provider and model they chose. The key
is encrypted under the current Windows account and never leaves the provider SDK call.

## Two things to copy into your own app

**Set `GenerateDocumentationFile`.** Roslyn only finds an assembly's documentation when the `.xml`
is deployed beside the `.dll`. Without it every lookup returns a correct signature with no prose —
which looks fine, and is not. See the note in the `.csproj`.

**Write XML doc comments on your API.** They are what `lookup_symbol` hands back, and they are the
difference between an assistant that writes correct scripts and one that guesses plausibly.

## Optional: `scriptchat.context.md`

Deployed beside the executable and picked up automatically. It carries the prose the generated
index cannot — *why* these scripts exist, and the conventions a change should keep to. Delete it
and the assistant still works; it just knows less.

## Running it

Bring your own key — nothing ships with the sample. Build and run, then use the chat panel's
**Settings** button to choose a provider and enter a key. Then try asking:

> *Log how far out of tolerance each failing part is.*

The assistant proposes a change, you see it as a diff, and nothing touches the editor until you
click **Accept**.
