# Defer native call continuation ownership until transfer

The private `29cb3439a` build moves inline call promotion into a cold
method. Completed native calls avoid initializing the continuation owner
from the current argument lease. The existing output lease holds the
pending owner until promotion succeeds or cleanup releases it. Stack
admission, interruption, exception context and pending-child cleanup
still use the existing paths. With tiering disabled for code inspection,
`StartInline` drops from 896 to 509 native bytes and the cold method adds
431. That confirms a smaller hot method. The measured workers use
ordinary tiering.

| Collection | Script and boundary | Elapsed change | Process CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| B1541 | Short report, warm | −6.21 to −0.51% | −6.22 to −0.79% | 5.31 / 0.72% |
| B1541 | Helper invoice, warm | −3.19 to +0.35% | −3.66 to +1.23% | 2.75 / 0.89% |
| B1541 | Supplied function, warm | −2.00 to −0.11% | −1.71 to +0.22% | 0.86 / 1.06% |
| B1541 | Postponed factories, compile to result | −2.17 to +4.08% | −1.67 to +3.73% | 1.09 / 5.23% |
| B1542 | Postponed factories, compile to result | −1.80 to −0.30% | −1.84 to +0.89% | 0.56 / 0.96% |
| B1542 | Short report, warm | −2.73 to +0.11% | −3.37 to +0.26% | 1.76 / 1.14% |

The more stable confirmation leaves the short report's elapsed and CPU
ranges overlapping zero. Factory elapsed moves favorably, with CPU still
overlapping zero. The prototype stays private. The smaller hot method
alone gives insufficient reason to add a separate promotion method and
unpublished-output state. Revisit continuation ownership when a design
removes an actual transfer or backing owner.

The quiet Azure F4as_v7 on Ubuntu 24.04.4 and .NET 11 RC3 used
`43688c753` as the reference on November 23, 2026 UTC. Only the core DLL
differs across the 32-file images. Each range compares all pairings of
medians from two processes per source, with seven rotated samples each.
The initial collector recorded a 12-second warmup but used an eight-second
loop threshold. The confirmation uses 12 seconds for both and reverses
lane and initial worker order. A first confirmation launch stopped at the
12 MiB free-space gate before workers started. Reusing both frozen images
and reserving 1 MiB for two lanes of receipts above the 10 MiB workspace
margin allowed the collection. The original condition is archived.

Warm elapsed includes mediated I/O, output and per-result disposal.
Compile-to-result adds compilation and its owner cleanup. Wider process
CPU includes reset and verification. Process startup is outside these
clocks. All four CPython goldens, 168 timed batches, the quiet gates and
cleanup verify. The private change passed 663 focused Windows .NET 11
white-box and 1,651 Ubuntu .NET 10 public Release checks.

[Initial timing](initial-summary.json) ·
[Confirmation timing](confirmation-summary.json) · [Image](identity.json) ·
[Initial raw](initial-observations.tar.xz) · [Confirmation raw](confirmation-observations.tar.xz) ·
[Procedures and source](procedures.tar.xz) · [Code size](jit-summary.json).
