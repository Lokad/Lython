# Known scalar boundaries for Unicode captures

R422,2026-11-16UTC. Retain refined **8f468f0a** versus fully-qualified
**c023042d /0393331a**. Only core scalar projection changes; normal released
PythonRe0.4, optional adapter, worker and complete auxiliary images stay identical.
No unpublished producer API, new backend, public API, coordinate cache, owner,
memory policy or work quota.

One prepared operation retains its original window bounds. Select the nearest
known byte/scalar position from the current cursor, origin, subject end and
distinct window endpoints. Replaces the origin-only reset heuristic; full-window
anchors are compared once. Existing sorted coordinates/bidirectional cursor
remain. Captures before Pos, reversed windows, empty/unmatched groups and original
subject aliases retain their semantics. Cancellation checks stay within active
scans; cursor publication follows successful projection. Admission/capture arrays/
pending users and joined cleanup retain their existing ownership. The128B operation
estimate covers the retained window metadata; physical allocation adds16B/invocation.

For zero-width matches at i with a suffix capture [i,n], the prior core algorithm
walks the suffix for each result. The new algorithm uses the known end coordinate
and advances the previous whole-match cursor locally. This removes the quadratic
core projection walk for that shape, not arbitrary far-capture work or backend
matching/staging complexity. All requested captures are still validated and
projected. Do not infer universal linear regex behavior.

## Complete real-adapter diagnostics

Quiet Azure F4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs/16GiB/no swap,.NET11RC3 normal
tiering/natural workstationGC,2026-11-16. Existing checked worker, two processes/
source,5s full-request warmups/five rotated~0.5s batches. All scripts construct a
literal subject, iterate actual normal-adapter captures and print independently
derived count/span checksums. No file I/O; identical empty fixture maps. Growing
Unicode suffix lookahead and ASCII/short Unicode controls; diagnostic workload,
not the primary representative CPython table or isolated projection CPU.

| Whole script / warm | Elapsed change | Wider process CPU change | Reference/candidate spread |
| --- | ---: | ---: | ---: |
| Unicode suffix1,024scalars |−10.15..−5.31%|−11.11..−5.56%|4.88/0.48%|
| Unicode suffix4,096scalars |−5.20..−4.94%|−6.45..−4.92%|0.10/0.17%|
| ASCII suffix4,096scalars |−0.86..+0.13%|−1.72..+1.75%|0.79/0.20%|
| Unicode short4,096scalars |−1.56..−0.53%|−2.17..−0.23%|0.12/0.92%|

All four process-median pairings, positive means slower, not confidence intervals.
B818completes153.252s. The smaller Unicode reference moves4.88%; magnitude is
uncertain. ASCII retains an adverse CPU pairing. [Initial B817](initial/README.md)
keeps Unicode short up to4.72%slower with3.70%candidate spread and every original
source/condition. Changed-source refinement removes duplicate anchors; separate
collections do not prove its isolated causal contribution or erase that debt.

Warm includes Run, actual backend work, complete stdout/file validation and
prior-result cleanup; excludes startup/compilation/reset/protocol/finalcache teardown.
WiderCPU includes reset/protocol. No CPython timing, fresh/startup, pending-I/O,
contention, heap/RSS or embedding-tail claim. Existing0.4 captured-text staging still
allocates large suffixes; clocks do not isolate its CPU share. Local natural-GC
allocation is effectively flat: suffix cases+16B/invocation, short−40B in the shorter
diagnostic versus+16B initially. Byte noise is not an allocation reduction claim.

## Validation and delivery

Nonincremental Release solution build and64internal/363public regex checks each
actual.NET11/10 pass. Existing in-flight provider and backward-scan cancellation,
terminal refusal, retained aliases/caches, budget reuse and joined cleanup checks
remain. Two new complete programs independently agree with CPython3.13.2, covering
full/restricted Unicode windows, suffix/lookbehind groups, empty/unmatched captures,
purge and original retained subjects. Four diagnostic checksums also agree.

All image/golden/budget/quiet/Closed/exit/join/archive gates pass. Evidence is
persistently archived/hash-verified under lease before teardown; no users/RAM remain.
Only initialdcf's exact committed archived working replica is retired after fresh
UID/path/hash/link/all-alias proof. Fully-qualified039and new8f images/toolchains
remain;254/252MiB floors unchanged. Terminal cached forever. Initial driver fixture
failure and long-run timeout remain under initial, with missing cleanup witness
explicit; completed shortened allocation runs retain all output/ownership assertions.

[Full platform/stress/installed-package qualification passes](delivery/README.md); publication held.
Broader non-regex call/operand/collection/frontend work remains independent.
[All medians](summary.json),[allocation](allocation-summary.json),
[focused checks](focused-checks.json),[identity](identity.json),
[raw](observations.tgz),[source/binaries/procedures](source-checks-and-procedure.tar.xz).
