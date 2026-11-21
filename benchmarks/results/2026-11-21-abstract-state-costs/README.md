# Abstract interpretation state allocation

Local Windows .NET 11 RC3 attribution on `d09acaf69` with private
instrumentation. The three B1268 sources run through production
analysis on a borrowed allowance that returns to baseline.
Counters are enabled during abstract interpretation. Four batches
of 48 compiles follow warmup. The allocations below are per script.
The instrumented image serves attribution only.

| Complete source | Abstract analysis allocation | State copies and allocation | Invalidation snapshots | Argument partitions |
| --- | ---: | ---: | ---: | ---: |
| Generated validator | 321,765 B | 193 / 92,824 B | 586 / 28,080 B | 392 / 13,296 B |
| Inherited dataclass configuration | 85,416 B | 50 / 24,184 B | 99 / 3,992 B | 50 / 1,600 B |
| Postponed nested factories | 7,664 B | 5 / 2,400 B | 7 / 344 B | 6 / 200 B |

The validator snapshots 1,170 named entries across its invalidations
and rewrites two values. The count excludes removals. State copies
are the larger target. The existing flow distinguishes value facts,
length-only facts, lexical invalidation and diagnostic span provenance.
A replacement needs to preserve those distinctions and eliminate
a whole map copy or repeated state staging. Small argument partitions
alone have a lower allocation ceiling. These counts establish neither
a removable CPU share nor a speedup.

[Raw samples and image hash](detail.json) · [Probe source and exact catalog](probe.tar.xz).
