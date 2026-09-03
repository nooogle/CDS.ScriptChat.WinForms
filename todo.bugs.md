# TODO — Bug backlog

Defects in current behaviour. Distinct from `todo.features.md` (new
capabilities) and `todo.packaging.md` (NuGet/CI/release mechanics).

---

**No open bugs.**

Most recently closed: the transcript not scrolling to the end when a new prompt
was sent. Fixed upstream rather than here — there is no scroll-handling code in
this repo — by the move to one continuously-appended transcript (D18/D19) and
the `CDS.Markdown.Lite` `1.5.4` → `1.5.5` bump that went with it.

Note that `todo.features.md` → "Known issues" carries two smaller defects that
are tracked there rather than here: a flaky mouse-wheel UI test, and markdown
tables rendering badly at the chat panel's default width.
