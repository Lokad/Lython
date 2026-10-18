# Direct split calls with a count, 2026-10-18 UTC

Immediate positional `text.split(separator, maxsplit)` now reads existing operands
through the native receiver path. It avoids a temporary bound-method wrapper,
capturing delegates and argument packaging. The existing split helper/kernel
retains the corrected count order, range and type-index behavior. Only guest
index values enter the awaited managed wrapper; ordinary native counts complete
synchronously. Exposed methods, keywords, expansion and custom receivers remain
on their general routes. No binder, cache, execution mode or owner is added.

Baseline is immutable **7bf17b16cbeec49a81c702c43da705e4b3375847**, which already
contains the count correctness fix. Candidate is the [frozen four-file runtime
extension](candidate.patch), plus focused public regressions. The split semantics,
type-index helpers, UTF-8 kernels, preflights and ownership bodies are unchanged.

## Fixed allocation and work gate

| Route | Sync before / after | Async before / after | Saved per call |
| --- | ---: | ---: | ---: |
| Separator + count | 116,224 / 73,216 B | 112,104 / 73,192 B | 336 / 304 B |
| None + count | 116,224 / 73,216 B | 112,104 / 73,192 B | 336 / 304 B |
| Exposed method control | 112,136 / 112,136 B | 112,112 / 112,112 B | 0 B |
| Omitted/single argument controls | 91,648 / 91,648 B | 91,624 / 91,624 B | 0 B |

Totals include the native loop/checksum for **128 calls**. One baseline and one
candidate capture use four warmups/eight samples per route. All eligible samples
decrease and control arrays match exactly. All **240 work/result/identity/error/
span/callback/fuel records** match, including count-before-separator failures,
large/overflow counts, guest hooks and exhaustion. The [fixed gate](comparison.json),
[baseline](baseline-native.trx), [candidate](candidate-native.trx) and
[source](diagnostic.cs) preserve the evidence; no retuning or repeated capture.

This is an internal executable-loop allocation diagnostic, excluding frontend,
context/input setup, public projection and worker scheduling. Local Windows x64
build 26200, i7-14700KF, 28 logical processors, .NET host 10.0.13 and
SDK 10.0.300-preview.0.26177.108 are recorded in [environment](environment.json).
No elapsed, complete-job or CPython speedup is measured or claimed. Pending
host-I/O allocation is not measured by this completed native-loop fixture.

## Validation

All **1,014 distinct final focused Release cases** pass: 127 scalar/split, 176
surrounding string/lifetime, 496 binding, 89 ownership/admission, 62 complete-job/
cancellation and 64 white-box cases, including 58 complete CSV/text/JSON/file jobs.
All [31 split source goldens](python-goldens.json) match isolated CPython 3.13.2.
[Validation](validation.json) records source and process hashes.

New cases verify a native count produced after genuine host suspension/cancellation
and a custom two-argument receiver. Existing guest-index/descriptor I/O, receiver
rebinding, retained results, refusal through finally/catch and cleanup remain
covered. Original Stress scales, assertions and timeouts remain unchanged.

The temporary diagnostic is removed. This closes the two-positional call shape;
other methods and wider binding/ownership designs are outside this change.
Core dependencies and planning-only regex migration are unchanged.
