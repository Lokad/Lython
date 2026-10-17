# Direct asynchronous loop-step dispatch

One local Windows capture on 2026-10-17 UTC compares Lython `5b546b2e` with
the [frozen two-file patch](candidate.patch). Each sample executes 4,096 native
range or list iterations, assigning the last value. Source, input, compilation
and execution context already exist. Public invocation setup, host I/O, startup
and fresh compilation are excluded.

| Loop / API | Baseline median ms | Candidate median ms | Baseline spread | Candidate spread | Managed bytes, unchanged |
| --- | ---: | ---: | ---: | ---: | ---: |
| Range / Run | 1.9804 | 2.0104 | 27.0% | 1.1% | 131,600 |
| Range / RunAsync | 2.6685 | 0.9742 | 6.4% | 53.5% | 131,600 |
| List / Run | 0.5246 | 0.5267 | 2.2% | 8.9% | 480 |
| List / RunAsync | 1.0530 | 0.8592 | 136.3% | 4.0% | 456 |

Eight samples follow four warmups. Spread is `(max - min) / median`. Every raw
sample is retained in [evidence](evidence.json) and the original
[baseline](baseline.trx)/[candidate](candidate.trx) output. RunAsync medians are
lower in this one capture; wide distributions leave magnitude unqualified.
There is no established complete-job, CPython-ratio, RSS or embedding-latency
improvement. Synchronous code is unchanged; its medians move within the observed
spreads. Allocations remain unchanged in all cells.

The cursor already returns completed ValueTasks for native movement. Previously,
every async `for` pull also entered the large `ExecuteValueOperationAsync` state
machine. The patch moves that existing awaited body into the main async switch
and deletes the helper case. It calls movement once, publishes the same value,
pops before awaited disposal and changes blocks only after cleanup completes.
Custom iterator/generator suspension uses the same cursor operation and awaits.
No classification flag, mode, iterator, owner, cache or protection policy is added.

Separate finite fuel controls preserve all 80 outcomes exactly: 40 step budgets
per API record effects, step count, error message/type/span and completed results.
They cover partial loop bodies and exhaustion. This supplements existing protocol,
actual I/O, cancellation, admission, stack and representative-script regressions;
the native diagnostic alone does not establish those wider guarantees.

The [temporary source](ForNextProbe.cs.txt) was removed before final qualification.
Reproduce at `5b546b2e` by copying it into the white-box project, building Release
and selecting only `ForNextProbe`; apply the frozen patch and repeat once. Remove
temporary source afterward. Keep the existing 28-second owned-process cap plus one
second for cleanup. No forced collection, tiering adjustment or long lane is used.

Machine and SDK are recorded in evidence. The exact test-runtime patch was not
captured. An initial diagnostic incorrectly relied on a temporary module local
being mirrored after setup. It was corrected to return the input and bind it
explicitly before these valid baseline samples; the failed receipt remains in
private history. That fixture correction is not a runtime bug or speedup.
