# Native admission in the development regex adapter

An isolated adapter patch for Lython `d28266d7b` uses the immutable
0.5.0 Core and PythonRe packages from `61228b20f`. The package hashes
and the loaded assemblies match the producer receipt. Production
continues to pin 0.4.0.

The bridge replaces the adapter's coarse compilation and whole-window
estimates with producer admission. A compiled expression charges its
program owner, while one-shot matches and cursors charge their execution
owner. One aggregate reservation covers each owner's live claims and
keeps reusable capacity after partial release. The final release returns
that reservation. The adapter separately funds its copied name metadata.

Result snapshots stay charged through the copy of absolute byte ranges
and `lastindex`. Cursor construction forwards cancellation while the
creation call is active. Later advances use their own token through the
producer's advance overload. Refusal reports the producer's first failed
request and unwinds the active owner.

Windows .NET 11 and Ubuntu .NET 10 each pass 159 focused Release checks.
These include separate program and working budgets, first refusal,
retained captures and in-flight cancellation. The first run on each
platform passed 155 checks and failed two that expected the old coarse
reservation during cursor creation. The corrected tests exercise the
first actual working admission and verify that cleanup and reuse succeed.

Timing and wider qualification remain open. Final adaptation also needs
staged parser and compiler operations, compiled-program equality and
reversed anchored windows. The conservative retained capacity is part
of the current bridge and remains a target for measured refinement.

[Package and test identities](verification.json) · [Source and test logs](procedures.tar.xz).
