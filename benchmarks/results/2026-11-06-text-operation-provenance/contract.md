# R17 explicit text-operation provenance

Baseline62141d4d is fully qualified by terminalCI42243450458/all8. Native helper,
compiler-fact, context/storage/stack/thread and directUTF8 changes remain intact.
Private adapter44 baseline is separately focused84internal/421public per actual
runtime; it does not qualify a released backend or change the normal0.4 pin.

R16 gate: a compact integer object would require one representation across scalar
production, native proofs, operators, hashing, identity and canonical projection;
existing host primitive round trips must survive. Current whole-script samples
do not establish enough dominant removable work to select that extra subsystem.
Keep it deferred with exact acceptance rather than adding a long-only fast path.
Imports already execute executable code (840621405,2026-10-20); stale PLAN entry
is corrected. Compact shared text payloads were actually tried in B149/B205/B206
with mixed/adverse controls and extra ownership/access cost; do not replay them.

Selected source hypothesis: remove PyString's per-value birth-span reference and
the unused IPyGovernedValue interface (no production consumers; one test observes
concrete byte/text owners through it). Existing text operations already carry
an explicit span or synchronous ambient operation provenance. Immutable text
needs its owner/capacity/cache, not a historical error origin per value. Expect
8B less physical string metadata; measure actual managed object allocation, do
not infer from field sizes. Current approximate text allocation is string-heavy,
but no percentage predicts a CPU gain. No shared windows/payload flags/new owner,
cache, diagnostic table, integer type, alternative backend or new policy.

Contract: errors use the requesting operation's explicit/current provenance.
Context-free internal operations may have no source span; the public boundary
still reports its active Python instruction. Do not replace an already-recorded
first refusal. Preflight before allocations, cache rollback, actual capacity,
owner identity and retained aliases stay unchanged. Estimates remain deliberately
conservative; do not reduce them merely because physical shells shrink.

Delete the field/property, constructor storage and birth-span fallbacks; remove
the unused interface/declarations and update the one independent ownership test.
Keep all meaningful concrete owner properties and their current consumers.
Add a focused refusal-origin regression where a string was born at a different
span from the allocating operation. Reuse Unicode/case/split/slice/index/regex,
cache/retention/cancellation/refusal/cleanup checks on actual.NET11/.NET10.
Then one short complete CSV/text/JSON/report source pair against621, reusing
frozen workers, full goldens/admission/joins/quiet controls, and a separate short
allocation diagnostic. Keep adverse outcomes; decide any CPU regression debt.
Full platform/stress/package qualification only at a retained delivery boundary.

B351 five complete jobs: CSV warm/fresh, text/JSON/report warm; identical current
control, fully qualified621 source-equivalent9e05892e reference and CPython.
Five warmup seconds/role/job, seven rotating150ms rounds; existing b94 worker and
protocol2. Normal GC/tiering. Complete stdout/file goldens, actual loaded hashes,
funding/Closed/joins, removed RAM fixtures and quiet endpoints. Includes mediated
RAM I/O, full validation and prior-result cleanup; fresh adds compile/disposal.
Startup/staging/reset/transport/final teardown out. One bounded comparison; no
allocation/RSS/embedding-latency claim from timings. Archive verified under lease.
