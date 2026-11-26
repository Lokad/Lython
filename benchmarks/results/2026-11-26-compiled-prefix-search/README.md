# B1814 Prepare counted prefix search during compilation

The candidate on `f5fc7eb9a` moves the fixed
loop-shape proof from B1813 into backedge emission. The existing
`ForNext` instruction retains the count slot. No separate traversal or
metadata table is needed. The runtime checks window readiness before
reading the mutable helper and counter. A group of rejected lines uses
one admitted Python line view and publishes the final line and count
before another ordinary pull. The reader retains its existing refill,
close and newline behavior. Custom reservations continue on the ordinary
path.

The exact loaded core marks one loop in the search tape and none in the
Unicode file-report control. Both 32-file measurement images match three
independent CPython output and file goldens. Windows .NET 11 and Ubuntu
.NET 10 each pass 118 white-box and 857 public focused Release
checks. The five additional file shapes match CPython 3.13.2. The Linux
subprocess checks initially failed to locate a matching probe. Both
targets reran with snapshot-built probes verified against their matching
test core. Original logs remain in the procedures archive.

## Allocation and admission

Windows .NET 11 uses the existing shared-budget diagnostic. Both APIs
return the allowance to baseline after each run.

| Workload | Mode | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Match at line 1,025 | sync | 113,280 → 56,160 | 167,072 → 36,512 |
| Match at line 1 | sync | 39,288 → 39,288 | 19,556 → 19,556 |
| Unicode file-report control | sync | 322,992 → 322,992 | 603,795 → 603,795 |
| Match at line 1,025 | async | 113,528 → 56,408 | 167,072 → 36,512 |
| Match at line 1 | async | 39,536 → 39,536 | 19,556 → 19,556 |
| Unicode file-report control | async | 323,240 → 323,240 | 603,795 → 603,795 |

## Local timing

Ubuntu WSL uses two workers per source, three seconds of warmup and
seven rotated timed batches per worker. Ranges span all four worker-median
pairings. Negative changes mean less time. Warm elapsed includes mediated
RAM-backed I/O, output verification and previous-result disposal.
Through-result timing also includes compilation and compiled-script
disposal. Wider process CPU includes fixture reset and request framing.
Startup is outside these clocks. All 140 batches completed and the
archive verified with worker and fixture cleanup confirmed.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Match at line 1,025 | Warm async | −54.41% to −49.40% | −46.42% to −40.94% |
| Unicode file-report control | Warm async | −2.42% to +4.64% | −2.16% to +3.12% |
| Match at line 1,025 | Warm sync | −53.89% to −52.44% | −50.09% to −49.45% |
| Match at line 1 | Warm async | +2.46% to +3.03% | −1.42% to +6.73% |
| Match at line 1,025 | Through result | −35.85% to −31.73% | −30.68% to −28.23% |

Preparing the shape at emission narrows the early-match elapsed cost
from B1813's 3.90–12.76% to 2.46–3.03% on this machine. Early-match CPU
spans zero, and Unicode file-report elapsed and CPU span zero. The
large late-match gains agree with the allocation and admission changes.
The early elapsed cost remains visible. This run is local and compares
two Lython builds. The quiet VM had 3.9 MB free, so this experiment did
not collect a quiet or CPython timing result.

Delivery review found a case missed by the initial regression set. A
forwarded global increment in a function uses the same encoded operand
space as constants. The fixed-shape proof indexed the constant array
with that global operand and failed to compile the script. The private
source timed here needs a bounds check and a regression before delivery.

[Initial trial](../2026-11-26-buffered-prefix-search/README.md) ·
[Allocation and admission](allocation-summary.json) · [Timing and spreads](summary.json) ·
[Frozen image hashes](identity.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Checks, tapes and exact patches](procedures.tar.xz).

[Corrected source and delivery check](correction/README.md).
