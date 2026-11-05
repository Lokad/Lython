# Compiler-bounded private operand cleanup

Source0752be3c plus the archived D56 patch; candidate/control86cea356,
reference7895ac6d (same D55 production, frozen before commit metadata).

The existing compiler shape pass now records one conservative highest slot that
can contain an interpreter-owned temporary. It covers iterator acquisition,
seeded generator-expression cursors, comprehension scopes, expanded call/class
accumulators, formatting builders and every duplicate output. These internal
wrappers never enter guest locals/arguments/results as guest values.

Retirement starts its existing private-disposal scan at the smaller of that bound
and the live tail. It still clears the **entire** original range. Reverse order,
failed-disposal entries/live extent and the awaited pending-cursor chain remain.
Ordinary and retained generator frames share this invariant. Remove classification
of scalar/ordinary guest operands above the bound; no runtime resource registry,
bitset, extra pass, array, owner, mode or public API. Code/frame metadata gains one
integer each; conservative duplicates can limit savings. No changed memory contract.

## Complete-script observations

B286140.24s within180s, five jobs, workerb94/CPython3.13.16 on the dedicated quiet
Azure VM. Normal GC/tiering,5s per-role/job warmup,7rotated~150ms samples, independent
identical-current process. All quiet/source/fixture/golden/library/Closed/final-zero/
joined-worker/removed-fixture gates pass.

| Lane/job | Candidate ms | Identical-current ms | Reference ms | Current/reference changes |
| --- | ---: | ---: | ---: | ---: |
| Warm ordinary CSV |4.5839|4.6426|4.6779|−2.01% /−0.75%|
| Warm text helpers |2.2601|2.2112|2.4377|−7.29% /−9.29%|
| Warm JSON helpers |4.4132|4.4410|4.4918|−1.75% /−1.13%|
| Warm report rendering |3.5574|3.4538|3.6885|−3.55% /−6.36%|
| CSV compilation only |.07231|.07162|.07295|−.89% /−1.83%|

Text current-role spread2.16%, candidate IQR1.85%; report spread2.91% and IQR
2.54/3.37/3.72% for candidate/control/reference. CSV/JSON smaller observations
remain susceptible to process variability. Compilation IQR4.23/3.52/1.25% makes
the small difference inconclusive. These are elapsed observations, **not attributed
CPU savings or a portable/general speedup**. Allocation, physical heap/RSS and host
latency were not measured. No separate isolated retiming proposed without new
evidence; retain all original observations.

Warm includes invocation, actual mediated /dev/shm I/O, full output/report-file
validation and prior-result cleanup. Compile includes compilation/disposal only,
with output equivalence checked outside that timer. Excluded: process startup,
staging/reset/transport and final cache/process/fixture teardown. This diagnostic
source pair does not replace the full CPython comparison guide.

**Retain:** compiler-owned bounded cleanup removes real repeated classification
without changing values, ownership or disposal strategy. Existing broad call/frame,
scalar/frontend and independent regex work remains open; this gate does not complete
the performance objective. D55's possible mixed-Unicode regression remains separate.

## Validation and delivery

Release build27.13s, zero warnings/errors. Focus223internal/786public per actual
.NET11/10 pass, including generators, comprehensions, expanded calls/classes,
formatters, native returns, refusal, cancellation and actual pending cleanup.
One new mixed guest/private lifetime control; existing reverse/failure/clearing
controls preserved. No timeout, scale, containment or checkpoint-count relaxation.
Full local4131internal/16004public pass on each actual runtime. Retained commit
b69a0a49 is pushed; Windows/Ubuntu, original stress and installed-consumer
qualification is running as [CI42065949621](https://github.com/Lokad/Lython/actions/runs/42065949621).

[Contract](source-contract.md), [observations](summary.json), [source patch](source.patch).
Raw archive preserves identities, full samples, orchestration, checks and source.
Archive SHA256`ffe5f1ef58deb5b1f82ec81f51afafa4b09a1b20ee5c87464b55ab14e8920124`.
