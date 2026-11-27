# B1876 Resolve identifiers directly from abstract flow bindings

The abstract resolver reads identifiers from the existing name binding map.
It no longer copies their resolved or missing values into the syntax-keyed
cache. The remaining cache is created on demand. Parentheses are unwrapped
once and one resolver dispatch handles uncached values. Computed results keep
their original cache identity and invalidation, including scalar results
captured before an effectful call changes collection facts.

The frozen reference is `50392d6fa`. Only the core DLL differs between the
32-file images. Both match four independent CPython 3.13.2 goldens. The
compiler probe compares 17 scripts across 131 budget levels. Instruction
tapes, locations, captures, named bindings, admission and failure records
match, and cancellation restores the allowance. Windows Release .NET 10 and
.NET 11 and Ubuntu WSL .NET 10 pass 598 public and 832 white-box checks each.

| Workload | CLR bytes per compilation and disposal | Peak admission |
| --- | ---: | ---: |
| Ordinary definitions | 1,443,248 → 1,410,040 | 889,975 B |
| Escaping definitions | 1,477,752 → 1,444,544 | 973,783 B |
| Guarded report | 2,243,456 → 2,165,120 | 2,410,080 B |

Allocation is the median of 17 fresh Windows .NET 11 compilations after
warmup. The reported peaks are equal on both images.

## Local timing

Ubuntu WSL and .NET 11 RC3, two workers per image on one logical CPU.
Each worker warms for five seconds and contributes seven rotated batches.
Ranges span all pairings of worker medians. Compile-only includes compiled
owner disposal. Through-result adds execution, verification and cleanup.
The warm nested-read control also includes mediated RAM-backed I/O. The
process CPU clock further includes reset and request framing. Startup is
outside these lanes.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Compile only | −5.66% to +10.93% | −9.60% to +4.26% |
| Escaping definitions | Compile only | −6.22% to −2.15% | −6.75% to +2.14% |
| Guarded report | Compile to result | −5.37% to −1.40% | −4.15% to +0.44% |
| Guarded report | Compile only | −3.35% to +4.69% | −4.75% to −0.36% |
| Nested read control | Warm | −5.96% to +2.03% | −9.47% to +3.57% |

Keep the simpler resolver and the allocation saving. Ordinary compile
timing spans zero with 10.33% same-image reference CPU spread. The nested
read has 10.04% candidate spread. The timing remains diagnostic. All 140
batches verify, all workers join and the raw archive verifies.

[Timing](summary.json) · [Compiler parity](compiler-parity.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch and checks](procedures.tar.xz).
