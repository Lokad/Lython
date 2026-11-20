# Ordered class header arguments

The parser keeps one ordered argument list for a class header. The
compiler lowers each expression once. The change deletes the parallel
base and keyword lists, their lowered copies and the alternate path for
an absent list. The existing executable and lowered class paths still
use the same expanded argument order and cleanup.

The reference is `551fde44e`. The measured source is `3704eb850`,
retained at `9d13432fb`. Windows .NET 11 and Ubuntu .NET 10 each
passed 1,133 public and 143 white-box focused Release checks. Coverage
includes expanded bases and keyword options, metaclass effects,
suspending class headers, generic scopes and inheritance.

Both 32-file Windows .NET 11 RC3 images passed three CPython 3.13
goldens. The local allocation diagnostic compiles and disposes each
script separately. Warm checks use the same scripts and shared allowance.

| Workload | Compilation allocation | Warm allocation |
| --- | ---: | ---: |
| Generated configuration with 48 inherited dataclasses | 645,192 → 626,272 B (−2.93%) | 491,904 B on both |
| Annotated rules control | 57,872 → 57,768 B (−0.18%) | 1,439,240 B on both |
| Config manifest control | 286,768 → 286,464 B (−0.11%) | 103,024 B on both |

All warm accounted peaks match and allowances return to baseline.
Retained for simpler representation and lower compilation allocation.
CPU and elapsed were outside this diagnostic.

[Source, images and observations](checks-and-procedures.tar.xz).
