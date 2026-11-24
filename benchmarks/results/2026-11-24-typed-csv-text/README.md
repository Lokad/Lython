# B1550 Keep short text in typed CSV slots

Retained `b3c447420` removes the quoting-mode exception that eagerly
published short text fields during fixed unpack. Parsed numbers retain
their existing representation and error timing. Typed and ordinary CSV
now use the same admitted UTF-8 slots. Exact dictionary accesses use the
short key bytes, and observable reads still publish a canonical string.

The reference is `583d0e80b`, with the same runtime source as the parent
`66a034a0c`. Both Release images pass the same four stdout and file
goldens against CPython 3.13.16. The observing variant also takes the
length of every short key before the dictionary operations, so it measures
the ordinary publication route. Focused checks on Windows and Ubuntu
each pass 60 public and 128 white-box tests, including typed and ordinary
canonical key identity, async file disposal, numeric failures and budget
reuse.

November 24, 2026 UTC, Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3.
Two workers per image warm for eight seconds and contribute seven
rotated batches. Ranges span all worker-median pairings. Warm elapsed
includes mediated RAM-backed I/O, goldens and disposal of the previous
result. Process CPU includes request handling and reset. Local allocated
bytes are medians of six complete Windows warm invocations.

| Complete script | Warm elapsed change | Process CPU change | CLR bytes before → after |
| --- | ---: | ---: | ---: |
| Typed Unicode CSV meter | −13.05% to −10.83% | −11.64% to −9.80% | 1,475,362 → 1,189,318 |
| Typed meter, all keys observed | −1.80% to +2.13% | −3.85% to +1.82% | 1,606,288 → 1,549,784 |
| Explicit-float meter control | −1.92% to +1.34% | −1.80% to +1.84% | 991,304 → 991,528 |
| Quoted invoice control | −0.69% to +1.29% | −0.55% to +1.27% | 3,280,436 → 3,280,676 |

Typed meter allocation falls 19.39%. The observer still allocates 3.52%
less because it leaves other fields unobserved. Both unchanged controls
have flat allocation. Admitted peaks match for all four, and every
local invocation restores the budget baseline. The observer and
unchanged controls span zero on timing. All 112 batches pass. The
workers join, quiet gates pass, RAM fixtures are removed and the archive
verifies under lease.

[Allocations](allocation-summary.json) · [Image hashes](identity.json) ·
[Timing](summary.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures and patch](procedures.tar.xz).
