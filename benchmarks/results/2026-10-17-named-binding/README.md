# Named builtin binding allocation diagnostic

One local Windows capture on 2026-10-17 UTC compares Lython `49b6b65e` with
the [frozen binder patch](candidate.patch). The boundary is 128 warm native
`sorted(values)` calls, including interpreter/call/result handling. Source,
input, context and compilation already exist. Startup and host I/O are excluded.
This exercises a call form used by the complete text/CSV/JSON/file jobs; it is
not a timing of those jobs.

| Call form | Synchronous baseline bytes | Candidate bytes | RunAsync baseline bytes | Candidate bytes |
| --- | ---: | ---: | ---: | ---: |
| `sorted(values)` | 139,904 | 133,760 | 160,472 | 154,328 |
| `sorted(values, reverse=True)` | 135,808 | 135,808 | 156,376 | 156,376 |

Each cell is the median of eight current-thread managed-allocation samples after
four warmups. All baseline samples equal their median. One candidate async
keyword sample is 2,664 bytes high; all other candidate samples equal the median.
The ordinary call saves one 48-byte temporary parameter array per invocation.
The keyword control already occupies the full shape and has no median change.
No elapsed-time, CPython multiplier, RSS or embedding-latency benefit is proved.
The second keyword lookup's latency cost is unmeasured.

The binder validates already-expanded arguments in their original order, then
creates their final storage once. Full temporary arrays, trimming resize/copy
and the extra-positional value list disappear. Existing presence bits, parameter
indices, optional gaps and omission semantics remain; there is no cache, binding
map, dispatch mode or ownership mechanism added. Argument evaluation precedes
both passes. The core argument transport is unchanged.

The same temporary probe captures 4,686 binding records: zero through four
arguments drawn from positional, known/unknown keyword and explicit `None`
tokens, across optional, variadic and positional-only layouts, with and without
presence tracking. Before/after results, presence and error type/message/span
are byte-identical. The single canonical [trace payload](binding-traces.json.gz)
and hashes, raw [baseline](baseline-allocation.trx)/[candidate](candidate-allocation.trx)
output and all samples are in [evidence.json](evidence.json). This finite corpus
supplements existing regressions; it does not prove all Python call semantics.

Machine and SDK are recorded in evidence. The exact test-runtime patch was not
captured. The source-linked Release net10.0 assembly completed each owned run
under the existing 28-second cap plus one second for cleanup, without forced GC.

The [temporary probe](NamedBindingAllocationProbe.cs.txt) was removed before
final qualification. To reproduce, use revision `49b6b65e`, copy that source into
the white-box test project, adjust its capture path, build Release and select
`NamedBindingAllocationProbe`. Apply the frozen patch and repeat once; remove
the temporary source afterward. This is archived evidence, not a benchmark
framework or an ordinary allocation-threshold test.
