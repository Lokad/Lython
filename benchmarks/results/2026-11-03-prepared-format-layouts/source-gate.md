# B160 immutable format layout preparation

Source selected after B159 full-script profiles. No context/interpreter merger:
lexical contexts can escape while execution buffers must end. No binding-only
prototype: its dominant CPU impact is unproven. Prepare constant f-string native
grammar once within the existing executable operation and shared parser. This
removes per-field scans, width/precision substring objects and parsed descriptors
for static specs. Dynamic specs and other public formatting APIs still parse at
the actual operation. Custom/date formatters receive raw text; invalid grammar
must reach their real call, not become a compile error. Pure preparation catches
only grammar/value/representation failures, never admission/cancellation/OOM.
There is no lazy cache, extra owner/budget, alternate evaluator or policy switch.
The existing coarse compilation estimate funds immutable metadata; active native
formatting still uses current execution admission and cancellation.

Implementation shares one parser and formatter through optional internal prepared
metadata. Complex formatting keeps immutable with-copies; standalone and appended
fields preserve identity, error order and user protocols. No speculative output
capacity or CPU claim. Broader complete activation remains open.

Independent CPython3.13.2 goldens cover cross-type reuse, conversion, date/complex,
custom raw malformed/oversized specs and native runtime errors. Same compilation
runs sync/delayed async repeatedly with borrowed budget returning to baseline.
Focused actual .NET11/.NET10:826 public and59 internal per target PASS. Existing
protocol cancellation/refusal and cleanup controls remain. All builds/tests joined
before freezing. Baseline1634 has fully qualified B158 runtime; nonincremental
build precedes mutation. Worker and ordinary adapter auxiliary bits normalized;
only core DLL/PDB differ. Timing includes report warm/compile-run/compile-only
and CPython, text/CSV/JSON controls, explicit RAM filesystem. Allocation uses the
unchanged complete-job tool with report and three controls. Retain all adverse
conditions; bounded correction/refinement allowed. Decide architecture, allocation
and CPU separately; remove prototype if it duplicates machinery or has unjustified
regression debt. Do not treat one formatting slice as the full performance goal.
