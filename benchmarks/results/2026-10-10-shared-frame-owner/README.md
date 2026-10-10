# Shared interpreter and slot-state owner

Each executable interpreter now owns the slot-state references directly through
its base class. This removes a separate frame-state allocation and duplicate
references to the code object, local array and local-cell array. Complete call
jobs use **about 6.3–6.4% less managed allocation** in both fixed-count passes.
The change is integrated as a storage improvement. Timing is mixed between
replicas; no reliable throughput gain is established. Short collections do not
qualify performance, and the remaining call gap against CPython stays open.

Twelve paired micros stop in **8.04–8.06 seconds**, including
owned-group cleanup. Separate allocation groups stop in **4.03 /
4.04 seconds**, after all normal micros finish. Each collection has
a **30-second external process-group cap**. **No full lanes run.** Longer
qualification remains deferred to an infrequent, declared milestone.

## Design and frozen sources

Baseline `17a6b046b9208b7cc4f835253cc28e273dbbabf3`, production tree `47e033c1c653812e18adec388ca0fc41f3e48337`,
is identical in production to delivered green head `bb293c84`. Its prepared
Release producer is rehashed and reused. Candidate `e0a4ea775171ea113ef813a8d99da5d9e583e632` has
production tree `eb8857ec378778a91831b6ea5549fe9389d4a956`. Tests remain
`4873ff730fb2c6aa1ae7746ea083ed2f909a7b91` and the whole benchmark project remains
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

Previously the interpreter and a separate slot-state object retained overlapping
references. The interpreter now derives from `ExecutableFrameState`, borrows
its readonly fields and adds its execution-context reference. Compiled metadata
from both frozen libraries confirms the inheritance and absence of the former
compiler-generated duplicate fields. No object-size saving is inferred from
field counts alone.

Ordinary execution preserves conditional slot installation and restores the
previous frame in its existing finally path. Each generator retains one shared
owner, installs it on resumption, restores the caller's slots on suspension and
releases the owner at completion. Cells, local mirrors, stack backing arrays,
temporary disposal, exception state, checkpoints, limits, logical charges and
host mediation retain their contracts. No pooling or harness changes are added.

## Complete-job execution timings

Microseconds per invocation; parentheses are IQR divided by median. The last
column compares candidate with baseline: negative means faster.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.233 (2.7%) | 23.202 (3.6%) | 1.463 (0.5%) | -0.13% |
| Integer loop / 1 | 1355.947 (1.1%) | 1363.085 (0.9%) | 673.808 (0.2%) | +0.53% |
| Positional calls / 1 | 695.398 (0.8%) | 658.095 (1.2%) | 134.115 (0.8%) | -5.36% |
| Keyword calls / 1 | 753.694 (1.2%) | 690.640 (1.0%) | 151.164 (1.0%) | -8.37% |
| Full stable sort + output / 1 | 2897.179 (8.5%) | 2916.802 (8.2%) | 320.325 (1.0%) | +0.68% |
| ASCII pipeline / 1 | 148.749 (1.2%) | 143.394 (2.6%) | 43.375 (0.6%) | -3.60% |
| Empty / 2 | 25.273 (2.5%) | 24.723 (2.6%) | 1.552 (0.3%) | -2.17% |
| Integer loop / 2 | 1368.674 (0.8%) | 1350.845 (1.7%) | 668.586 (1.7%) | -1.30% |
| Positional calls / 2 | 665.564 (1.3%) | 663.504 (0.9%) | 128.503 (1.0%) | -0.31% |
| Keyword calls / 2 | 716.594 (0.5%) | 717.430 (1.8%) | 144.314 (0.2%) | +0.12% |
| Full stable sort + output / 2 | 2834.505 (8.6%) | 2689.158 (5.6%) | 320.734 (0.6%) | -5.13% |
| ASCII pipeline / 2 | 145.271 (2.6%) | 145.827 (3.5%) | 44.194 (0.5%) | +0.38% |

