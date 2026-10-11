# Instance MRO lookup reuse: integrated

The candidate avoids a repeated class-member MRO traversal within one instance
attribute read. The method workload changes **-5.06% /
-2.86%** across the two retained passes, with maximum producer
spreads **0.96% / 1.02%**.
The frozen method timing rule passes: **True**.
Repeated material timing regressions: **[]**;
repeated complete-job allocation regressions: **[]**.
The decision is **integrated**. These short measurements establish no
certified Python ratio or milestone qualification.

## Change and independent contracts

Baseline `5dc90ad4` / production `87eaf498` and
isolated candidate `95835dde` / production `cd9dc4b4`
share tests `57d066d4` and harness `c194ce88`.
Only the synchronous and awaited no-fallback instance readers change: retain
the first live class-member lookup result across the side-effect-free own
attribute dictionary read. Data descriptors and own-field hits still return
first. Ordinary class-member binding uses the already resolved member.

No result persists across accesses or crosses a guest callback. Live class and
descriptor mutations, custom slots, exact receiver/context/span, fees and
checkpoints, failure behavior, JSON engine fallback and genuine awaited effects
remain on their existing paths. The body's own-field read already returns before
this repeated lookup; this change targets ordinary method/class-member reads.
The broader call/body/frame/namespace and pipeline investigations remain open.
The earlier [method CPU profile](../2026-10-11-method-cost-profile/README.md) supplies
the source lead; it does not measure the candidate or establish CPU causality.

Before candidate implementation, the original runtime supplies **18 pinned
white-box checks and four independent CPython goldens**. They cover current
inherited values and removal, own-field shadowing, live descriptor-type changes,
getter mutation/reentry, exact context/span and 16-byte descriptor charges,
failure identity, fuel-before-cancellation, real mediated host suspension and
cancellation, and fresh lookup afterward. Existing default-object/call/context
checks remain in the full suites. Test-harness build/nullable/helper-name errors
and initially disabled step accounting are corrected before the candidate;
their original logs and observations remain retained.

Both clean producers pass **9,840 Debug and 9,840 VM Release checks each**
(1,862 white-box / 7,978 public), with matching probes, before normal collection.
Seven complete canonical cases match isolated CPython 3.13.16. Every measured
invocation verifies the unchanged complete source, fixture and expected output,
including complete stable-sort and ASCII-pipeline output.

## All short controls

Negative time change is faster. Spread is the larger retained IQR fraction of
the two Lython producers in that pass; the acceptance boundary is max(2%, spread).
All CPython observations remain in the metadata and original protocol records.

| Case | Time change, passes 1 / 2 | Retained spread, passes 1 / 2 |
|---|---:|---:|
| control.empty.control | -1.26% / -1.11% | 3.66% / 4.39% |
| loops.integer.large | -3.77% / +0.70% | 0.93% / 2.68% |
| calls.positional.medium | +0.69% / -2.40% | 1.52% / 1.37% |
| calls.keyword.medium | -1.86% / +1.26% | 0.89% / 2.94% |
| calls.method.medium | -5.06% / -2.86% | 0.96% / 1.02% |
| lists.stable-sort.medium | +8.21% / -0.63% | 16.32% / 6.91% |
| strings.pipeline-ascii.medium | -1.03% / -1.57% | 2.44% / 3.11% |

Fourteen separate paired groups take **8.038–8.051s** including startup and
cleanup. Separate ordinary allocation groups take **4.032–4.040s**;
native-code groups take **4.029–4.031s**. Every owned collection
group has a 30-second hard bound; preparation builds/tests occur beforehand.
No full lane, favorable recollection or discarded replica is used.

Method-job allocation savings in the two passes are **-3,690.88 bytes (-0.19%) / 96.00 bytes (0.01%)**. Allocation
measures complete compile/invoke jobs using process-wide
`GC.GetTotalAllocatedBytes(precise:true)`, with 16 warmups and two fixed
100-invocation passes per case, without forced collections. It is not exact
per-guest-type or per-method-call accounting. All seven controls are retained.

Separate native diagnostics retain every matched emitted body/tier, inline
summary and call/jump transfer, including tail transfers:
**81 baseline / 81 candidate bodies**.
The matched Tier1 outer instance reader changes from 4,642 to 3,902 bytes;
the no-fallback reader changes from 3,265 to 2,523 bytes. Their PGO / single-block /
other inline summaries change from 29 / 44 / 1 to 22 / 34 / 1, and from
23 / 35 / 0 to 16 / 25 / 0 respectively. Matched Tier1 main execution remains
4,584 bytes, stack transfer 2,458 bytes and MRO lookup 980 bytes. Every other
body/tier and its inline summary remains visible in the metadata.
Code sizes and inlining profiles cannot predict timing gains or establish
total resident code-cache costs. Public metadata retains every matched body/tier
and its inline summary, transfer counts/hashes and tail transfers. Complete
instruction records and JIT text remain in the private evidence; no body or tier
is discarded.

Both producers use SDK 10.0.401 / runtime 10.0.12 with ordinary worker tiering/PGO/GC,
workstation GC and Interactive latency. Normal worker overrides are absent.
All 20 owned services are terminal, all 66 recorded PID entries are absent,
both sources are clean, inputs rehash identically and the VM lease is free.
Private original protocol records, TRX receipts, JIT text and journals remain
retained. [metadata.json](metadata.json) contains all summarized controls,
native bodies and evidence hashes.
