# Original-subject regex window diagnostic

Measured 2026-10-30, Release .NET 11 RC3 on Windows x64 / Intel Core i7-14700KF.
Control `10d77eda` slices the subject before preparing it. Candidate `2796d2e2`
passes the original subject and scalar window, correcting anchors/lookbehind and
absolute capture coordinates. Both use the real optional PythonRe/Core 0.4.0
adapter. [Exact revisions, assembly hashes and raw counts](measurements.json).

| Case, window 1024..3072 of 4096 scalars | Managed bytes / 100 searches before | After |
| --- | ---: | ---: |
| 4,096-byte ASCII subject | 326,400 | 108,000 |
| 12,288-byte BMP/supplementary subject | 4,470,400 | 6,933,600 |

The Unicode allocation regression remains visible. Correct original-subject
context covers more text; the 0.4 adapter still builds a dense scalar-coordinate
map and uses detailed APIs with materialized capture data. Removing the segment
copy alone does not establish lower total allocation. Published native projection
and range APIs remain separate integration work. There is no elapsed-speedup or
CPython-ratio claim, and this does not update the primary comparison table.

The frozen [driver](Window.cs) compiles one dot pattern and prepares immutable
input before sampling; it warms twenty searches, then samples one hundred using
the current-thread allocation counter. Each search crosses the real bound-method,
provider preparation, matching and native result-copy boundaries. It checks full
subject content, match span and value before/after sampling, drains
runtime resources and verifies the shared allowance returns to zero. No startup,
compilation or public CLR projection is sampled. Admission remains active with an
unbounded shared root.

Reproduce as a Release net11.0 console named Lokad.Lython.WhiteBox.Tests,
referencing each recorded source revision's core and adapter projects. Build/run
each revision separately with the pinned SDK and backend; verify emitted hashes.
The driver targets these revisions' internal interfaces, not future HEAD APIs.
