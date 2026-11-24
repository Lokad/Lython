# B1554 Batch JSON scalar output in one window

Private `c9746215b` compares with `eabbf02ed`. Bounded scans recognize
unescaped ASCII strings. Those strings and small numeric keys write
their quotes and content through one admitted builder window. The
existing escape and Unicode path handles the remaining strings. The
shape changes the requests that cross a doubling boundary in the
underlying builder.

Both frozen images pass four stock CPython 3.13.16 stdout and file
goldens. The escaped variant appends newline, non-ASCII, quote and
backslash characters to every name. The candidate passes 909 public
and 221 white-box JSON-focused Release checks on Windows. Six local
complete warm invocations per script restore the budget baseline
every time.

| Workload | Median CLR bytes before → after | Admitted peak before → after |
| --- | ---: | ---: |
| Expanded export | 5,523,686 → 5,556,558 | 6,157,312 → 6,169,600 |
| Default export | 4,938,966 → 4,938,966 | 6,228,493 → 6,228,493 |
| Escape-heavy export | 7,101,990 → 7,102,850 | 7,503,356 → 7,503,356 |
| Unchanged positional calls | 11,712 → 11,712 | 1,284 → 1,284 |

Keep this shape private. The proposed reduction in growth storage
doesn't appear on the matched scripts, and the expanded export uses
more admitted and CLR memory. CPU and elapsed are unmeasured. A
future design should identify an actual redundant replacement or
intermediate rather than infer a saving from the number of append
operations.

[Allocation and admission](allocation-summary.json) · [Image hashes](identity.json) ·
[Raw diagnostics, fixtures and patch](observations.tar.xz).
