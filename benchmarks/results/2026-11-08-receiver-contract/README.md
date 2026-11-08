# Canonical receiver callable declarations

Baseline91f15a3e/core4c072fed; final source is87862247 plus the preserved R85 patch,
core `aa4acebe0df9a3dd1c29201134a422b4bf8a155fcb13dcbb694e685d59509150`.
Production inventory identifies the precommit build; informational SourceLink87862247.
Workers/optional0.4 adapter unchanged. Initial prototype6fdf3fdd is also preserved.

One ReceiverMethod declaration now serves immediate calls, exposed bound values
and existing native-return operations for casing/predicates/encoding and dictionary
views. Removed: both typed no-argument delegate target forms/accessors,
LoadCallReceiver/opcode factory/compiler name heuristic, redundant family routing,
text-semantics provider closures and unused wrappers. Existing native-body type
proof and lower/upper split fusion remain; no per-name native-proof marker or
eligibility expansion. The zero-argument calling-convention overload avoids empty
argument-memory transport within this existing contract. No new API, owner, pool,
configuration or backend. Dictionary views preflight and roll back construction.

Whole casefold/title helper reports allocate44.96–50.38%less sync and46.59–52.00%
less async; these ordinary operations now use the existing native workstate instead
of constructing lexical contexts. All complete stdout/files independently match
isolated CPython.373internal/2046public focused Release checks pass per actual
.NET11/.NET10, including metadata, binding, Unicode, dynamic lookup, aliases,
actual protocol suspension/cancellation, terminal refusal and budget reuse.
An initial minimum-only signature overload let invalid arguments through;17
existing public regressions detected it. Fixed zero-argument declarations/common
diagnostics restore those assertions unchanged. All failed/adverse originals remain.

B448: six jobs168.102seconds, quiet Ubuntu F4as_v7/.NET11RC3, normalGC/tiering,
two old/two candidate processes,5s warmup/role/job,seven rotated150ms rounds.
Ranges cover all four old/new process-median pairings:

| Complete script/API | Elapsed change | Interpretation |
| --- | ---: | --- |
| Casefold helper, sync | −24.26 to−22.90% | CoarseCPU−23.53to−21.57% supports direction |
| Casefold helper, async | −24.14 to−21.55% | CoarseCPU−27.91to−23.91%; candidate spread3.20% limits magnitude |
| Title helper, sync | −24.45 to−22.12% | CoarseCPU−25.86to−18.10% supports direction |
| Rsplit helper control, sync | −0.90 to+4.45% | Inconclusive; candidate spread3.85%, possible adversity retained |
| CSV control, sync | −0.38 to+2.70% | Possible regression retained; old spread1.71%, candidate1.35% |
| Report control, async | −4.14 to−2.78% | No per-mechanism or universal CPU attribution |

Retain the whole representation cleanup and affected-script benefit; keep control
uncertainty and earlier debt explicit. Initial6fdf desktop rsplit+6–7% motivated
the bounded fixed-arity refinement; all original/refined controls remain. Timing
uses completed I/O; actual suspension is independently tested. Includes public
invocation/mediated RAM I/O/full validation/prior-result cleanup, excludes compile,
startup/staging/reset/protocol/final cached teardown. No refreshed CPython, RSS,
embedding-tail or general CPU claim. Full retained-source qualification follows.

All source/core/auxiliary/actualReady/runtime/golden/quiet/Closed/exit/join/fixture/
archive gates pass. Workers join and owned RAM fixtures are removed/unmounted
before archival under lease and independent local verification.210s owner+30stop+
30margin. Atomic hardlink deduplication of one identical managed worker library
freed52,531,200bytes; all paths/bytes/cores/sources/archives remain, with local hash
proof. No runtime dependency change or evidence deletion.

[Assessment](assessment.json) and [complete evidence](observations.tar.xz) retain
exact sources/cores, initial functional failures, all adverse/partial observations,
independent goldens, source inventories, both-target logs and verified VM archive.
