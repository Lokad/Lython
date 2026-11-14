# Initial checked machine-integer prototype

R357,2026-11-14. Frozen69d936e1 vs fully qualifiedf5817a15/frozen9cfb.
2,090focused actual11/10checks pass. Individual exact arithmetic reuses the
existing machine kernel; boxed BigInteger publication and major admission remain.

B731fourjobs167.976s/allgates: complete100kloop elapsed−14.73..−13.29%and
wider CPU−14.16..−13.27%; allocation flat. Complete fulfillment elapsed
−1.66..+0.35%/CPU−3.56..−0.26%; invoice−2.59..+1.98%/CPU−1.92..+1.54%;
text−4.04..+1.36%/CPU−2.64..+1.82%, with process spreads. Representative
benefits remain limited/uncertain. These are same-source warm comparisons,
excluding compile/startup/reset/protocol/final cache disposal and including
mediated I/O, output/file validation and result cleanup. No CPython gain inferred.

This implementation is superseded before delivery: expected machine overflow
would throw on each probe of repeated wider products. A nonthrowing shared kernel
using exact Int128 intermediates/explicit bounds should remove that path and
three overflow-catching boundaries across ordinary arithmetic, chains and cells.
Final code must be independently checked/timed. No initial result is discarded.

Preserved failures: typed BigInteger[] test ingress corrected to supported
object[]; original2failed cases per target retained. A four-case local allocation
process exceeded30seconds; killed/joined and owned footprint retained before
cleanup. Narrowing to two affected workloads kept the30second bound, counts,
warmup and images; four fresh processes pass. Postprocessing initially expected
CPython labels and was corrected to reference labels; raw observations unchanged.

[Decision](initial-decision.json),[timing](timing-summary.json),
[allocation](allocation-summary.json),[all sources/binary/checks/tools/raw](sources-checks-and-observations.tar.xz).
