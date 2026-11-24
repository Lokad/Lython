# B1629 Share prepared function-default storage

Executable and lowered definitions and generated signatures use one
allocator. The signature owns its ordered keyword keys. Lowered lambdas
and generated dataclass constructors can retain prepared keyword values
until a dictionary is needed. Generated positional and keyword-only
defaults follow their own parameter order even when class fields
interleave them. Default capacity is admitted before expression effects.
The callable and any published default aliases keep their existing owners.

Windows and Ubuntu each pass 817 white-box and 1,263 public .NET 11
Release checks and 104 focused .NET 10 checks, covering admission refusal,
cancellation, cleanup and retained defaults. Five CPython 3.13.2 probes match in both execution
modes on both images. They exercise lowered class-namespace lambdas,
their failure order, generated parameters, annotation evaluation order
and Unicode keyword names. Both frozen images verify four complete
script goldens.

The local allocation diagnostic agrees on managed allocation and admitted
peaks for both rule scripts and the quoted invoice. The dataclass script
allocates 483,600 B instead of 492,104 B, a 1.73% reduction. Its peak
admission falls by 4,608 B. Every allowance returns to baseline after
cleanup. The dataclass script adapts the existing inherited-configuration
example to 48 subclasses with interleaved keyword and positional defaults.

## Controlled comparison

The reference is `cdaf31164`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 24, 2026 UTC. Each of the two sealed 32-file
images runs in two independent workers. Each worker warms for at least
five seconds of request time and supplies seven rotated batches per lane.
Ranges span the four pairings of worker medians. Warm elapsed includes
applicable mediated RAM I/O, verification and prior-result cleanup. Time
to result adds compilation and compiled-owner disposal. The wider CPU
clock includes fixture reset and request framing. Startup sits outside
both clocks.

| Workload and boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted rules, warm | −1.54 to +5.45% | −1.02 to +4.48% | 5.72 / 1.30% |
| Defaulted rules, time to result | −1.39 to +15.61% | −2.02 to +16.14% | 1.01 / 16.07% |
| Eager annotated rules, warm | −5.33 to +2.34% | −3.65 to +2.48% | 2.37 / 5.61% |
| Eager annotated rules, time to result | −1.60 to +1.57% | −1.17 to +1.17% | 3.01 / 0.21% |
| Dataclass configuration, warm | −0.79 to +1.37% | −3.13 to +2.06% | 1.81 / 0.37% |
| Dataclass configuration, time to result | −0.40 to +1.40% | −2.12 to +2.33% | 1.79 / 0.01% |
| Quoted CSV invoice, warm | −1.86 to −0.58% | −3.45 to +1.27% | 0.95 / 0.35% |

All 196 batches and both quiet gates pass. Both affected rule images have
some divergent same-source workers. The largest spread is 16.07% on
candidate time to result, where the two candidate workers bracket the
reference range. The dataclass timing ranges cross zero. The quoted
invoice shows a small elapsed improvement with inconclusive wider CPU.
The contract checks, worker joins, fixture cleanup and archive complete.

## Confirmation on the same images

The repeat preserves the warmup, request clock, worker rotation and
controlled scripts for three lanes. Both defaulted-rule spreads are
below 1.1%. Time-to-result slowdown is smaller than in the first block.

| Workload and boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted rules, warm | +1.26 to +2.66% | −1.37 to +2.07% | 0.55 / 0.83% |
| Defaulted rules, time to result | +0.74 to +2.36% | −1.15 to +1.71% | 0.51 / 1.09% |
| Quoted CSV invoice, warm | −0.83 to +0.62% | −3.45 to +0.00% | 1.19 / 0.27% |

Retain the shared allocator, signature layout and lower dataclass
allocation. Record the repeated defaulted-rule elapsed cost as regression
debt for the next complete call/frame checkpoint. CPU results still span
zero. This comparison covers the whole migration and leaves the cost's
source unresolved. All 84 repeat batches, both quiet gates, joined cleanup
and persistent archival pass.

[Image identity](identity.json) · [Allocation](allocation-summary.json) ·
[Timings](quiet-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Procedures, probes and source patch](procedures.tar.xz) · [Terminal receipt](quiet-terminal-status.json).
[Confirmation timings](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation procedures](confirmation/procedures.tar.xz) ·
[Confirmation receipt](confirmation/quiet-terminal-status.json).
