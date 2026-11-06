# Current whole-script CPU gate — B349

Runtime `66ae029d`, source-equivalent frozen core `977da8d6`, unchanged worker
`b94fa98c`/protocol2. Complete text/helpers896 and CSV/functions480 jobs after
six guest warmup seconds. Two captures:2.08 and2.10 active-guest CPU seconds,
414 and419 samples;29 and22 unknown leaves. Normal GC/tiering, perf-map-only3,
targeted prefix/publication JIT listings. This is attribution, not a speedup test.

All87 text and54 CSV selected-method samples map to a unique own-process Tier1
listing with the exact mapped code size. Partial final map lines remain excluded
and preserved. Owner/method grouping excludes return and argument type names;
signature substring matching had inflated accounting attribution and was corrected.

Namespace publication is not a dominant sampled phase: retain annotation/lambda
lifetime semantics and defer that redesign. Prefix leaves70text/51CSV, local
completion17/3, CSV parser41leaf/72inclusive; inclusive counts overlap useful work.
CSV has30 leaf `__tls_get_addr` samples, including actual native indexing/lookup
paths; text has6. These are leads, not a removable percentage or attribution of
all TLS/locks to accounting. Explicit dictionary context is selected for a bounded
source pair. Universal values/frames and cancellation removal are not justified.

Quiet endpoints, actual loaded hashes/TIDs, complete goldens/funding/Closed,
joined workers and removed RAM fixtures pass. Raw evidence is persistent and was
archived and verified under the collection lease before it ended. Initial setup
headroom failure and the unchanged-threshold recovery are retained; no collection
was submitted during that failed setup. No production source changed for profiling.
