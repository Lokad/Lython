# B1280 source coordinates during literal scanning

The candidate collects physical newline positions while looking for the end
of a string. The tokenizer uses the existing newline table and restores its
length before the malformed f-string fallback. The scanner shares escape
handling with format specifications. Nested strings and comments contribute
to the same source map.

Local instrumented compilation at `86da4a942` assigned 18.87 µs of a
1.045 ms template-heavy compile to the newline rescan. It assigned
7.08 µs of a 2.304 ms validator compile and 3.50 µs of a 1.271 ms dataclass
compile to the same pass. These measurements include instrumentation and
timer cost. The template fixture has 96 sections and 29,565 source
characters. It exercises multiline literal text, expressions and
format specifications together.

The controlled comparison used reference `86da4a942` and candidate
`0123f5e84` on November 21, 2026 UTC on the quiet Azure F4as_v7, running
Ubuntu 24.04.4 and .NET 11 RC3. Each lane had two independent workers per
source, at least eight seconds of warmup per worker and seven rotated
batches. The ranges cover all pairings of the process medians. Compilation
includes code-owner disposal. Time to result adds guest execution,
mediated RAM-backed I/O, output verification and prior-result cleanup.
Process CPU covers the wider request. Process startup, fixture reset and
final process disposal are separate.

| Script | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Embedded restock templates | Compilation | −3.52 to +0.08% | −2.50 to −1.88% |
| Embedded restock templates | Compile to result | −4.33 to +1.52% | −5.29 to +1.78% |
| Generated validator | Compilation | −1.78 to −0.54% | −1.31 to +0.22% |
| Generated validator | Compile to result | −2.83 to +0.44% | −3.46 to +1.26% |
| Inherited dataclasses | Compilation | −1.40 to +1.63% | −1.83 to +1.95% |

Retain the change. It removes a full literal rescan and decreases
template compilation CPU across all four pairings. Template compilation
elapsed touches zero and both time-to-result measurements span zero.
The dataclass control is inconclusive. No execution-only speedup was
measured. CPython is the semantic reference for the workload goldens.

All 140 timed batches and both machine gates passed in 271 seconds.
The workers joined, the guest fixtures were removed and the raw
observations verified under the lease.

The local differential checks compared tokens, spans, physical coordinates
and parser diagnostics across 186 inputs, 150 of them valid. The template
fixture matches local CPython in synchronous and asynchronous execution.
Windows .NET 11 and Ubuntu .NET 10 each pass 141 white-box and 1,681
public focused tests. Both complete 32-file frozen worker images pass the
four script goldens. Local warm allocation and accounted peaks match, and
each execution returns its allowance to baseline. Uninstrumented local
compilation allocation is effectively unchanged. Local elapsed medians
include volatile unchanged controls and are diagnostic.

[Controlled timing](summary.json) · [Phase attribution](phase-attribution.json)
· [Local timing](local-timing-summary.json) · [Allocation and ownership](allocation-summary.json)
· [Raw observations](observations.tgz) · [Source, checks and procedures](checks-and-procedures.tar.xz).
