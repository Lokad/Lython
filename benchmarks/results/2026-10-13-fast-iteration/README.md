# Faster containment regression iteration

Producer: c4cd4e9d, production tree f15f46687ef29c864e6a75c43e9d9d93870fdac1.
Measured on 2026-10-13 on the local Windows x64 development workstation (Alienware Aurora R16,
Intel Core i7-14700KF, 28 logical processors, 64 GiB RAM),
.NET SDK 10.0.300-preview.0.26177.108 / runtime 10.0.12. These are test-run
observations on a shared workstation, not Lython execution benchmarks or
embedding-latency qualification. [TESTING.md](../../../TESTING.md) gives the
maintained development and checkpoint commands.

Most lifetime regressions now use at most 2,048 iterations and 64 KiB instead
of 20,000 to 100,000 iterations and 1 to 3 MiB. A 32-byte stranded charge per
iteration alone exhausts that small cap. Larger fixed live inputs require
specific fixtures: regex compilation has an existing 64 KiB pattern charge,
slices use 64 items, file scans stream 4,096 rows through a 128 KiB cap, and
import tests use small deferred bodies with separately funded controls.
Retained integer tests use 1,024-digit parses or 512! and also prove that one
result fits the same 16 KiB cap. Original counts/caps are independently
selectable through `Category=Stress`, sharing bodies and assertions.

The 40,000-member module fixture retains its exact content but now uses
StringBuilder; repeated concatenation previously copied a quadratic amount
of text and contributed unrelated allocation pressure.

## Iteration results

| Check | Result | Elapsed including cleanup |
| --- | --- | ---: |
| Historical full Release suite at 4b8108bf | 10,924 pass | 542.731 s |
| Ordinary suite at c4cd4e9d | 10,938 pass | 292.389 s |
| Original stress scales at c4cd4e9d | 375 pass | 218.811 s |
| Explicit Release pack and consumer at c4cd4e9d | pass | 24.821 s |
| Focused accounting/CSV/unpacking | 59 pass | 10.669 s |
| Minimized boundary/input fixtures | 36 pass | 4.790 s |
| Binding/gzip/stored-child continuation | 68 pass | 1.652 s |

The ordinary suite excludes separately retained stress scales. Its final parsed-
integer, factorial, yielding-instance and list-growth checks report 0.881,
0.255, 0.167 and 0.155 seconds respectively. Previously these were among the
concurrent multi-minute cases. Other .NET test processes were observed during
the final checkpoint; these durations are observations, not repeatable speedup
ratios. Some unchanged CSV/protocol cases still take 24 to 48 seconds in that full
run. Use focused regressions and batch full gates; do not claim a one-minute
whole suite or continue an open-ended fixture-tuning campaign.

## Failure detection

Temporary fault binaries were discarded and source bytes restored before
qualification. Disabling swept-owner refunds (with a concurrent deliberately
underestimated integer-size control) makes all nine selected small streaming,
container growth, instance, diff, matcher and unpacking regressions fail. Four
integer value/single-result checks and two retention checks pass under that
combined control; it is not evidence for omitted integer ownership.

A separate control bypasses durable integer ownership at OwnFreshInteger.
Both retained-magnitude tests fail, while all four value/single-result controls
pass. This distinguishes rejection caused by retained magnitudes from rejection
of the first parse/factorial. The controls finish in 2.291 and 2.036 seconds
including cleanup. No production fault, weakened assertion or relaxed timeout
remains.

## Scheduling observation

One comparison uses the identical test DLL and ten reduced cases. Parallel
execution takes 3.126 seconds including cleanup; serial execution takes 3.530.
Individual iterator/diff/matcher/container cases run about two to five times
faster serially, but total group throughput does not improve. GC, CPU contention
and startup are not separately attributed. This finite observation supports
isolation of lifetime observations, not a causal GC performance claim or a
blanket serial-suite policy.

Tests that explicitly invoke GC or finalizer waits now share the existing
nonparallel collections (76 additional classes). Small lifetime families use
the public collection because exhaustion relief also invokes process-wide GC.
Ordinary semantic tests remain parallel. Assembly isolation does not guarantee
machine-wide quietness.

## Runtime continuation

5d259df7 preserves the original full presence-aware argument extent rather
than trim and reconstruct it. The original allocation oracle observes 128
bytes/call and fails; the accepted route passes the conservative 56-byte/call
bound, preserving optional None/presence, variadic and validation behavior.
This is an allocation control, not a qualified runtime speed comparison.

dc5b6d95 shares gzip discard/release paths, adds guest-free execution-thread
retirement to the existing finalizable-owner contract, and describes the stored
cursor owner/input cycle. Controlled disposal faults still release local buffers
and attempt owned-stream close. Active operations reject abandonment; supplied
streams, escaped values and the closed owner shell retain their charges.
68 focused binding/resource/stored-child controls pass in 1.652 seconds.

Retirement and storage walking remain disconnected from governor relief.
Complete root, child, resource-release and workspace-capacity proof is still
required before logical reclamation. Forced GC remains current policy.

## Qualification

All10,938 ordinary and375 original stress checks pass, as does the explicit
Release package compatibility/mediated-file consumer. Results, binary identities
and package SHA512 are recorded in qualification.json. Cross-platform CI is a
separate gate at the delivery documentation descendant; this report retains
its measured producer.
Earlier failures remain in the private receipts: an overly broad diagnostic hit
its 28-second cutoff; smaller budgets exposed legitimate regex/live slice
working-set requirements; one append-recovery fixture needed headroom for its
funded recovery result. The first full checkpoint reports four failures in
341.996 seconds. Corrected slices reduce their actual live input instead of
relaxing their assertions; regex caps fund the existing pattern and compilation
reservation. Imported-code fixtures and scratch loops were then minimized.
The original frozen governor history is unchanged.
