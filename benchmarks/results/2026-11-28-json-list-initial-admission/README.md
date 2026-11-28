# B1913 Admit JSON list backing at container entry

Nonempty JSON arrays and the lists passed to `object_pairs_hook` admit
the shell and initial capacity together. Previously, the first append
admitted a replacement and then returned the empty shell's charge. The
decoder bypasses that first growth and refund. Both paths keep the same
capacity for a completed list. Admission can now refuse before the first
value callback. Empty lists keep zero backing capacity.

Both complete 32-file images differ only in the Lython core DLL. The
reference is `63099731a` and the candidate is the recorded patch. Both
match three independent CPython 3.13.16 output and file goldens. The
4,096-record nested-array Lines workload is unchanged from B1833.

| Workload | Growth requests before → after | Peak run admission |
| --- | ---: | ---: |
| Nested-array Lines | 20,540 → 16,444 | 5,952,087 B |
| Flat Lines control | 12,342 → 12,342 | 3,035,454 B |
| Dense nested document | 7,446 → 7,445 | 2,592,718 B |

The counts and peaks match across hosting modes. Managed allocation
changes by at most 230 bytes per complete run in the Windows diagnostics.
Compilation admission matches. Every run returns its allowance to the
compiled baseline and disposing the script returns it to zero.

The first refusal fixture passed on both binaries. The revised test compares
the charge held during a scalar parse hook with the charge held when the same
hook runs inside a list. The reference holds just 64 B extra and fails both
hosting modes. The candidate passes, with cleanup returning both allowances.
Focused Release suites pass 1,113 public checks and 229 white-box checks per
target on Windows .NET 10 and Ubuntu WSL .NET 11. Windows .NET 11 passes the
same focused suites with the initial fixture and all 23 continuation checks
with the revised fixture.

## Local timing

November 28, 2026, on Ubuntu 24.04 WSL with .NET 11 RC3. Two workers per
image warm for at least five seconds each and contribute seven rotated
batches. Ranges span the worker-median pairings. Elapsed includes mediated
I/O, verification and previous-result disposal. Process CPU includes
fixture reset and request framing. Compilation and startup are excluded.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested-array Lines | Warm sync | −1.97% to +3.67% | −7.32% to +0.92% |
| Nested-array Lines | Warm async | −6.48% to −0.44% | −7.86% to +2.48% |
| Flat Lines control | Warm async | −1.12% to +2.37% | −0.91% to +4.65% |
| Dense nested document | Warm async | −1.72% to +2.09% | −1.90% to 0.00% |

All 112 timed batches verify. Nested-array async elapsed falls locally,
with CPU spanning zero and a 6.19% CPU spread between reference workers.
Keep the change for the removed first growth and the unchanged admitted
peaks. The dense document's timing remains inconclusive.

[B1833 list growth](../2026-11-27-list-growth-transaction/README.md) ·
[Admission counts](admission-summary.json) · [Allocation](allocation-summary.json) ·
[Timing](summary.json) · [Images](identity.json) ·
[Observations](observations.tar.xz) · [Source and checks](procedures.tar.xz).
