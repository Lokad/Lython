# B1989 Compact shared builtin-module index

This refinement replaces the two factory delegate sets from B1988 with
one name-to-module index, separate construction switches and a published
member snapshot per accessed module. Configuration compilation materializes
only `json`, XML selection materializes only `xml.etree.ElementTree`, and
the generated branch script leaves every member snapshot dormant. Sixteen
concurrent readers agree on the published member and export snapshots.

The two images agree on all 66 modules, 874 members, 871 exports, 2,138
member queries and 13 compiler diagnostic sequences. Windows .NET 10 and 11
and Ubuntu WSL .NET 11 each pass 740 public and 67 white-box focused Release
checks. Four stock CPython 3.13.16 goldens match both images.

## First compilation and fresh commands

November 28, 2026 UTC. The images differ only in the core DLL, comparing
`979e1ca3d` with the archived patch. Forty-two fresh processes on Windows
.NET 11 RC3 measure caller-thread allocations during the first compile.
Admitted charges match and compiled storage refunds to baseline.

| Script | Reference CLR allocation | Candidate CLR allocation | Bytes removed |
| --- | ---: | ---: | ---: |
| Configuration edit | 400,992 B | 384,984 B | 16,008 B |
| XML selection | 336,400 B | 321,336 B | 15,064 B |
| Generated read-only branches | 1,008,824 B | 1,008,824 B | 0 B |

The 48 fresh commands on Ubuntu WSL use verified native-disk images and
RAM-backed fixtures. Elapsed spans process startup, compilation, execution,
I/O, verification, cleanup and exit. Configuration median elapsed changes
from 254.64 to 259.71 ms, and XML from 227.86 to 230.87 ms. Median process CPU
changes from 216.98 to 216.59 ms and from 188.73 to 187.97 ms respectively.
All paired elapsed and CPU ranges span zero.

Compile-only JIT captures verify matching runtime initializer lists and no
builtin module resolution in both images. The candidate replaces the eager
8,882-byte IL initializer with a name index and on-demand member construction.
The 8,024-byte IL initializer for `LythonRuntime` runs in both captures and
still creates type constructor wrappers. The initial trace assertion expected
that initializer to be absent. The unchanged baseline disproved it, and the
corrected check retains that trace.

## Warm compilation and execution

Ubuntu WSL, .NET 11 RC3, one logical CPU. Two workers per image warm for
five seconds and contribute seven rotated batches each. Ranges pair worker
medians. Compile includes retirement of prior compiled storage. Execution
includes applicable RAM-backed I/O, verification and result disposal. CPU
also includes fixture reset and worker framing. All 140 batches verify.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration edit | Compile | −1.51% to +0.71% | +0.19% to +1.74% |
| Configuration edit | Compile to result | +0.38% to +5.40% | +1.81% to +6.45% |
| XML selection | Compile to result | +2.06% to +3.71% | +1.02% to +6.69% |
| Generated read-only branches | Compile | +0.96% to +1.66% | −0.39% to +1.91% |
| Builtin module imports | Warm execution | +0.21% to +1.09% | −1.56% to +1.28% |

The compact index stays shelved. It saves first-compile allocation, and the
fresh timing remains unresolved. The adverse warm configuration observation
persists across both local designs, and this variant also uses more warm CPU
for XML. The original registries remain in production.

[Allocation](allocation.json) · [Fresh timing](cold-summary.json) ·
[Warm timing](warm-summary.json) · [Image identity](identity.json) ·
[Warm observations](warm-observations.tar.xz) · [Patch, traces and procedures](procedures.tar.xz) ·
[First variant](../2026-11-28-shared-module-catalog-trial/README.md).
