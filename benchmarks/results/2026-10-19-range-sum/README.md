# Share checked range summation during async hosting, 2026-10-19 UTC

For a sealed range with an exact integer start, `SumAsync` reuses the existing
checked synchronous `Sum` loop. This removes generic async cursor acquisition
and per-item async pull/result work for that shape. Custom starts can invoke
guest code and retain the general async route, as do other iterables. Existing
range cancellation, numeric allocation preflights and general awaited cleanup
remain. No new iterator, formula, accumulator representation or public option.

Baseline: `64b8aca50c7e53fba91e2702f809747c321d9e5e`. Candidate: that revision plus
[the one-file patch](candidate.patch). Production sources compile into a private
diagnostic assembly like the white-box suite. These frozen snapshots do not
describe whichever revision is currently HEAD.

Each job computes 16 sums of `range(128)` or the `iter(range(128))` control.
The entry is compiled once; fresh mock-host creation, execution and result cleanup
are included. Compilation, process startup and I/O are excluded. The mock host
completes synchronously; real pending guest I/O/cancellation is tested separately.

| API / input | Before median | After median | After / before | Allocation change |
| --- | ---: | ---: | ---: | ---: |
| Run / iterator control | 395.56 µs | 430.58 µs | 1.089 | 0 B |
| Run / exact range control | 329.25 µs | 357.68 µs | 1.086 | 0 B |
| RunAsync / iterator control | 1,261.94 µs | 1,294.49 µs | 1.026 | 0 B |
| RunAsync / exact range | 398.51 µs | 309.33 µs | 0.776 | +512 B |

These are nominal elapsed samples from one short nonquiet capture. Control times
move too; no qualified latency, host tail-latency or CPython speedup is established.
The existing checked synchronous path costs an extra 32 B per sum in this fixture
(512 B per job). That allocation tradeoff is retained explicitly rather than
reported as an allocation improvement. Memory ownership and admission policy stay
unchanged; every job checks allowance returns to its compiled baseline.

Eight warmup jobs, seven samples of eight jobs; all raw bytes/ticks are preserved.
Allocation uses `GC.GetTotalAllocatedBytes(true)` across managed threads. This is
cumulative allocation, not retained heap/RSS or an admission policy. No forced GC.
Windows10.0.26200 X64, i7-14700KF / 28 logical processors; .NET10.0.13,
SDK10.0.300-preview.0.26177.108. Two new semantic fixtures match CPython3.13.2:
empty-range aliases/bools/large bounds and guest-start output bytes. Existing
active-loop cancellation, refusal and funded async-cleanup regressions are reused.

[Scope](measurement-scope.json), [before](before.json), [after](after.json),
[summary](summary.json), [diagnostic](diagnostic.cs), [project](diagnostic.csproj),
[baseline identity](baseline-source.json), [candidate identity](candidate-source.json),
[CPython semantic controls](cpython-semantics.json).
