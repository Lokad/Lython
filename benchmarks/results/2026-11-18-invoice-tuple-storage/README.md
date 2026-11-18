# CSV invoice tuple allocation attribution

R545 used the unchanged d9234f876 / 33025cee image on Windows on
2026-11-18. All 32 worker image files were verified. The existing 1,024-row
invoice ran with normal GC and tiering. Fifteen warm batches passed the existing
stdout and file checks during capture. The worker joined and its fixture closed.

| Allocation triggered under `FromIntegerSlots` | Module path samples | Initialized frame samples | Trigger object bytes | Share of sampled weight |
| --- | ---: | ---: | ---: | ---: |
| `ExecutableSlot[]` | 512 | 194 | 88 | 7.79% |
| `PyTuple` | 345 | 110 | 48 | 5.02% |
| Temporary reservation | 180 | 86 | 32 | 2.94% |

The trace has 9,043 allocation samples with 52 unresolved. The three rows
cover 15.75% of its total allocation weight. These weights estimate the volume
represented by sampled triggers. Retained capacity, exact per-type allocation
and removable CPU require separate evidence. The compiler and CPU gate shows the
invoice has both a four-value helper return and a separately observable
accumulator tuple stored in a dictionary. The helper call already describes
four-result unpack intent. [Compiler and CPU gate](../2026-11-18-ordered-dict-attribution/README.md)

Keep the existing representation at this checkpoint. Replacing the backing
with raw integers would need a new home for the identity of each lazily boxed
element. A managed subtype could place those slots inline, but would change
exact-type member resolution and initial-global normalization. Growing the
shared tuple header would revive the earlier size and adoption tradeoffs. The
temporary reservation accounts for 2.94% of sampled weight on its own. These
observations support investigating a replacement for the complete construction
stage if an identity-preserving layout emerges. [Earlier inline prototype](../2026-11-12-inline-tuple-prototype/README.md)

[Capture and image verification](capture.json) · [Sample counts](attribution.json) · [Trace and inspector](trace-and-inspector.tar.xz)
