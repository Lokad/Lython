# B1947 Store frontend module members once

The candidate replaces the frozen membership set and separately sorted
member array with one ordinal array per module. Construction reuses and
deduplicates the declaration's array. Binary search uses the same array
as member enumeration. A module creates a filtered export snapshot only
when requested and can share the member array when every name is exported.
Publication preserves a stable snapshot for concurrent callers.

The exact reference and candidate match all 66 module inventories, 874
declared members, 871 exports, 2,138 member queries and 13 diagnostic
cases. Those queries include metadata, submodules, reexports, case
variants and missing names. Windows .NET 10 and 11 and Ubuntu WSL .NET 11
each pass 691 public and 67 white-box focused Release checks. The two
32-file images differ only in the core DLL and verify four stock CPython
3.13.16 output and file goldens. The reference contains `b2aed5489`.

## First compilation and fresh commands

On November 28, 2026, the Windows .NET 11 witness compiles once in each
of 28 fresh processes, after preparing the source and engine. It measures
caller-thread CLR allocation and elapsed across the compiler invocation.
Admitted compilation bytes match and both scripts refund to zero.

| Script | Before | Candidate | CLR bytes removed | Median compile elapsed, before → candidate |
| --- | ---: | ---: | ---: | ---: |
| Configuration edit | 717,752 B | 550,760 B | 166,992 B | 114.19 → 112.89 ms |
| XML selection | 654,096 B | 487,112 B | 166,984 B | 108.46 → 111.92 ms |

Paired first-compile elapsed changes span zero for both scripts. A separate
local WSL run takes 48 verified timings for the same scripts in fresh
processes. Both images are copied to persistent native WSL storage and
verified. The parent measures process launch through exit and pipe drain.
The child includes runtime initialization, compilation, execution, applicable
I/O, verification and cleanup. Twelve rounds rotate case and image order.

| Script | Median elapsed, before → candidate | Median process CPU, before → candidate | Paired CPU change |
| --- | ---: | ---: | ---: |
| Configuration edit | 278.01 → 272.17 ms | 226.33 → 223.21 ms | −8.10% to +3.74% |
| XML selection | 249.34 → 240.37 ms | 198.29 → 197.19 ms | −3.12% to +3.10% |

Fresh elapsed and CPU are inconclusive. The initial fresh collector
stopped during priming when it expected an output-file digest for the XML
case. The corrected verifier handles the optional digest and reuses both
complete images. The first failure is preserved.

## Warm compilation and execution

The local WSL comparison uses .NET 11 RC3 on one logical CPU. Two workers
per image warm for five seconds and supply seven rotated batches. The ranges
pair all worker medians. Compile includes retirement of the prior compiled
result. Compile-to-result also includes execution, applicable RAM-backed I/O,
verification and result disposal. Process CPU includes fixture reset and
framing. All 140 batches verify.

| Script | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration edit | Compile | −5.53% to +19.06% | −13.38% to −0.73% |
| Configuration edit | Compile to result | −1.37% to +2.73% | −3.51% to +1.18% |
| CSV meter report | Compile | −3.06% to −0.95% | −1.20% to +0.27% |
| XML selection | Compile to result | +1.28% to +18.10% | +0.66% to +24.05% |
| Function-free branches | Compile | −3.09% to −0.30% | +0.26% to +1.94% |

Candidate CPU medians differ by 12.56% for configuration compilation and
20.52% for XML compile-to-result. Those two lanes were selected for a
bounded quiet-VM confirmation with unchanged images and inputs. The
confirmation favors the new surface and supports retention of the storage
simplification. [B1948](quiet/README.md).

[Warm timing](summary.json) · [Fresh timing](cold-summary.json) · [Allocation](allocation.json) ·
[Image identity](identity.json) · [Warm observations](observations.tar.xz) · [Procedures and parity](procedures.tar.xz).
