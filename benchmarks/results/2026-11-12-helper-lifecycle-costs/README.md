# Ordinary helper lifecycle costs — 2026-11-12

Fully qualified `82ed92d4` production, unchanged in9906efc8 evidence; exact frozen
`0106a711` core, common workerfec79afd and optional adapter0.4. Quiet F4as_v7 /
Ubuntu24.04.4/.NET11RC3. Two six-second perf/JIT windows use actual loaded maps/code.
No production prototype, elapsed comparison or publication.

| Complete script / Run | Guest CPU seconds | Guest leaf samples | Unknown |
| --- | ---: | ---: | ---: |
| Ordinary guarded search | 4.99 | 986 | 40 |
| Native helper search | 4.55 | 898 | 42 |

Guarded search associates175 leaves with shared dispatch,47 with binding fill,
34 with ordinary binding,37 local-result completion,31 bound-call entry,30 workstate
construction,18 pending-call entry and17 operand-storage construction. Native search
associates134 with dispatch,70 native binding and68 native proof. These labels
include useful/inlined work; sample counts and CPU windows have different completed
invocations and are not additive removable shares or predicted speedups.

Source confirms ordinary helpers already share workspace and execute their deferred
prefix without constructing a lexical context. The two complete scripts differ by
only~88B/command allocation after R256. The remaining ordinary helper route also
stages pending-function/receiver/site state, enters the frame runner, binds, creates
workstate, executes the completed prefix, then resumes the parent. Native helpers
can complete directly from the call opcode. Do not attribute the gap to absent
per-call heap allocation, missing UTF8, or scalar boxing already removed.

Next coherent source milestone: consolidate completed helper-call preparation,
execution and publication. Preserve native operand/inline-binding wins; let only
actual pending binding or child execution transfer into the existing frame runner.
Remove displaced completed-call staging/resume rather than add reverse caller
links, controller/arena, outcome objects, a duplicate dispatcher or a narrow
unary-only rescue for this one script. The source gate must name actual removed
state/control; timing decides magnitude. Preserve defaults/keywords, closures,
receiver protocols, reentry/snapshots, real I/O, errors/order, cancellation/terminal
refusal, managed stack and awaited ownership cleanup. Temporary regression debt
needs an explicit delivery decision. No further unchanged profile collection.

Both full stdout/file/allowance/loaded-image and quiet-before/after gates pass.
All workers joined,37 payload SHAs verified while persistent, owned RAM removed,
terminal cached. Instrumented execution clocks total10.404s; guest CPU boundaries
differ. Raw host/reset/protocol stacks, all samples and unknowns remain. No CPU
attribution from elapsed or claim that an entire method can disappear.

Only B603 raw/catalog and old B474 raw duplicates were retired after exact committed
canonical Git, fresh idle lease, UID/path/inode/all-hardlink proofs. Both selected
core generations remain protected; OS240MiB/evidence12MiB floor unchanged. Source
qualification is reused, not repeated for this unchanged-source investigation.

[Assessment](assessment.json), [raw/maps/code](observations.tgz),
[identities](source-identity.json), [receipt](archive-receipt.json) and
[preparation](preparation.tar.xz). Current CPython results remain in
[BENCHMARKS](../../../docs/BENCHMARKS.md).
