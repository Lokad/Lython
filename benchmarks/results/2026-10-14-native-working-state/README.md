# Native working state and caller lifetime

Runtime producer **09d81bb1**, production tree
**ec5f063033457c8456cf1637082dd72726e3ac61**, combines native working storage
(`9cf386b8`), active callers (`146fb75b`) and stored aggregate state (`09d81bb1`).
**Forced-GC relief and logical reclamation are unchanged.** Complete native
operations, partial construction, conditional identity and derived funded
workspace remain required before replacing CLR-dependent reclamation.

Native expanded-call and sorting buffers now describe their actual children,
including deferred inputs and merge scratch. Traversal follows list backing
fields without acquiring deferred ownership. Private buffers drop references
before funding is released, while copied arguments retain their identity.
Measured deficits justify stronger conservative shell charges; the original
small-sort charge passed until the new active-state fields were added.

One run-owned head links existing function and executable frames to their
dynamic callers. Lexical name resolution remains independent. No activation
arrays, copied operands, additional call-scope objects or instruction hooks
were introduced. Yield, return, cancellation and failed entry restore the
actual caller.

Native `sum` now has one preflighted 128-byte execution object for its evolving
accumulator, current item, source and iterator. Those references remain published
through callback reentry, genuine async suspension, iterator cleanup and native
result registration. Start validation is shared. Shell retirement drops its
references before returning funding; nested sums restore the outer operation.
SPEC records the conservative fixed-charge compatibility tradeoff. Opaque
compiler enumerators explicitly stop complete traversal.

## Validation

All eight original aggregate probes fail on the preceding runtime. The final
18 aggregate cases cover a 16,000-byte accumulator/current item, nested sums,
cleanup and result-registration failures, cancellation, denied publication,
measured shell funding and allocation-free retirement. **83 focused internal
checks pass in 1.274 seconds**, and **649 public checks pass in 7.020 seconds**,
including cleanup. The original 5,000-row CSV sum succeeds within 256 KiB when
rows are dropped and rejects retained rows through both synchronous and genuinely
delayed asynchronous host paths.

The combined full ordinary Release suite passes **11,034 cases** in
**305.520 seconds**, including cleanup. Preserved original stress cases and the
explicit Release package consumer are recorded in [qualification.json](qualification.json),
with exact artifact hashes and producer identities. Full Windows/Ubuntu CI is a
separate delivery gate. Test durations are correctness observations, not runtime
speed or embedding-latency measurements.

The preceding native-storage and active-caller focused assessments are preserved
in [native-storage-focused.json](native-storage-focused.json) and
[active-callers-focused.json](active-callers-focused.json). Their earlier
"focused only" status applies to their original checkpoints; the combined
qualification is recorded separately here.

## Short generated-sum comparison

One **7.893-second** diagnostic uses the existing 20,000-item generated-square
sum from `AsyncMaterializationBenchmarks`, replacing the top-level return with
shared printed output. Exact `146fb75b` sources are archived and built separately.
The candidate source was subsequently committed as `09d81bb1`; its precommit
library metadata names `146fb75b`, so frozen DLL hashes and source identity are
essential. It is not a clean preceding-revision measurement.

| Engine/source | Warm time per job | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython preceding `146fb75b` | 4.0675 ms | 7.77% | Diagnostic baseline |
| Lython sum candidate (`09d81bb1` source) | 3.9467 ms | 1.02% | Diagnostic candidate |
| CPython 3.13.2 | 0.4993 ms | 5.38% | Primary external reference; diagnostic |

This short run is **inconclusive** for the implementation's cost, and qualifies
neither an improvement nor a CPython multiplier. It was not repeated. Warm
synchronous public invocation includes fresh state, captured/projected output
and the independent output check; compilation, startup and transport are outside
the timer. Raw attempts and loaded identities are in [micro.json](micro.json).

Machine: shared Windows 11 x64 workstation, i7-14700KF, 28 logical processors,
64 GiB; .NET SDK 10.0.300-preview.0.26177.108, runtime **10.0.13**. CPython uses
ordinary GIL and cyclic GC with `-I -S`. This newer runtime identity differs from
earlier reports. No embedding-latency or overall current-HEAD claim follows.
See [BENCHMARKS.md](../../../BENCHMARKS.md) for the primary performance account.

Private build/test receipts and unsuccessful setup attempts remain under
`.git/agent-notes/native-aggregate-20261013/`. No comparative ten-minute lane ran
at this correctness checkpoint.
