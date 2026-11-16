# Shared replacement construction: initial two-pass prototype

R455/B858, 2026-11-16 UTC. Private source at 9ee6552d plus the archived patch;
qualified be176ed0/frozen642f5115 reference, candidate903bf146. The same 32-file
worker image differs only in core. Normal optional adapter remains 0.4.0.
**Do not promote this initial shape.** A single-pass refinement is independent
changed-source work; these observations remain its original adverse condition.
The [retained refinement](refined/README.md) records its separate measurements
and accepted debt; it does not inherit this prototype's allocation reduction.

The prototype replaces duplicate str/bytes builders with shared measurement and
direct-final writing. No-op receiver aliases avoid new storage. It removes growth
and the shrinking-byte final copy, but searches matches twice. The complete-script
comparison establishes a CPU cost that the allocation reduction does not justify.

| Complete workload | Phase | Allocation diagnostic | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: | ---: |
| Text normalization, 4,096 lines / 52,224 B | Warm | −0.14% | +5.72..9.35% | +5.49..9.16% |
| Same text script | Compile and run once | Not measured separately | −0.87..+1.84% | −1.99..+2.75% |
| Binary normalization, same input bytes | Warm | −9.80% | +15.02..15.40% | +9.35..12.01% |
| Same binary script | Compile and run once | Not measured separately | +9.35..11.96% | +4.48..5.79% |
| Ordinary positional helper/range, 16,384 calls | Warm control | Flat | −2.76..+2.39% | −1.71..0.00% |

Changes combine the two candidate and two reference process medians, not confidence
intervals. Reference/candidate controls move 2.74/0.68% for warm text,
0.20/0.14% for warm binary, 1.66/0.72% for fresh binary and 4.13/1.11% for calls.
Maximum per-process IQR is 1.47% for binary and 6.44% for calls. Text fresh is
inconclusive; binary adversity is clear. No causal claim about the prior runtime
debt, CPython gap, or exact removable CPU share follows.

Quiet Azure F4as_v7, Ubuntu24.04.4, four EPYC9V45 vCPUs, 16GiB/no swap,
.NET11RC3 natural workstation GC/normal tiering. Five jobs finish in 148.522s;
four independent workers/job, 4s full-request warmups, five rotated approximately
0.5s batches. Warm includes mediated RAM I/O, complete output/file checks and
prior-result cleanup; compilation/startup, fixture reset/protocol and final cache
teardown are excluded. Fresh adds compilation and compiled-owner cleanup. Process
CPU has a wider boundary including reset/protocol. This is a source pair, not a
CPython timing refresh or a heap/RSS/embedding-tail qualification.

Both actual Windows .NET11/10 targets pass 779 public /99 internal focused cases,
including cancellation, terminal refusal, retained aliases, actual asynchronous
protocols and shared-budget reuse. Both frozen workers match CPython on six scripts.
The two new bounded no-op cases fail with SharedMemoryAllowance on the qualified
reference and pass here through both execution APIs. An initial oversized-output
MemoryError differed from the existing RuntimeError; code was corrected without
relaxing the test. Original sources/failure remain archived. No full platform or
package qualification applies to this private prototype.

Allocation uses the unchanged natural-GC diagnostic harness: three four-run blocks
after warmup, complete I/O/reset/checks/result cleanup, no forced collection and no
local CPU claim. Numeric/CSV/call controls are flat. Every run restores its shared
compilation budget baseline and compiled disposal restores zero. Peaks remain
conservative ownership balances, not live CLR heap measurements.

All users were joined; full evidence was archived and SHA-verified under the VM
lease before fixture teardown. Only six exactly proved committed working replicas
were retired, after fresh ownership/hash/all-alias checks. Canonical evidence,
toolchains and selected images remain. Original preparation path correction and
locally refused premature observation are preserved; neither changed a measurement.

[Decision and all pairings](decision-initial.json), [exact image/source inputs](identity.json),
[complete sources](candidate-sources.tar.xz), [catalog](catalog.json.gz),
[observations](observations.tgz), [focused checks](focused-checks.tgz),
[procedures](procedures.tgz). Publication remains held.
