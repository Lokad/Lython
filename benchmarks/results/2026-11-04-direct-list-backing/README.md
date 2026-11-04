# Direct list backing

2026-11-04, retained for design simplification and lower whole-script allocation,
with explicit text timing debt. Source0e516365 (same runtime asffc01134) plus B203;
full patch/snapshots and hashes are authoritative. Final frozen core
`67373996909a8b55239d5895e4a7ab7b6f13a5b53f46627fd1b61e1afb61aa7a`.
Baseline core71879656 is qualifiedffc01134; initial prototyped8859af9 is separate.

## Removed work and ownership

PyList owns its array/count/capacity funding directly. Delete IPyListStorage,
SmallPyListStorage, ArrayPyListStorage, the CLR List<object> wrapper for larger
lists and the promotion/factory dispatcher:366 net production lines removed.
One allocation/interface layer disappears for ordinary lists; larger lists also
lose their CLR wrapper. No new backend, pool, policy, owner layer or public API.

Capacity preflight funds old/new overlap before allocating or visible mutation;
construction failure refunds private storage and live extension retains its
successful prefix. Scalar headroom remains capacity-based. Sort detaches an
array/count/charge value through callbacks and real suspension, restores it in
finally and releases callback working capacity. No added mutation counters.
Repetition copies the populated prefix in bounded cancellation-checked blocks,
preserving periodic sequences and aliases across block boundaries.

## Initial adverse condition and justified correction

Initial8->16 growth was unrelated to deleting storage wrappers. It places the
1,024 config paths at exact capacity, then appending causes another large backing
and overlap: allocation+6.08%; admitted execution peak253,951->394,361B. This is
real evidence, not a failed quiet gate. The initial source and all observations
are retained. One ordinary expression restores established first-growth demand
sizing (8->requested), followed by geometric growth. No storage classes return.
Tiny-budget identity is not promised; retained-capacity and admission guarantees
remain. Final config allocation is effectively flat (-0.04%) and admitted peaks
match the baseline, as do all eight allocation cases on both APIs.

Final whole-script allocation: CSV-1.90% Run/-1.78% RunAsync, text-2.95% both,
fixed-slice control-3.63% both. JSON/report/config/scalar/padding effectively flat.
Text saves about73.8KB/job. No lower live-budget, heap/RSS or embedding-latency
claim follows from these allocation reductions.

## Controlled elapsed comparison and retention decision

Quiet Azure F4as_v7/Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,
pinned.NET11 RC3, ordinary GC/tiering. Initial8 jobs, old/initial/old,6s warmup;
capture256.729s/owned service257.341s. All goldens, identities, balances, quiet
gates and joins pass. Initial text elapsed straddles identical-source controls
that differ6-7%; no confident regression magnitude follows from that collection.

Refinement6 jobs, old/initial/refined/old,8s warmup/image, nine rotating~200ms
samples; capture302.608s/owned service303.230s, below420s bound. All gates,
goldens/balances/normal exits/cleanup PASS; no guest fixtures remain. Separate
existing three-image eight-case allocation driver passes both APIs in each capture.
Warm clocks include invocation, actual mediated RAM-file I/O, full checks and
prior-result cleanup. Compilation/startup, reset/staging, response hashing and
final cached teardown are excluded; every user is joined. No frontend changed,
so no unaffected compilation or external CPython campaign is repeated.

Negative elapsed change is faster; both old roles use identical71879656 bytes.

| Complete job | Refined vs old first / last | Refined vs initial | Old-role spread |
| --- | ---: | ---: | ---: |
| text-summary-helpers / warm | +2.94% / +1.22% | +0.50% | +1.69% |
| text-summary-helpers / warm-async | +7.12% / +0.71% | +0.53% | +6.37% |
| config-edit / warm | -2.63% / -2.13% | -1.42% | -0.51% |
| config-edit / warm-async | -2.67% / -2.09% | +0.23% | -0.59% |
| csv-summary-helpers / warm | -1.31% / -0.28% | +2.74% | -1.03% |
| report-render / warm-async | -0.82% / +0.28% | +1.10% | -1.11% |

**Retain** the final simpler representation. Config elapsed improves2.1-2.7%
against both old roles under these conditions; no broad CPU gain is claimed.
**Accept synchronous text1.2-2.9% elapsed cost as explicit debt**: refined IQR0.46%,
old-role spread1.69%; do not call it a speedup or erase it with allocation results.
Async text magnitude remains uncertain (+0.7..7.1%, old spread6.37%). CSV/report
points are small/control-limited. Process CPU is a separate quantized diagnostic
and does not establish an isolated CPU improvement. The next text representation
milestone must assess this debt against the same source, without adding deltas or
crediting unrelated report gains as a causal explanation. No unchanged retiming.

## Correctness and delivery

Final nonincremental Release build, zero warnings/errors.410 internal/1,701 public
per actual.NET11/10 PASS, covering constructors/capacity/refusal/rollback, aliases,
self-extension/live iteration, slicing/repetition, sort callback mutation/error,
genuine async keys, cancellation/awaited cleanup, shared-budget reuse and CSV/JSON/
text streaming. Architecture-only clone/growth tests now exercise ordinary PyList
values/funding; no containment assertions or timeouts relaxed. The independent
odd-period alias case was strengthened from9,003 to18,003 items to cross actual
copy blocks, then the complete focused selection reran on the refined source.

Earlier baselineffc01134 passes all eight delivery gates in cached41788586642.
Final B203 Windows/Ubuntu, original stress and installed-consumer checks are
pending. Publication/tagging remain postponed; the performance objective is open.

Initial raw SHA256:
`4af35d1f63714ec071af770c0a22bcef11fc03944e45e8a509b9acb070391628`.
Refined raw SHA256:
`da801184841143d108df073fbf3bc30858f940437d7e9a251a8e1aee72547f88`.
Archives retain every sample, source/deletion patch, snapshot, conditions and
receipt. Focused qualification includes original pilot/final/strengthened logs,
both actual runtimes and the discarded capacity condition. No dormant prototype
or alternate runtime remains in supported source.
