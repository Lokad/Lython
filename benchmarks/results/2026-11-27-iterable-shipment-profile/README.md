# B1882 Attribute the iterable shipment report

On November 27 the existing B1881 worker ran on WSL pinned to CPU 27, with
.NET 11 RC3 and the original CSV fixture in RAM. The frozen core is
`ab883268b`. The image and fixture hashes match B1881. The collector
verified 68 batches and joined every worker. The archive includes the
initial launch that rejected the fixture directory before sampling.

| Report | Host API | Verified commands | Generator advancement¹ | Iterable row invocation¹ | Argument expansion¹ | Buffer allocation¹ |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Four-yield iterable rows | `Run` | 1,344 | 63.0% | 52.4% | 37.4% | 1.4% |
| Four-yield iterable rows | `RunAsync` | 1,024 | 65.8% | 51.7% | 38.1% | 1.6% |
| Five-item list rows | `RunAsync` | 1,984 | 13.7% | — | — | — |

¹ Inclusive share of sampled time with a resolved Lython runtime frame,
after excluding observed thread-wait and GC-poll stacks. The regions
overlap. Generator advancement includes the outer generator and useful
work in the row body. Argument expansion includes generator execution.
These are six-second managed-stack samples used for attribution.
Use the B1881 comparison for whole-command elapsed and process CPU.

The profiles support investigating the generator transport ahead of a
new argument-buffer cache. The current positional drain materializes a
private cursor for each row, takes the first yield separately, then fills
spare admitted argument capacity through the existing buffered path.
Test removing that initial handoff for same-command generators while
retaining the ownership and checks at both ends.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Run summary](run-summary.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) ·
[B1881 comparison](../2026-11-27-wsl-shipment-cpython/README.md).
