# Short escaped JSON strings, 2026-10-17 UTC

This change reduces managed allocation for short escaped strings; it establishes
no elapsed improvement. The native diagnostic saves 32 bytes per short-string
conversion. One complete in-memory JSON transform has a nominally higher median.

Baseline: `928066890e6199c64d8a44733116fe244ff342d1`. Candidate:
`c9990b3850d20dca78d3780022769c28106d23ca`. Later documentation-only revisions
do not change the measured runtime. These historical measurements are not a
current-HEAD performance claim.

| Diagnostic | Earlier Lython | Candidate | CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Native short escaped string, allocated bytes / 128 calls | 80,896 | 76,800 | Unmeasured | Eight identical samples under each API; 4,096 fewer bytes |
| Native unescaped control, bytes / 128 calls | 76,800 | 76,800 | Unmeasured | Unchanged |
| Native large escaped control, bytes / 128 calls | 275,456 | 275,456 | Unmeasured | Unchanged |
| JSON parse/edit/dump, 1,024 records / 55,723 UTF-8 input bytes | 3.350 ms | 3.503 ms | 1.016 ms | One local diagnostic; no qualified ratio or elapsed benefit |

The workload is the unchanged `json.transform-roundtrip.large`: parse a JSON
array, update every record, then print its serialized result. Its text fields
contain short Unicode escapes. Warm execution includes fresh mutable guest state,
ordinary invocation setup, prior-result cleanup, output capture and cheap complete
output checks. Compilation, worker/process startup, transport and response hashes
are outside timing. All three engines pass the independent golden before and after
sampling. The native allocation counter includes direct `json.loads` calls with
funded input, context/import/callable setup outside the counter; results are consumed
by UTF-8 length and checked afterward. No collections are requested.

This was the active Windows development workstation, **not the quiet Azure VM**:
Intel Core i7-14700KF, 20 cores / 28 logical processors, Windows 11 build 26200,
x64, .NET SDK 10.0.300-preview.0.26177.108 / runtime 10.0.13, CPython 3.13.2 with
ordinary GIL and GC. Both Lython workers use Release builds and ordinary protection,
GC and tiering. The existing micro command collected seven rounds in one 8.4-second
owned process. IQR/median is 3.4% baseline, 3.9% candidate and 5.7% CPython. The
candidate median is 4.6% higher; one unqualified run cannot establish its cause or
a reliable regression magnitude. The capture was not repeated or tuned.

The implementation uses a bounded, memory-safe managed span for escaped UTF-8
tokens up to 256 bytes and the existing governed result-copy factory. It removes
the intermediate CLR string and re-encoding on that path. Larger tokens and
invalid UTF-8 retain the existing decoder. The token reservation, retained result
funding, errors, character offsets, hooks, cancellation and stack policy remain.
A blanket heap-buffer replacement was rejected in API feasibility because it
increased allocation for Unicode-heavy inputs. No pool or new ownership layer
was introduced.

Raw evidence: [allocation](allocation.json), [micro receipt](micro.json) and
[identity, input hashes and worker cleanup](measurement.json). The temporary
[native probe](native-allocation-probe.cs.txt) and its
[source-exact fixture](native-storage-fixture.cs.txt) are archived here and removed
from the ordinary suite. Permanent controls cover independent CPython results,
threshold boundaries, lenient controls, local/shared denial before the result
copy and retained ownership. Existing JSON fuel traces and legacy error snapshots
match. This bounded investigation is closed; allocation totals establish neither
RSS nor embedding latency.
