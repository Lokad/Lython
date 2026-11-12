# JSON tokens over existing immutable storage — R279 retained

2026-11-12, qualified41bd869b/frozenf0b736b2 versus prototype4f57ca33 overb3574b5c.
All568 source hashes are frozen; two production files change. Informational
metadata is not relabelled as a postcommit/CI image. Normal optional adapter0.4,
fixedfec79 worker and all existing sources/fixtures/goldens remain unchanged.
Full retained-source platform/stress/installed-consumer delivery follows; publication
is held. This is a bounded storage milestone, not completion of the performance goal.

Plain non-inline JSON strings can reuse same-owner immutable input. R271 already
supplied the array/offset/tag window representation; no wrapper field/accessor
shape/hash policy, parser, owner, ledger, pool or option is added. An owned source
transfers its backing to command lifetime using its existing storage tag; its
shell can then refund without releasing the full capacity/header. Admission funds
the new shell and32B header at the first transition, before allocation/publication.
Unowned/other-owner, escaped and inline text still copies. Earlier R54/R60 global
representation costs and [withdrawal evidence](../2026-11-07-array-json-windows-withdrawn/README.md)
remain relevant, but this is a different boundary. Making the storage tag mutable
could affect JIT costs; controls are measured rather than assuming zero overhead.

Conservative tradeoff: a small native alias can retain a large input/full spare
capacity until bounded command cleanup. Public CLR projection can independently
copy strings and end the guest lifetime. No arbitrary-reference reclamation,
tracing, forcedGC, retry or hidden allowance is introduced.

## Focused correctness and complete scripts

Release nonincremental solution build passes. Final254internal/891public checks
per actual.NET11/10 (2,290) pass: Unicode, strict errors/original offsets, property
keys, hooks, true suspension/late cancellation, source-shell cleanup, spare
capacity, nested windows, external/other-owner copies, terminal refusal before
publication and retained projected aliases/shared allowance reuse.

Seven existing complete search/guarded-search/CSV/captured-text/JSON/UTF8-report/
UTF16-report scripts pass both APIs, compilation and compile-to-result, independent
stdout/file goldens, normal runtime, image isolation, shared balances and joined
fixture cleanup. Warm report allocation falls4.68%UTF8 and4.46..4.49%UTF16; fresh
falls4.29..4.57%. Other warm/compile allocation is effectively flat. These are
physical CLR allocation observations, not guest-heap/RSS measurements.

All desktop clocks survive: async UTF8report+3.05..3.76%/UTF16+6.11..6.86% with
controls0.69/0.71%, and compilation control drift up to75.54%. They motivated a
targeted async confirmation, not a failed-design declaration. No desktop CPU gain.

Initial new public fixtures incorrectly expected full guest input retained after
CLR projection and supplied unowned host input, which takes the copy path. Corrected
file-input cases assert projected aliases/funding and reuse; native cases prove
backing lifetime directly. Original two failures remain. A nullable test-return
compile error, first launcher directory-vs-DLL mistake and invalid hash-witness
source all stopped before affected measurements and remain with corrections. The
existing driver/protocol is reused; no benchmark framework is introduced.

## Controlled source pair and targeted confirmation

Quiet AzureF4as_v7/Ubuntu24.04.4, four EPYCvCPUs/16GiB/no swap, actual.NET11RC3,
normalGC/tiering. Two independent processes/image, seven rotated500ms rounds;
8swarmup reports/JSON,12scaptured,5sCSV. B635 uses the identical cores/profile and
only the three async jobs answering the original desktop adversity.

| Complete warm script / API | Elapsed change | Wider processCPU change | Reference / candidate spread |
| --- | --- | --- | --- |
| B634 UTF8 report / Run | −5.66..−3.03% | −2.32..−1.20% | 2.67/0.04% |
| B634 JSON-lines / Run | −2.32..+3.01% | −3.30..+3.55% | 3.79/1.45% |
| B634 captured-text control / Run | −1.97..−1.34% | −1.99..−1.14% | 0.45/0.19% |
| B634 CSV control / Run | −2.59..+0.05% | −4.72..+1.05% | 2.10/0.60% |
| B635 UTF8 report / RunAsync | −3.57..−2.49% | −2.46..−0.62% | 0.73/0.38% |
| B635 UTF16 report / RunAsync | −3.04..+1.99% | −1.15..+0.48% | 3.84/1.28% |
| B635 CSV control / RunAsync | −1.87..+0.44% | −0.08..+2.00% | 1.68/0.67% |

Negative means less time. Ranges span four process-median pairings, not confidence
intervals. Retain copy removal/allocation reduction and observed UTF8report elapsed
with modest supporting CPU. Favorable unchanged text and moving references limit
attribution; do not label all elapsed improvement removable CPU. UTF16/JSON/CSV
CPU remains uncertain. Accept that measurement/control debt and conservative
retention explicitly. Original desktop adversity is not erased or causally solved.
No further unchanged retiming is needed for this decision; useful new evidence can
justify refinement. No extrapolated CPython ratio or general CPU/equivalence claim.

B634four jobs208.984s under300sowner/30sstop/30smargin; B635three143.230s under
240sowner/30sstop/30smargin. All core/auxiliary/Ready/golden/zero-balance worker exit,
quiet endpoints, Closed/exit0/join, RAM unmount and persistent/local archiveSHA
gates pass. Terminal states cached; no users/sole RAM evidence remain. No prior
evidence/image was removed or OS240MiB/evidence12MiB floor reduced. Final free
324,067,328B. Original raw sources/identities/maps of earlier work stay separate.

Warm clocks include invocation, mediated RAM I/O, full validation and prior-result
cleanup; exclude compile/startup/reset/protocol/final caches. Wider processCPU
includes reset/protocol/GC/JIT. Completed-I/O RunAsync is not pending-I/O latency.
RSS, embedding tail impact and broad large-file throughput remain unqualified.
