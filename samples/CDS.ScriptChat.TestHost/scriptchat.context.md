These scripts drive **Kestrel**, a proprietary machine-vision toolkit. A script is handed a
`KestrelFrame` named `frame` and must return a `KestrelFrame`. Pipeline components are fetched
from the workspace with `Workspace.Acquire<T>(slot)`, where slot names come from the app's
`.kestrel` manifest rather than from code.

Kestrel is internal and is not documented publicly, so you will not have seen it before. Do not
guess its API from naming conventions in other imaging libraries — the names and parameter
rules are deliberately unlike them. Call `lookup_symbol` for every Kestrel type or method you
intend to use, and follow exactly what it returns.
