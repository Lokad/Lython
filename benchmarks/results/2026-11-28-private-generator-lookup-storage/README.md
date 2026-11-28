# B1951 Borrow the definition table for private generator lookups

The candidate against `68ee4b4db` uses a prepared range of the enclosing
function's admitted table for record-only sites in a private `sum` generator
expression. It eliminates one lookup array and its admission per activation
after first use of that function's table. Cleanup clears the borrowed range.
Reentrant lookups check the current record layout and key and read the live
slot. The table retains no row or row value. Other call targets and public
generator expressions keep their own lookup storage.

The frozen layouts preserve every opcode sequence. The ordinary reduction
adds one definition slot and the range reduction adds two. Both controls
keep their original cache counts. The first use of a function's table may
admit the prepared ranges before their use, a conservative tradeoff.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 742 public and
211 white-box focused Release checks. A new regression resumes a recursive
reduction after host I/O and reads mutated records across multiple lookup
sites. Both 32-file images verify four CPython 3.13.16 output and file
goldens. Only the core DLL changes between images.

The separate Windows .NET 11 witness shows 98,280 B less CLR allocation
per ordinary `Run` and 98,224 B less per `RunAsync`. The range workload
saves 147,408 B on either API. Both reductions admit 32 B less at peak.
The two controls' CLR allocations differ by at most 56 B at unchanged
peak. Compiled reservation matches and every command refunds to baseline.

## Warm execution

November 28, 2026 in Ubuntu WSL on .NET 11 RC3. Two workers per image
share one logical CPU, warm for at least five seconds each and contribute
seven rotated batches. Ranges pair all worker medians. Warm elapsed includes
applicable RAM-backed I/O, verification and prior-result disposal. Process
CPU also includes fixture reset and framing. Compilation and startup are
outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary tuple reduction | `Run` | −6.44% to −2.98% | −4.29% to −1.96% |
| Ordinary tuple reduction | `RunAsync` | −4.80% to −3.21% | −5.35% to −0.22% |
| Range reduction | `Run` | −5.15% to +0.32% | −4.69% to −0.01% |
| Range reduction | `RunAsync` | −1.03% to +2.57% | −2.66% to +0.21% |
| Iterable shipment | `RunAsync` | −4.81% to −0.97% | −1.49% to +2.75% |
| File-fed quantity map | `RunAsync` | −0.72% to +9.22% | −1.07% to +6.00% |

All 168 batches verify, all workers join and persistent archival verifies
after fixture removal. Retain for the eliminated storage and favorable local
ordinary timings. Range elapsed and both control CPU comparisons span zero.
The file-fed control reaches +6.00% CPU with 5.65% candidate worker spread.
The earlier file-fed timing uncertainty stays open for the combined quiet
checkpoint. [B1950](../2026-11-28-sum-source-selection/baseline/README.md).

[Timing](summary.json) · [Allocation](allocation.json) ·
[Lookup layout](lookup-layout.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Source and procedures](procedures.tar.xz).
