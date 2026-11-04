# Current ordinary text/report CPU attribution

2026-11-04. Runtime **5c827e68**, frozen **21cddae7**; documentation commit8c63fe63.
Existing complete text/report scripts, ordinary GC/tiering, quiet Azure F4as_v7.
External perf199Hz/frame-pointer capture; only symbolization-map override. Six
seconds guest warmup,2048 full invocations each; actual guestCPU6.00/8.69seconds,
1,191/1,730 samples. Both loaded-core identities/goldens/quiet gates/joins pass;
service inactive/success/MainPID0, RAM fixtures gone, shared lease free.

Text leaf samples: instruction prefix15.87%, ordinary call5.12%, mutation4.62%,
lower4.45%, whitespace scan3.78%, native proof2.77%, native evaluation3.11%.
Report: JSON string6.13%, container state machine5.72%, monitor enter/exit10.0%,
instruction prefix4.74%, formatted append3.18%, native append2.95%.
Inclusive JSON31.50%, native formatted evaluation37.05%; these include useful work
and overlap. They are not predicted removable costs. Unknown4.70%/7.75% and malformed
last map lines are retained. [Counts/callers](inclusive-assessment.json), [offline reader](analyze-inclusive.py).

No general call/frame or tagged-value rewrite follows from these totals. Shared
budget synchronization remains required. Current native-return proofs prevent
fallback after effects/admission; don't remove them on allocation totals alone.

Next bounded proposal: a shared synchronous JSON container loop, retaining CLR
nesting and existing grammar/storage. Enter an await continuation only for an
actually incomplete child/hook; no parent-array workspace, token staging, second
parser or per-object owner. This differs from [B176](../2026-11-03-json-reader-withdrawn/README.md)
and [B137](../2026-11-03-json-container-reader-withdrawn/README.md), whose manual
parent/workspace costs and adverse results remain relevant. Semantic/lifetime
acceptance and a controlled complete-script pair must precede retention.

[Original plan](plan.md), [raw capture](observations.tgz), SHA256
`11503f4a710e85c119b80377c7116ef50207219d2f3e75367d2572a611f3953e`.
Initial archive as lokad could not read root-owned perf.data; reran only archiving
under sudo, without resampling. [Correction](archive-permission-correction.txt).
No source-pair speedup or final full qualification is claimed here.
