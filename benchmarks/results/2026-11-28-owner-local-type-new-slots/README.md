# B1990 Publish builtin constructor descriptors on their owners

The candidate removes the eager `LythonRuntime` constructor and its shared
`__new__` lookup table. Eligible runtime types publish their descriptor on
first lookup. Names, receiver identity, inheritance and per-run ownership
follow the same rules. The images differ only in the core DLL, comparing
`adf0f7111` with the archived patch.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 611 public and
116 white-box focused Release checks. Coverage includes concurrent first
access, descriptor identity, constructor semantics and admitted lifetimes.
Four independent stock CPython 3.13.16 goldens verify on both images.

## First compilation and startup

Forty-two fresh Windows .NET 11 RC3 processes measure caller-thread
allocations during the first compile. Compiled admission matches and
returns to baseline after disposal. Compile-only EventPipe traces show
that the 8,024-byte IL runtime initializer and the unrelated datetime,
random, partial and `struct_time` initializers disappear from the capture.

| Script | Reference CLR allocation | Candidate CLR allocation | Bytes removed |
| --- | ---: | ---: | ---: |
| Configuration edit | 400,144 B | 355,312 B | 44,832 B |
| XML selection | 336,544 B | 292,088 B | 44,456 B |
| Generated read-only branches | 1,008,824 B | 961,312 B | 47,512 B |

A separate local Ubuntu WSL comparison uses 48 fresh commands. Its clock
includes process startup, compilation, execution, verified output and file
I/O, cleanup and exit. Configuration medians are 291.86 and 281.55 ms.
XML medians are 242.73 and 255.31 ms. All paired elapsed and CPU ranges
span zero.

## Warm execution and compilation

Measurements use two workers per image, five seconds of warmup and seven
rotated batches per worker. Ranges pair worker medians. Compile includes
retirement of prior compiled storage. Execution includes applicable
RAM-backed I/O, verification and result disposal. CPU also includes
fixture reset and worker framing.

In the local run, all 140 batches verify. Configuration compile CPU rises
1.62–2.23%. The configuration compile-to-result reference workers differ
by 28.44%, so that lane is particularly noisy. A confirmation on the quiet
Azure VM uses the same images and .NET 11 RC3. Both machine gates and all
112 timed batches verify.

| Quiet workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Builtin constructor descriptors | Warm execution | −4.38% to +3.05% | −3.99% to +2.36% |
| XML selection | Compile to result | −0.38% to +0.59% | −1.24% to +0.84% |
| Configuration edit | Compile to result | −1.35% to +0.83% | −3.96% to +0.64% |
| Configuration edit | Compile | −0.46% to +1.03% | −1.54% to +1.28% |

All quiet timing ranges span zero. The allocation and initializer removal
are established. The full timing evidence also includes the first launch
refused by the staging reserve and the first collector that stopped before
sampling at its disk reserve. The retry reused both verified images after
an older completed core was copied to persistent local storage and its
checksum verified. The collector and sampling gates stayed the same.

[Allocation](allocation.json) · [Initializer traces](compile-traces.json) ·
[Fresh timing](cold-summary.json) · [Local warm timing](warm-summary.json) ·
[Quiet warm timing](quiet-summary.json) · [Quiet terminal status](quiet-terminal-status.json) ·
[Local observations](warm-observations.tar.xz) · [Quiet observations](quiet-observations.tar.xz) ·
[Patch and procedures](procedures.tar.xz).