Both producers run the same canonical source, fixture and complete golden output.
Compilation occurs before execution timing. Every invocation creates fresh guest
state through the existing comparison worker. CPython 3.13.16 uses its pinned
PGO/LTO build, -I -S, ordinary GIL/GC. Lython uses SDK 10.0.401, CLR 10.0.12,
workstation GC, Interactive latency and ordinary tiering/PGO settings. No worker
environment overrides are supplied. The canonical full sort retains its complete
output work. Runtime APIs, object models and containment still differ; the empty
control and short unqualified observations are not general language speed claims.

## Separate managed-allocation diagnostic

The previously frozen allocation helper is rehashed and reused without rebuilding.
It calls the exact comparison worker's Compile/Invoke methods, checks complete
outputs on every invocation and measures `GC.GetTotalAllocatedBytes(precise:true)`
around 100 fixed invocations after 16 warmups per case/pass. There are two passes
in each producer process: 2,400 measured and 384 warmup invocations, 24 rows.
This is process-wide managed allocation, including worker/materialization/runtime
costs. It is not exact allocation by guest type; no collections are forced.
The passes share their producer process and are not independent-session qualification.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved | Saved |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 29,416.00 | 29,424.00 | -8.00 | -0.027% |
| Integer loop / 1 | 1,079,488.64 | 1,079,448.40 | +40.24 | +0.004% |
| Positional calls / 1 | 1,277,463.84 | 1,195,524.32 | +81,939.52 | +6.414% |
| Keyword calls / 1 | 1,294,103.36 | 1,212,157.20 | +81,946.16 | +6.332% |
| Full stable sort + output / 1 | 3,580,079.68 | 3,597,936.88 | -17,857.20 | -0.499% |
| ASCII pipeline / 1 | 461,872.00 | 461,832.00 | +40.00 | +0.009% |
| Empty / 2 | 29,286.64 | 29,299.60 | -12.96 | -0.044% |
| Integer loop / 2 | 1,079,261.84 | 1,079,221.84 | +40.00 | +0.004% |
| Positional calls / 2 | 1,277,287.36 | 1,195,341.20 | +81,946.16 | +6.416% |
| Keyword calls / 2 | 1,293,949.20 | 1,211,975.36 | +81,973.84 | +6.335% |
| Full stable sort + output / 2 | 3,486,368.88 | 3,486,328.88 | +40.00 | +0.001% |
| ASCII pipeline / 2 | 461,872.00 | 461,832.00 | +40.00 | +0.009% |

Positional call allocation falls 6.414% / 6.416%; keyword falls 6.332% / 6.335%.
The empty control rises by 8 / 12.96 bytes per job, consistent with additional
shared-owner storage plus process-wide variation. First-sort allocation rises
0.499% and second-sort allocation falls 40 bytes. Both stay visible, with no
exact attribution of process-wide variation. Loop/pipeline savings are about
40 bytes per job. No result is discarded or recollected.

## Verification and cleanup

Matching Debug probe and full frozen Debug suite pass **9,164 checks**
(1,407 white / 7,757 public) before collection is declared. Matching VM Release
probe, **111 selected white / 286 selected public checks**, and six canonical
cases against CPython pass before timing. Test sources are unchanged. Existing
coverage includes closures/local mirrors, generators, reentry, exceptions,
cancellation, limits, synchronous/asynchronous execution and streaming.

Independent audit checks **515 normal responses**, complete output
hashes, request IDs/counts, versions and defaults, recalculates medians/IQRs and
checks all 24 allocation rows against the frozen producer identities. Journals
prove prospective starts, collection order and whole-group bounds. Both source
trees are clean, both producers and the reused helper are rehashed. All **16 owned
services** are terminal; all **54 recorded PIDs** are absent; the shared lease is
freshly available. No collection is repeated or discarded.

Raw declarations, TRX, journals, hashes and cleanup receipts remain private under
`.git/agent-notes/shared-frame-owner-20261010/`. Maintained [evidence](evidence.json)
retains every observation and supporting hash. Original CSV/pipeline investigations,
external Utf8Regex integration, remaining call/context/local-array costs and
matched milestone/secondary qualification stay pending.
