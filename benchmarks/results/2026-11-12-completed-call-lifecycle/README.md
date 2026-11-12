# Completed executable call lifecycle — retained for delivery

2026-11-12. Five-file runtime migration over fully qualified `82ed92d4` production
(unchanged in40369b33). Frozen candidate `0e715d19`, reference `0106a711`, with
precommit metadata; full production source inventories identify the measured code.
Full platform/stress/installed-package qualification is pending separately.
Publication and tagging remain postponed. No public API or resource-policy change.

## Removed state and transitions

Calls now bind and start their existing executable body at the call instruction.
Completed deferred prefixes publish directly after binding/workspace/admission
cleanup. `_pendingFunction`, `_pendingReceiver`, `BeginPendingCall`, the frame
runner's preparation branch and its completed-call resume cycle are gone.
Constructors and raw type-subscription/getter protocols share this call entry.
Native borrowed/inline binding and prepared native helper execution remain intact.

A ValueTask<ExecutionContext?> transition carries actual pending binding or a real
child to the existing driver. No new owner, reverse caller link, outcome object,
controller, arena or duplicate interpreter. Actual children retain source/completion
metadata; their result/error staging is necessary for parent async disposal before
publication. It remains funded through actual user completion and joined cleanup.
Async call and operation error routing is shared, with the original source span
saved before a completion can advance its instruction. No rebinding/replay after
effects, hidden limit, fuel, forced GC or tracing is added.

## Focused validation

Final Release passes **1,507 internal / 4,962 public cases per actual .NET11/10**,
12,938 total, on unchanged frozen source. Tests cover native/ordinary/default/
keyword/captured calls, constructors/type protocols, handler errors, real host-I/O
suspension, cooperative cancellation, admission reentry/first terminal refusal,
retained aliases, joined cleanup, budget reuse and managed stack/recursion.
Existing independent semantic coverage is retained; no architecture-only test added.
The solution/probe rebuild takes28.68s with zero warnings/errors before final tests.
Initial signature-wiring compiler errors and intermediate first-pass logs survive;
those intermediate logs do not qualify the final source. All final logs identify
actual target/runtime and frozen source hashes.

Seven complete scripts, both APIs and separate compilation, preserve full stdout,
file hashes, allowance balances and fixture cleanup through the existing worker.
Physical allocation is effectively flat. Desktop uses64warmups/image and five
rotated rounds of8jobs, normal GC/tiering and shared CoreLib; elapsed is diagnostic.
Adverse capturedAsync+5.24..+5.62%(0.36%control), reportAsync+4.43..+14.40%(9.54%control)
and JSONRun+2.56..+10.69%(7.34%control) remain. No CPU claim is inferred from allocation.

## B605 quiet source pair

Nine targeted jobs finish in**251.294s**. Existing Azure F4as_v7 / Ubuntu24.04.4 /
.NET11RC3, fixed `fec79afd` worker and normal optional adapter0.4. Two independent
processes per image, five-second warmups and seven rotated150ms rounds. Ranges
cover four median pairings, not confidence intervals. Negative means less time.

| Complete script / API | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Guarded helper search / Run | -10.60..+1.73% | -6.55..-2.31% |
| Guarded helper search / RunAsync | -8.61..-6.75% | -7.20..-1.46% |
| Native helper search / Run | -4.62..-3.22% | -5.42..-1.15% |
| Ordinary text / Run | -3.62..-0.82% | -3.10..-1.56% |
| Captured text / RunAsync | -4.70..+1.03% | -4.17..+6.25% |
| Report / Run | -2.51..+3.41% | -4.41..+6.67% |
| Report / RunAsync | -6.52..+3.75% | -4.00..+3.50% |
| CSV / RunAsync | -2.63..+2.76% | 0..+3.45% |
| Guarded helper compilation | -1.62..+0.77% | -1.92..+0.89% |

Guarded async, native Run and ordinary Run have supporting CPU improvement.
Synchronous guarded elapsed remains uncertain: unchanged reference control8.74%
versus3.71%candidate, though its CPU pairings are negative. Captured/report/CSV and
compile remain variable; controls/IQRs are preserved in b605/summary.json.

Warm clocks include public invocation, mediated RAM-file I/O, full output/file
validation and prior-result cleanup; exclude startup/compilation, fixture reset/
staging, protocol and final cache disposal. Wider CPU includes protocol/reset.
Desktop includes fixture reset and immediate result disposal. Different boundaries
and collections cannot establish a cause for an adverse result or a general gain.
No updated CPython ratio, physical-heap/RSS bound or embedding-tail claim is made.

All loaded-image/source/output/file/allowance/quiet-before-after/join/RAM/archive
gates pass, archive independently SHA-verified and terminal cached. Superseded
c982 core aliases and B604 raw/catalog duplicates were retired only after exact
committed canonical Git/SHA and fresh idle lease, UID/path/inode/all-hardlink proof.
The current0106 reference and candidate stay protected; OS/evidence floors unchanged.

## Retention and remaining work

Retain the simpler state/control contract and measured affected-script improvement.
Accept additional captured/report/CSV CPU uncertainty versus82ed at this milestone,
with an explicit review/acceptance before publication. Earlier82ed debt versus2ec
has a different reference; do not compound ranges or multiply source-pair gains
into CPython ratios. Original adverse desktop results remain intact; the quiet
observations do not retrospectively invalidate them. No unchanged retiming campaign.

Complete one retained-source Windows/Ubuntu actual11/10, original stress and installed
Release-consumer checkpoint, then continue the broader runtime roadmap. This does
not finish the whole-script performance objective. [Decision](decision.json).
Current external comparisons remain in [BENCHMARKS](../../../docs/BENCHMARKS.md).
