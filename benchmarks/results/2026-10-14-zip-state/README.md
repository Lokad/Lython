# Zip construction and row containment

Runtime producer `a00fc0eaf79058e5b3222a4bbb9da89703ba9701` (production tree `7c6c6134e31bef738a74bec560bda78e08b19a3c`) preflights zip's
cursor storage, exposes partial initialization and rows through the existing
native caller chain, and transfers each fresh row array into its tuple.
Constructor LINQ materialization and the second tuple array copy are removed.
Conservative cursor and temporary-row charges can reject tiny budgets earlier.
Strict peeking, source acquisition order and consumed-input behavior are retained.

Before the change, two warmed allocation controls demonstrate charges of128 bytes
against304/2784 allocated bytes for1/32 input sources. Final50 white-box and601
public Release checks pass in1.360/6.838 seconds including cleanup. Ten new cases
cover constructor publication/rollback, measured storage funding, nested pulls,
genuine asynchronous suspension, denied preflight with no consumption, and denied
tuple registration with exact refund, retained aliases and subsequent recovery.
Existing public controls include strict behavior, cancellation and the original
5,000-row CSV dropped/retained cases.

These are focused checks, not full/platform/package qualification. DLLs were
built before the producer commit and retain earlier informational versions; exact
identities and the formatting boundary are in [qualification.json](qualification.json).
The green combined list/enumeration checkpoint at344147a9 is historical and does
not qualify this newer source. No live-value reclamation or forced-GC change is enabled.

One11.853-second diagnostic uses the existing16,384-row
`iterators.zip-enumerate.large` workload, including materialization and rendering.
Warm execution/output excludes compilation and startup. It compares separately
built344147a9 sources to the candidate on the shared Windows machine with.NET10.0.13
and CPython3.13.2. Medians are42.906/60.651ms; IQR/median55.25%/39.32%. CPython's
context median is5.116ms with2.66%. The higher candidate median remains
inconclusive: no improvement, proven regression, qualified CPython ratio or
embedding-latency claim. The async pull cost is unmeasured. This diagnostic will
not be repeated to replace noisy samples. [micro.json](micro.json) retains the
date, exact workers/toolchain, input/expected-output identities and every attempt.

The broader ownership requirement remains open: opaque upstream captures,
other private construction/native state, conditional identity retention and a
derived funded workspace still prevent complete logical reclamation.
