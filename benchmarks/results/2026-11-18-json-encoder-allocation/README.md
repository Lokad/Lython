# Allocation attribution for manifest JSON encoding

R542 sampled the retained bdab88c54 / 7170869d worker on Windows on
2026-11-18. The unchanged config-manifest script runs on a scaled fixture
with 256 declared targets, 192 emitted targets, 36,677 input bytes and
60,627 bytes across the two output files. The fixture was checked against
CPython 3.13.2 and all 32 frozen image files were verified.

The warmed allocation capture completed 17 verified batches. Of 4,031 GC
allocation samples, 165 named the encoder's EmitFrame type (4.07% of
weighted ticks). Native dictionary entry iteration and its version wrapper
account for 2.36% and 0.71% of weighted ticks. These dictionary types also
serve other operations. The capture includes mediated I/O, reset, result
validation and protocol, so weighted ticks give approximate allocation
attribution. Exact per-type bytes and CPU impact remain unmeasured.

The encoder currently creates a heap frame for each nested container. Each
sequence adds an enumerator even though the builtin sequence is already
indexed. An isolated prototype can replace those two allocations with
governed frame capacity and a live sequence position. Preserve the sorted
pair snapshot, dictionary version checks, callback order, pending writer
funding, cycle checks and cleanup. Measure complete script costs before
deciding whether to retain the representation.

[Sample weights](allocations.json) · [Capture identity](capture.json) ·
[Raw trace, fixture and verification](trace-and-catalog.tar.xz)
