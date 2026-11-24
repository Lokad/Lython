# B1558 write match expansion into final UTF-8 storage

`Match.expand()` sizes its output from the validated replacement plan
and borrows the UTF-8 ranges through one admitted result write. This
removes builder allocation, growth, replacement overlap and backing
transfer. The wide workload expands a 1,024-character Unicode capture
twice per output and observes each result's length. The short workload
uses the same named and numeric references on a two-character subject.

| Complete warm script | Elapsed change | Wider process CPU change | CLR allocated bytes | Peak admitted bytes |
| --- | ---: | ---: | ---: | ---: |
| Wide expansion, 256 calls | −31.48 to −29.40% | −33.81 to −27.74% | 4,749,840 → 1,434,128 | 2,670,090 → 1,360,394 |
| Short expansion, 1,024 calls | −20.49 to −18.11% | −19.56 to −17.29% | 526,144 → 386,880 | 167,852 → 153,516 |
| Literal expansion, 1,024 calls | −2.95 to +3.36% | −2.40 to +3.74% | 149,304 → 149,304 | 146,317 → 146,317 |
| Template substitution, 1,024 calls | −0.01 to +1.50% | +0.15 to +0.88% | 6,244,160 → 6,244,160 | 168,020 → 168,020 |

The literal control's reference-worker spread is 5.82%. The
substitution control has a small adverse CPU signal in this collection.
Its elapsed range spans zero. These are same-Lython-revision comparisons
using the optional PythonRe 0.4.0 adapter, with CPython 3.13.16 used to
verify output. They establish no new CPython ratio or backend result.

The reference `eabbf02ed` matches the runtime and tests at
`000ab0632`. Candidate `83ae1e2fa` uses the same 32-file image
apart from the core assembly. On the quiet Azure F4as_v7, Ubuntu
24.04.4 and .NET 11 RC3, each of two independent workers per source
warms for eight seconds and contributes seven rotated batches.
Ranges compare all pairings of medians. Warm elapsed includes full
guest execution, output verification and prior-result disposal. Wider
CPU includes resets and transport. Compilation, process startup and
final teardown are outside the elapsed clock. All 112 timed batches,
quiet gates, worker joins, fixture cleanup and persistent archive
verification pass. The Windows allocation comparison is separate,
using medians of six invocations with allowance returned to baseline.

Each platform passes 73 white-box and 493 public .NET 11 Release
checks. Under the tightened 36 KiB headroom fixture, the reference
refuses the 32 KiB builder's growth and the candidate retains the
correct expansion with its exact storage still funded. Existing
checks preserve terminal refusal, cancellation, capture semantics
and cleanup. The initial golden setup assertion counted one extra
character in the expected expansion and was corrected before any
benchmark workers ran.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures, checks and patch](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
