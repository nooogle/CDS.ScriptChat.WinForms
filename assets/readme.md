# Assets

## icon.png

The NuGet package icon for both `CDS.ScriptChat.Core` and
`CDS.ScriptChat.WinForms`. 256×256 PNG, referenced from each `src` project via
`PackageIcon` — do not duplicate it per project.

**Attribution is required by the Flaticon free licence** and must appear wherever
the icon is used. It is currently carried in both package readmes; add it to the
root `README.md` too if the icon ends up shown there.

Markdown form (used in the readmes — nuget.org sanitises raw HTML):

```markdown
Package icon: [Seo and web icons created by Yogi Aprelliyanto — Flaticon](https://www.flaticon.com/free-icons/seo-and-web)
```

Original HTML form as supplied by Flaticon:

```html
<a href="https://www.flaticon.com/free-icons/seo-and-web" title="seo and web icons">Seo and web icons created by Yogi Aprelliyanto - Flaticon</a>
```

If the icon is ever replaced, remove the attribution along with it.

## screenshot-diff-review.png

Screenshot of `samples/CDS.ScriptChat.TestHost` at its default window size,
used in the root `README.md`. Captured via `--demo=patch` (see
`Demo/PatchDemo.cs`), which seeds one canned turn proposing a one-line
find/replace patch against the starter script — no real prompt or response
content, no provider key needed. Shows the transcript, the rendered diff, and
the Accept/Reject bar enabled.

`--demo=markdown` (`Demo/MarkdownDemo.cs`) is the other seeded fixture, used
by `MarkdownTurnRenderingTests` — it renders prose and a table but wraps
awkwardly at this window's default width, so it wasn't used for the README
screenshot. Re-capture `screenshot-diff-review.png` if the test host's layout
changes enough to make it stale.
