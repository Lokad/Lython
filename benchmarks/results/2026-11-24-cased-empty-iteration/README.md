# B1557 classify empty cased splits before borrowing storage

Private `1de3cdcb4` checks for a word before borrowing scratch for
compiler-private cased iteration. A whitespace-only input uses the
existing empty tuple internally. Nonempty input starts at the first
word. Observable string splits still return fresh lists.

| Warm workload | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Short report, 1,024 blank lines in 3,072 | +3.74 to +9.00% | +2.05 to +8.33% | 2.39 / 2.62% |
| Short report, all 3,072 lines nonempty | −2.30 to +1.98% | −1.07 to +1.72% | 1.29 / 3.05% |
| Mixed-width report | −2.27 to +6.69% | −2.37 to +5.78% | 8.02 / 1.06% |
| Quoted invoice | −0.33 to +1.01% | 0.00 to +2.00% | 1.25 / 0.10% |

Keep the prototype private. The target is slower across all elapsed and
CPU pairings. The nonblank control spans zero, and the mixed report has
unstable reference workers. Local CLR allocation and admitted peaks
match on all three text scripts. On the quoted invoice the local
allocation difference is 56 bytes in more than 3.2 million, with equal
admitted peaks. The existing cursor pool already reuses its storage
during the mixed blank/nonblank report.

The empty tuple also changes the fused histogram's dispatch. It goes
through the ordinary dictionary-loop path, which loads its metadata
before observing exhaustion. That is a possible cause of the adverse
timing. A useful follow-up would need to displace that work, or
identify another actual cost of empty iteration. The paired timings
alone do not assign the regression to either.

The reference image is `eabbf02ed`, whose runtime and tests match
`000ab0632`. Both 32-file images match all four CPython 3.13.16 goldens.
On the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3,
two processes per image warm for eight seconds and provide seven
rotated batches each. Ranges cover all pairings of their medians.
Warm elapsed includes mediated RAM-file operations, golden verification
and prior-result disposal. Wider CPU includes reset and protocol.
Startup and final teardown are outside elapsed. All 112 batches,
quiet gates, joined cleanup and the persistent archive verify.

Windows and Ubuntu each pass 93 white-box and 183 public .NET 11
Release checks, including zero-allowance iteration, Unicode whitespace,
escaping list identity, terminal refusal and cancellation. The archive
keeps the exact candidate and the adverse result.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and patch](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
