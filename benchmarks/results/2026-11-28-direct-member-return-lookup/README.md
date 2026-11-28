# B1995 Direct frontend member-return lookup

The trial removes a shared dictionary with 406 fallback member facts and the
two temporary arrays that initialize it. Receiver kind selects the applicable
name lookup. Existing builtin member catalogs remain the first lookup. The
images compare the landed `8633debf8` runtime with the archived patch on
November 28, 2026 UTC. Only the core DLL differs.

Retain direct lookup for the removed initialization stage and lower first
compilation allocation. Timing for the affected paths remains diagnostic.

## Correctness and first compilation

All 52,608 reflected lookup queries agree across receiver kinds, old member
names, uppercase probes and missing names. The 450 hits include all 46 explicit
unknown return shapes. The same 304 focused Release tests pass on each of
Windows .NET 10 and 11 and Ubuntu WSL .NET 11. They cover frontend diagnostics,
compiler cancellation, shared admission and frontend regex binding. The
unchanged comparison workers verify four independent CPython output and file
goldens on both images.

Seven fresh Windows .NET 11 RC3 processes per image and workload measure
caller-thread allocation for first compilation. Admission matches and returns
to zero after script disposal in all 42 observations.

| Workload | Reference CLR allocation | Candidate CLR allocation | Bytes removed |
| --- | ---: | ---: | ---: |
| Configuration edit | 355,168 B | 332,976 B | 22,192 B |
| XML selection | 292,088 B | 269,896 B | 22,192 B |
| Generated read-only branches | 961,312 B | 961,312 B | 0 B |

In the configuration compile-only EventPipe captures, the shared dictionary
initializer and its two array factories, previously 5,112 and 4,056 IL bytes,
disappear. The compiler loads the receiver dispatcher and the text-file lookup.
The 48 local fresh commands use Ubuntu WSL, .NET 11 RC3, verified native-disk
images and RAM fixtures. Configuration launch-through-exit elapsed medians are
260.30 and 257.21 ms. XML medians are 233.04 and 229.29 ms. Both paired elapsed
and CPU ranges span zero. Startup timing remains inconclusive. The trace clocks
describe instrumented method compilation and are separate from these results.

## Warm comparison

Two workers per image run on one logical CPU in Ubuntu WSL with .NET 11 RC3.
They warm for five seconds and supply seven rotated batches. Ranges pair worker
medians. Compile includes disposal for prior compiled storage. Compile-to-result
adds applicable RAM-backed I/O, output and file checks and result disposal.
Process CPU also includes fixture reset and framing. An initial setup stopped
before timing because it inherited `DOTNET_BUNDLE_EXTRACT_BASE_DIR`. Its
receipt and checks are preserved. The retry uses the same images and catalogs.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration edit | Compile | −0.31% to +2.39% | −1.91% to +1.23% |
| Configuration edit | Compile to result | −4.45% to −1.12% | −1.82% to +1.15% |
| XML selection | Compile to result | −2.62% to +1.50% | −1.88% to +2.17% |
| Generated read-only branches | Compile | −1.18% to +2.62% | +0.08% to +1.12% |
| Builtin constructor control | Warm execution | −0.99% to +4.66% | −1.59% to +5.41% |

The matching confirmation on a four-vCPU Azure F4as_v7 uses Ubuntu 24.04.4
and .NET 11 RC3. It repeats the whole-script boundaries plus the generated
and constructor controls with the same worker count, warmup and batches.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration edit | Compile to result | −1.43% to +0.42% | −1.96% to +0.83% |
| XML selection | Compile to result | −0.39% to +0.46% | −0.92% to +1.24% |
| Generated read-only branches | Compile | −2.65% to +0.25% | −3.33% to −1.20% |
| Builtin constructor control | Warm execution | −7.11% to +3.39% | −6.95% to +4.16% |

The affected whole-script timing ranges cross zero on the quiet VM. The
generated control changes direction between collections. The reference
constructor workers differ by 10.19% in quiet process CPU, leaving that
unchanged control inconclusive. Preserve the local adverse observations.
All 140 local and 112 quiet batches verify. Both machine gates pass, the
workers join, and the RAM fixtures are removed before persistent archival.
The stage reuses 63 verified worker files. It follows the verified lossless
transfer of two older completed library images to persistent local storage.

[Allocation](allocation.json) · [Initializer traces](compile-traces.json) ·
[Fresh timing](cold-summary.json) · [Local warm timing](summary.json) ·
[Quiet warm timing](quiet-summary.json) · [Validation](validation.json) ·
[Local images](identity.json) · [Quiet images](quiet-identity.json) ·
[Local observations](observations.tar.xz) · [Quiet observations](quiet-observations.tar.xz) ·
[Quiet terminal status](quiet-terminal-status.json) · [Patch and procedures](procedures.tar.xz).
