# B1801 Current JSON construction and aggregation costs

The capture uses production `29e078ddd` and the existing JSON service
totals and JSON Lines helper fixtures. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 26, 2026 UTC. Workers warmed for at
least eight seconds before `cpu-clock` sampling at 199 Hz. Samples include
mediated RAM-backed I/O, output verification, fixture reset, framing and
result cleanup.

| Observation | Service totals, sync | JSON Lines, async |
| --- | ---: | ---: |
| Verified invocations during capture | 8,704 | 2,048 |
| All-thread samples | 1,233 | 1,425 |
| Samples with a resolved runtime ancestor | 1,064 | 1,372 |
| Of those, samples with an unresolved leaf | 120 | 77 |
| Nearest iterative JSON construction loop | 108 | 151 |
| Nearest converted dictionary update | 152 | 0 |
| Nearest JSON decoder entry continuation | 3 | 52 |
| Nearest memory reservation | 138 | 110 |
| Of those, nearest caller creates a record | 45 | 26 |
| Of those, nearest caller copies exact UTF-8 | 83 | 20 |
| Nearest `SliceTrimmed` | 0 | 33 |

Record assembly already writes matching keys into the admitted final
slots. The samples locate proof, allocation and useful parsing work
together. The latest service totals also spend sampled time in the
converted dictionary update. The counts provide a route inventory with
unresolved and inlined work. They give no estimate of removable CPU.
[Earlier record attribution](../2026-11-26-json-record-attribution/README.md).

The JSON Lines script uses `if line.strip()` as a blank-line guard. Its
trimmed string has a single truth consumer. Test carrying that truth
through the existing private string-result path, using the same Python
whitespace scan and preserving Unicode behavior, managed stack checks,
cancellation and dynamic receiver calls. This can eliminate a string
result and its admission for every trimmed nonblank line. The two
fixtures previously motivated separate decoder-entry and capacity
experiments, whose adverse observations remain recorded.
[Decoder entry](../2026-11-26-json-default-decoder/confirmation/README.md) ·
[Record capacity](../2026-11-26-json-matched-record-capacity/confirmation/README.md).

Both independent CPython goldens and all 32 image hashes verify. Both
quiet checks pass. Workers and profilers join, temporary guest fixtures
are removed, and the persistent archive verifies under the lease.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Verification and procedures](procedures.tar.xz).
