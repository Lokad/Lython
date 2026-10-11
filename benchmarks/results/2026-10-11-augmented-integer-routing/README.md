# Exact-integer augmented-assignment routing

**Rejected.** The declared short-round decision is applied to every observation.
The basic-loop time changes are -1.50% / -5.48%
in the two passes, with retained spreads 1.05% / 0.64%.
Loop gains above max(2%, spread) in both passes: False. Repeated material timing regressions: none. Repeated allocation growth above 0.5%: none.

This is an ordinary improvement-round assessment, not milestone qualification.
It establishes no certified Lython/CPython ratio. The broader function-body
compiler, remaining dispatch/ownership costs and original pipeline causes stay open.

## Change and independent correctness gates

Baseline `35aa09eb` / production `87eaf498` and
isolated candidate `655ecfc0` / production `65993cbc`
share tests `6ea4d482` and harness `c194ce88`.
Only the operation bridge changes: exact built-in integer Add reaches the existing
`AddExactIntegers` helper without executable-to-syntax conversion. All other
operands/operators retain the mapped semantic fallback. The main loop, right/left
pops, result observation, actual context/governor/pool, source guards and async
paths remain unchanged. There is no representation change or discounted fee.

Before candidate implementation, the original runtime supplies twelve pinned
ownership/checkpoint checks and four independent CPython goldens. They cover signed
promotion, actual tracked result boxes, 201/209-byte ownership including registry
storage, denied reservation/registration rollback, all six fuel cutoffs and the
publication point, fuel before cancellation, mixed/rich/reflected/list fallback,
failure preserving its target, and generator suspension with captured state.
Full suites retain the existing actual context/namespace/cell/receiver, admission,
argument lease, delayed-host/reentry and lifetime checks.

Both clean producers pass **9,818 Debug and 9,818 VM Release checks each**
(1,844 white-box / 7,974 public), with matching probes, before normal collection.
Their seven complete canonical cases match isolated CPython 3.13.16, including
complete stable-sort and ASCII-pipeline output. Every measured invocation checks
the canonical output; case/source/fixture/output hashes are unchanged.

## All short controls

Negative time change is faster. Spread is the larger retained IQR fraction of
the two Lython producers in that pass; the acceptance boundary is max(2%, spread).
CPython observations remain in the metadata and original protocol records.

| Case | Time change, passes 1 / 2 | Retained spread, passes 1 / 2 |
|---|---:|---:|
| control.empty.control | +0.53% / +1.01% | 5.22% / 5.14% |
| loops.integer.large | -1.50% / -5.48% | 1.05% / 0.64% |
| calls.positional.medium | +0.99% / -3.91% | 1.41% / 4.39% |
| calls.keyword.medium | -0.52% / -0.72% | 3.44% / 2.65% |
| calls.method.medium | +7.36% / -1.33% | 0.97% / 1.83% |
| lists.stable-sort.medium | -12.16% / -3.95% | 16.08% / 14.44% |
| strings.pipeline-ascii.medium | -5.47% / +0.87% | 3.64% / 2.44% |

Fourteen separate paired groups take **8.038–8.056s** including startup and
cleanup. Separate ordinary allocation groups take **4.034–4.043s**;
native-code groups take **4.032–4.040s**. Each owned group has a
30-second hard bound. Preparation builds/tests occur beforehand. No full lane,
favorable recollection or discarded replica is used.

Allocation measures complete compile/invoke jobs using process-wide
`GC.GetTotalAllocatedBytes(precise:true)`, with 16 warmups and two fixed 100-invocation
passes per case, without forced collections. It is not exact per-guest-type
accounting. Native diagnostics are separate; every matching emitted body/tier,
inline summary and call target is retained. Code sizes cannot predict timing gains.
An additional offline review records every call/jump instruction, including the
tail transfers omitted by the original call-only summary. It confirms the candidate
bridge's direct tail transfer to the same integer helper and preserved rich fallback.
Tier1 bridge size is 57 to 145 bytes; value routing is 1,887 to 1,983 bytes; the
integer helper remains 409 bytes. All other emitted tiers remain visible.
Public metadata retains every matched body/tier and its inline summary, target
counts/hashes and tail transfers; full call/jump records and JIT text stay retained
in the private evidence. No native body or tier is discarded.

Both producers use SDK 10.0.401 / runtime 10.0.12 with ordinary worker tiering/PGO/GC,
workstation GC and Interactive latency. Normal worker overrides are absent.
All 20 owned services are terminal, all 66 recorded PIDs are absent, both sources
are clean, all inputs rehash identically and the VM lease is free. Private original
protocol records, TRX receipts, JIT listings and journals remain retained.
[metadata.json](metadata.json) contains all summarized controls and evidence hashes.
