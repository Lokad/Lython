# Machine-sized integer transport — R174

2026-11-09. Private producer/consumer prototype after qualified cac4acf8, source
c0c543c8. Both nonincremental-original and source/refined identities are explicit;
all production files matched the R173 freeze before editing. Existing five complete
scripts, sync/async, two identical original roles, unchanged whole-job driver and
auxiliaries. Not a CPython comparison or CPU qualification.

The prototype emits existing CLR Int64 values from fitting exact additions, checks
signed overflow and enters existing BigInteger preflight/promotion. Shared native
eligibility recognizes CLR integer types; no cache, pool, tagged slots, ledger,
budget policy, weakened cancellation or unsafe code. It eliminates eight bytes per
fitting result box, not integer objects or their identity. Conservative storage
charges stay unchanged. Initial classification redundantly traversed integer kinds;
one justified source refinement narrows the changed producer to BigInteger/Int64.
All other ingress widths/bools retain their established numeric funnel.

Both variants pass full stdout/file goldens, isolation, funding/cleanup and owned
fixture gates. Original/refined allocation: text about−6.91%, CSV−1.48..1.49%,
report−0.77..0.79%, Unicode-index control−3.78..3.79%. Config near-zero differences
include one moving allocation control. Warm includes mediated I/O/output validation/
prior-result cleanup; startup/reset/protocol/final caches excluded. Compilation
not separately measured because this is not a promotable representation.

Original sync text+3.19..8.04%, async CSV+8.03..9.09%, async config+27.79..30.86%
are preserved. Refined sync text−0.59..+0.34%, async text−3.78..+0.19% overlap
controls; async CSV+3.60..4.25%, report sync+1.25..7.01%/async+2.61..2.82% remain
adverse. These short shared-process/JIT/GC observations establish uncertainty,
not CPU gain/equivalence or a permanent design veto. No quiet VM campaign.

More decisively, LythonRunOptions documents BigInteger results from guest arithmetic,
while unchanged host integral values retain their own CLR type. Raw Int64 results
violate that public contract. Two new externally observable projection regressions
fail the prototype with actual long/expected BigInteger;136existing numeric/protocol/
ownership cases and42addition admission/rollback cases pass.NET11. Passing Python
outputs alone would miss this boundary. Keep the tests unchanged.

**Decision: remove both prototypes completely; do not change public projection to
make them pass.** Allocation savings are real, but a contract-preserving wider
integer representation is a separate migration, not justified solely by these
mixed CPU observations. No blanket rejection of future scalar redesign. All nine
production files restored exactly with fresh timestamps, forced nonincremental
builds, retained NumericIngress contract checks pass actual.NET11/.NET10. Full
platform/Stress/package checks of unchanged production are not repeated. The two
new tests remain as public contract coverage. Continue ordinary call/operand
transport, which can remove VM work without changing Python or CLR value types.

[Original](original-desktop.json), [refined](refined-desktop.json),
[identities](original-identity.json), [refined identity](refined-identity.json),
[baseline](baseline.json), [checks](qualification.json),
[exact sources/cores/tests/raw/adverse logs](source-checks.tar.xz).
