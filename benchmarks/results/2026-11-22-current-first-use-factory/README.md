# B1340 Attribute factory work by execution phase

Profiled the retained `e961df912` source on November 22, 2026 UTC on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The 32-file
image is the retained B1335 candidate. Linux `perf` samples
whole worker batches after independent warmup. Warm and first-use
batches include captured output verification and prior-result disposal.
First use adds compilation and code-owner disposal. The scripts use
no guest filesystem operations. Startup and final worker teardown are
separate. These are attribution samples.

| Resolved sample category | Mixed warm | Mixed time to result | Mixed compile only | Fully supplied warm |
| --- | ---: | ---: | ---: | ---: |
| All process samples | 1,196 | 1,102 | 1,107 | 1,215 |
| Unmapped leaf | 70 | 80 | 183 | 84 |
| Compiler ancestry | 0 | 20 | 1,075 | 0 |
| Closest runtime method is reserve | 162 | 157 | 7 | 0 |
| └ Callable construction | 68 | 51 | 0 | 0 |
| └ Capture construction | 47 | 65 | 0 | 0 |
| └ Inline keyword-default admission | 37 | 33 | 0 | 0 |
| General binder ancestry | 37 | 22 | 0 | 0 |
| Native prefix and return transport ancestry | 540 | 515 | 0 | 128 |

Construction and storage admission still appear in the compile-to-result
batch. Compiler ancestry appears for 20 of 1,102 samples on this
script. The fully supplied helper resolves no reserve work. General
binder and native-prefix samples overlap other categories. Native
execution includes the actual loop and function bodies. The counts
establish where to look and cannot estimate removable CPU or
explain the earlier joint-admission candidate's first-use regression.

Choose the next factory redesign by whether it can displace retained
capture backing. The old stable-parameter variant established an
allocation opportunity and introduced a repeatable eager CPU cost.
A new shape should encode capture kind in the shared compiler facts
and remove runtime capture-kind discovery on the affected call path.
Preserve sibling identity, nonlocal writes, forwarding, namespace
observability and independent owners for escaping callables.

The first capture completed warm and compile-to-result. The initial
compile-only batch exhausted its count bound before accruing eight
seconds of compiler work. The second capture increased the compile-only
batch from 128 to 4,096 invocations and collected the missing phases.
Both catalog checks verify the same image. Both quiet gates
passed for each capture, workers joined and the archive verified under
lease before removing RAM-backed fixtures. The original incomplete
capture remains available. Two staging attempts refused before mutation
because the working-storage contingency could not be met. The admitted
staging retired only an exact working DLL retained in the pushed B1339
archive.

[Attribution](attribution.json) · [Initial observations](initial/observations.tgz) ·
[Completed phases](completion/observations.tgz) · [Procedures](procedures.tar.xz) ·
[Prior immutable-capture prototype](../2026-11-20-stable-parameter-captures/README.md).
