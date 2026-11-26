# Await nested JSON hooks directly

`aef7c7071` removes the forwarding async operation that paired an incomplete
nested JSON hook with its byte end. The existing construction continuation now
holds the end and awaits the hook directly. The root scalar path still adapts
an incomplete result at its single-value return boundary. Default scalars and
completed callbacks continue through the construction loop directly.

The local Windows .NET 11 RC3 comparison uses the frozen `264a1176f` core and
the candidate core built before commit, with identical harness files. Each
workload has three warmed processes per image with alternating start order.
The hook variant decodes and aggregates four small documents per request. Each
request makes 24 host delays across integer and object hooks. The callback-free
variant aggregates the same document values. The measurement covers execution,
projection, result checks and disposal. Compilation and process startup are
outside the sample. Allocations use process-wide CLR counters.

| Workload | Median CLR bytes per request, before → after | Peak admitted bytes, before → after |
| --- | ---: | ---: |
| Nested integer and object hooks | 88,834 → 84,960 | 76,413 → 76,413 |
| Default nested decoding | 21,656 → 21,656 | 77,969 → 77,969 |

The hook workload allocates 4.36% less. Every run returns `100` and restores
the borrowed allowance after result disposal. Hook elapsed moves in both
directions across processes, and the medians are 3.98 s before and 4.21 s
after over 12 requests. Host timers dominate and wider CPU is coarse on this
machine. The callback-free timings also move. This establishes an allocation
result on an exercised suspended path. CPU and elapsed remain inconclusive.

Windows .NET 11 and Ubuntu .NET 10 each pass 926 public and 223 white-box
focused Release checks. These cover cancellation, terminal refusal, callback
failure cleanup, retained aliases and nested frame admission. The new mixed
hook `raw_decode` test checks the order of asynchronous callbacks and the
absolute Unicode end, also checked with local CPython.

[Raw runs](observations.jsonl) · [Summary and image hashes](diagnostic.json) ·
[Harness and procedures](procedures.tar.xz).
