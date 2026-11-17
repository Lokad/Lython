# Managed bulk scalar and UTF-16 widths

R483/B899,2026-11-17UTC. Retained, full delivery qualification pending. One
production file on50b33e17, frozena95ff2a4, compared with qualifiedd8611c85/
frozen0315de0a. Exact585production/2381build-test inputs and copied32file worker
hashes archived. Actual5case workers agree with CPython3.13.2 before staging;
Windows actual.NET11/10 each63internal/49public focused checks pass.

Valid UTF-8 scalar count is bytes minus continuation bytes; UTF-16 adds astral
leads. Safe managed Vector<byte> masks/population counts and a scalar tail replace
per-scalar width walking and chunk-boundary adjustment. Cancellation remains at
4KiB boundaries and completion. No per-string state, owner/copy/framework,
unsafe/pointer/intrinsic API or dependency. Indexing still uses GetRuneLength.
Projection admission/first refusal, retained aliases and budget cleanup remain.

The first SearchValues/CountAny implementation is discarded; stable dense
adversity and original failures remain in [first evidence](../first/README.md).
This is one justified changed-source refinement, not unchanged retiming.

| Complete workload / lane | Elapsed change | Wider process CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| scripts.text-normalization.large / warm | -9.72..-7.58% | -8.95..-5.34% | 2.33/0.04% |
| scripts.text-normalization.large / compile-run | -5.77..-2.85% | -5.07..-2.17% | 0.01/3.09% |
| scripts.text-normalization.helper-list / warm | -5.47..-2.72% | -5.05..-2.29% | 1.92/0.96% |
| scripts.report-render.large / warm | -3.49..-1.49% | -3.49..-1.92% | 1.56/0.50% |
| scripts.text-normalization.ascii / warm | -8.38..-1.72% | -6.61..-1.48% | 7.17/0.09% |
| scripts.text-normalization.dense / warm | -8.16..-1.73% | -7.51..+0.72% | 6.77/0.22% |

Original text, helper text and report improve with CPU support. ASCII/dense
reference movement limits precision; denseCPU spans−7.51..+0.72%, not a precise
gain or adverse-recovery attribution. Fresh candidate movement3.09% remains.
No universal speedup, RSS/heap, physical-GC, async-I/O or embedding-tail claim.
Allocation effectively flat (−0.006..+0.055%), all accounted peaks and compiled
balances unchanged, every shared-budget baseline restored. Dense2/3/4byte and
mixed counts are checked independently of the implementation.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3,6jobs229.23seconds,6second rotated
warmups/five samples/four processes; normal GC/tiering. Guest I/O/full goldens/
prior-result cleanup included; compilation only fresh. Staging/reset/protocol,
startup and final cache teardown excluded. WiderCPU includes request overhead and
is quantized. All quiet/golden/Closed/exit/join/archive gates pass; RAM removed,
terminal cached. Exact committed old raw/archive replicas and discardedbcfcore
retired after fresh lease/path/UID/hash/link/all-alias proof. Current0315,
toolchains/canonicals and254/252MiB floors preserved.

Full platform/stress/installed Release qualification and affected CPython refresh
are next. This milestone does not complete the broader call/frame/record/frontend
performance roadmap. Publication/tagging remain held.

[Decision](decision.json),[summary](summary.json),[allocation](allocation-summary.json),
[raw](observations.tgz),[source](candidate-sources.tar.xz),[identity](identity.json),
[procedures](procedures-and-checks.tar.xz).
