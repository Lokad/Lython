# Ordinary control flow through asynchronous hosting — 2026-10-16

Nine ordinary control-flow instructions now call their existing synchronous
handler directly under RunAsync. Their async helper's generic forwarding route
is deleted. Conditions whose values are not Python instances also use the same
synchronous truth kernel already used for those values. Custom instance truth
slots and chained comparisons retain their existing asynchronous path. No
compiler policy, stack limit, owner, operator kernel or awaiter framework is added.

| Engine | Measured revision | Warm branch-loop median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 6a4a236b4bbe7c5283d6a6cb313a37e57e42c3ed | 6.514 ms | 0.55% |
| Lython after | f8d87288c1fdbe7ec8ce86251d66ed3c07bd4459 | 5.980 ms | 1.39% |
| CPython | 3.13.16 | 1.0136 ms | 0.40% |

The candidate median is **8.2% lower in one short, unqualified diagnostic**.
There is no confidence interval, qualified CPython ratio, allocation count,
complete-job or embedding-latency claim. The remaining CPython gap is substantial.
These revisions do not describe later HEAD. AE's earlier integer-dispatch result
is a separate source pair; percentages must not be combined.

The unchanged `loops.branch.large` case executes 16,384 iterations with modulo,
comparison, branch, addition/subtraction and prints `-44725590\n`. Its source is
119 UTF-8 bytes. Independent goldens at 256, 2,048 and 16,384 iterations are checked
for both engines before timing, using the existing catalog and a closed-form sum.
Actual case, source hash, size and warm-async lane match before and after capture.
The initial setup selected the file-only representative profile and stopped at
its catalog assertion before any timing. Selecting the existing full profile and
filtering the same declared cases corrected setup; one timing capture followed.
[Original setup history](setup-history.json) is preserved.

Boundary: warm public RunAsync with reused compiled code and completed host
operations, fresh invocation state, execution/projection and prior-result cleanup
timed. Last-result ownership survives through its response. Compilation, worker
startup, transport and full response hashing are excluded. No files are staged.
Lython limits, cooperative admission and cancellation stay enabled; optional fuel
is unset. CPython has no equivalent in-process containment. Real suspension is
validated separately, without injecting I/O delay into the ratio.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, 16 GiB RAM, no swap; SDK 10.0.401/runtime 10.0.12. Workers keep ordinary
GC/tiering. CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO. Supervisor
tiering alone is disabled. Seven rounds span 7.136s under a 30s group cap. One
capture, no retry/retuning/new producer/long lane. All 524 production sources per
role match frozen Git manifests; comparison tooling/backend are identical. Seven
workers are gone, relevant services inactive and VM lease free.

Validation: 2,738 ordinary white-box tests pass (18.447s including cleanup), 96
focused boundary/iterator checks (1.275s), 122 public truth/return/protected-flow/
terminal-admission checks (4.919s), and 57 original stack/attribution/copy checks
(8.035s). All 11 saved fuel/effect outputs match byte-for-byte. New direct if
conditions await truth descriptors/bodies and stop on cancellation. Paused native
iterator disposal completes before outer finally side effects on return, break
and fault. Touched delayed fixtures now cancel/drain started executions and dispose
scripts/results deterministically. Original deadlines, 300-frame traceback,
100/600-copy fixtures and the managed stack policy are unchanged.

The common deferred cursor-disposal queue, per-instruction pre-checks, exception
routing and retained funding are unchanged. Chained-comparison result observation
has a separately tracked source asymmetry; this change leaves that path intact
and does not claim its contract impact has been established.

[Samples](warm-async.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[baseline guards](baseline-guards.json), [candidate guards](candidate-guards.json)
and [local validation](local-proof.json) preserve evidence. Frozen archives,
LF-validated scripts and detailed local receipts remain in private notes.
