# R215 — UTF-8 input contract and immutable dev67

Core correction committed as **69644713**, with all 568 production sources
[verified against that commit](source-identity.json). `os.fsdecode` previously copied arbitrary
bytes into guest strings: the original public probe accepts and re-encodes `ff`.
It now uses the existing strict UTF-8 codec. Valid text preserves BOMs/newlines;
malformed filesystem bytes raise `UnicodeDecodeError`. Linux surrogateescape paths
remain outside Lython's surrogate-free string subset. The local Windows CPython
comparison changes from mismatch to match; that is not cross-platform fsdecode parity.
[Original](fsdecode-before.json), [corrected](fsdecode-after.json).
[Strict byte-codec error goldens](strict-decode-goldens.json) independently check
all five malformed/truncated inputs under CPython 3.13.2.

The Lython-owned provider contract now explicitly guarantees valid, stable borrowed
UTF-8. Compile input is borrowed only during the call; retained pattern bytes need
provider-owned funded storage. Subjects remain unchanged and funded until their
subject/cursor users finish. Direct callers uphold the same preconditions. No new
buffer, wrapper, owner, option, backend dependency or input-validation layer.
Core string ingress copies mutable host arrays, strict codecs reject malformed
text, published builders relinquish writable storage, and provider capture bounds
are checked before slicing. Construction/rebinding and host-mutation regressions
exercise these boundaries. This does not remove unrelated internal defenses.

Forced Release solution builds pass with zero warnings/errors. Focused normal
checks pass on each actual .NET 11/10: 164 public / 29 codec-storage checks, then
71 public contract/fsdecode checks after contract coverage was added. Coverage
includes cancellation, first refusal, cleanup, retained results and shared-budget
reuse. Full delivery is **GREEN42918112217**: Windows/Ubuntu, both actual target
frameworks, ordinary suites, original stress and installed Release consumers.
[Cached terminal result](delivery-ci-status.json),
[CI](https://github.com/Lokad/Lython/actions/runs/42918112217). No unchanged full
qualification replay or publication follows.

## Development adapter, independent of core delivery

The supplied immutable Core/PythonRe **0.5.0-dev.67**, repository
**d8eb14a52b2c044f4345af03469356992be58861**, is consumed without sibling edits/builds.
Package SHA256: Core `bbaafb1efad7756a694316810de691195a747adba5a1891db23e82c3ccb64b6a`;
PythonRe `8664817c619f5d6459ad1995dbec1e8c5c0979f89d3f108e3e8185da28b2c502`.
XML member names are unchanged, which is not a complete API inventory.

The unchanged Validate bridge passes **86 internal / 524 public** focused checks
per actual target at 5be44091/e543 source. With the clarified contract and codec
fix, the development-only AssumeValid bridge passes **86 / 528** per target at
69644713. Each isolated qualification preserves all 29 normal source/pin/lock/output
identities at its own boundary. The intervening normal build intentionally changes
normal outputs; the two isolation proofs are not a claim that those builds are identical.
[Validated identity/checks](validated/qualification.json),
[trusted identity/checks](trusted/qualification.json),
[trusted isolation](trusted/normal-state-proof.json).

Normal adapter remains **0.4.0**, non-packable. Three final producer boundaries remain:
distinct single-match working ownership, native UTF-8 group-name enumeration, and
token-independent cursor creation with cancellable preparation. Native lookup and
per-move tokens already exist. Bridges stay outside core. Producer `Unsafe.SizeOf`
usage is not certified by consumer semantics tests; final released-adapter capability,
safety and performance qualification remains separate. Missing producer APIs block
neither core delivery nor independent performance work.

## Bounded same-producer diagnostic

Existing complete-job driver; identical current core, dev67 producer and benchmark
host binaries; only adapter validation mode differs. Three isolated images share
CoreLib/process GC: Validate, AssumeValid, unchanged Validate control. Six scripts,
five rotated rounds, normal GC/tiering. Original 32 warmups / batch 4; one justified
confirmation uses 256 warmups / batch 16. Both sources and every original observation
survive. This Windows desktop is not the quiet reference VM; clocks are diagnostic.

| Diagnostic script | Original elapsed change | Longer-warmup change | Confirmation Validate control spread |
| --- | ---: | ---: | ---: |
| ASCII prefix, 128 searches / 8,192 bytes | −10.91..−0.99% | −35.53..−14.50% | 32.63% |
| Unicode prefix, 128 searches / 8,192 scalars | +44.40..+50.20% | −11.24..−9.63% | 1.82% |
| ASCII prefix miss | −4.66..−4.56% | −5.67..−3.54% | 2.26% |
| Unicode lookbehind, 1,024 scalars | −14.54..−13.94% | −7.09..+0.06% | 7.70% |
| Unicode forward captures, 4,096 scalars | −20.78..−6.67% | −6.09..−5.14% | 1.01% |
| Non-regex text control | −0.54..+2.62% | −3.42..−1.38% | 2.11% |

Change means `(AssumeValid / Validate − 1) × 100`, across both Validate medians;
ranges are not confidence intervals. Allocation is essentially flat. Unicode-prefix
allocation is exactly 4,494,090 bytes/run in both original images: the bridge still
requests DetailedData's coordinate/text projections and discards them after extracting
byte ranges. Removing eager validation does not remove all necessary descriptions.
The large original adverse timing and moving controls remain unresolved; the
confirmation establishes sensitivity to warmup, not its cause or a general CPU gain.
No normal pin promotion or performance claim follows.

Elapsed includes public execution, fixture reset, complete stdout/file goldens and
result disposal; separate execution-only clock is retained. Compilation, startup and
final script/cache disposal are excluded. No guest file I/O in these diagnostics.
All output, file, isolation, stable shared-budget balance and final-zero/fixture
cleanup gates pass. Raw coarse process CPU includes surrounding work and shared JIT/GC.
No forced collection or abandoned users.

[Original data](diagnostic/measurements.json), [original summary](diagnostic/summary.json),
[confirmation](confirmation/measurements.json), [confirmation summary](confirmation/summary.json),
[identical-image controls](comparison-identity.json),
[sources, logs, core/adapter/producer and diagnostic binaries, original conditions](qualification-and-diagnostics.tar.xz),
[archive identity](archive-identity.json). Publication remains postponed.
