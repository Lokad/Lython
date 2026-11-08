# Allocation selection evidence — 2026-11-08

R94 at committed05d81b83/core68f63d29. The unchanged existing allocation-tick
collector observes the complete ordinary text/CSV scripts, including mediated
disk I/O, full validation and prior result cleanup. Compilation, startup,
128warmups and final teardown are excluded. Windows/.NET11RC3, normal GC/tiering;
256captured executions each, about2.09/2.74seconds. Diagnostic, no CPU comparison.

| Sample-associated type | Text tick share | CSV tick share |
| --- | ---: | ---: |
| PyString |44.81%|29.13%|
| Boxed BigInteger |21.61%|27.13%|
| PyDict |negligible|14.64%|
| TemporaryMemoryReservation |6.76%|11.10%|
| Object[] |4.30%|7.82%|
| Boxed Boolean |negligible|7.25%|
| Byte[] |12.06%|2.00%|
| PyList |9.20%|negligible|

Text4303ticks, CSV6358within-capture of6359total, zero lost events. Every tick
reports the type associated with an allocation interval; it does not assign all
interval bytes exclusively to that type. These are neither exact per-type bytes
nor object counts, and do not prove CPU causality or a particular redesign's gain.
All original ticks, capture boundaries, identities and unknown/unlisted types stay
in observations.tar.xz and assessment.json. No forced collection, heap dump,
rundown/root tracing or runtime instrumentation was introduced.

Full stdout/files/Ready/source/core/normal-runtime/collector stop/Closed/joins/
fixture removal and ownership checks pass. Source/core provenance links to the
[R92 artifact](../2026-11-08-shared-cased-split/README.md); no new runtime prototype.

Selection: assess generic direct comparison branches. In a condition such as
`amount < 0`, exact native operands can potentially deliver truth directly,
eliminating the boolean result box, result publication and separate truth read.
Keep one shared comparison implementation; preserve the existing general protocol
path for custom results, genuine suspension, rich-comparison ordering and cleanup.
This is a hypothesis with semantic and source-pair acceptance before retention.
It does not justify an integer-cache series, global tagged-value replacement,
new pool or memory-owner infrastructure. Existing record adoption/batched parsing
remain; the earlier CSV bulk-copy proposal cannot be revived unchanged.
