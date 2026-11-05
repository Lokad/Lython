# Immutable development producer qualification

2026-11-05, Lython sourcecf3dc9cd. Supplied immutable Core/PythonRe
**0.5.0-dev.29**, package repository revision **43435906**. This qualifies the
optional Lython adapter against changed producer capture/execution internals,
independently of the normal-pin core delivery. Normal adapter pin **0.4.0**,
main outputs and nonpackable status remain unchanged. No packages are published.

Reuse the existing isolated feed/output/obj/lock workflow and exact selections:
**84 white-box /421 public checks per actual.NET11/.NET10 pass on Windows**.
The421 includes six lifetime cases and three real compile/match/cursor in-flight
cancellation subprocess controls per target, not additional cases. Existing
checks cover frontend/runtime/import provider semantics, flags/captures/offsets,
replacement/split, first refusal, retained cache/program users, cancellation and
awaited cleanup. Build34.96s/20.36s, zero warnings/errors; no assertions relaxed.

[Package provenance/DLL hashes](identity.json), [exact qualification](qualification.json),
[test/build logs](checks/), [pre-qualification scope](pre-qualification-plan.md).
Private test and worker-fixture DLL hashes match each immutable package target.
Core dependency inspection has no Utf8Regex edge; the main core remains unchanged.
The reflected main PythonRe class has [no declared public API changes](api-comparison.json)
versus dev26. [Complete reflected surface](api-dev29-net11.json).

An initial API inspection targeted the class-library output, which does not copy
package dependencies. It was corrected to the actual consuming public-test output;
no tests/source/packages were repeated or changed. [Layout correction](surface-layout-correction.md).

Development-artifact evidence only: no released-adapter promotion, cross-platform
producer qualification, new backend performance or internal safety-policy claim.
Native UTF-8 capture-name enumeration, one-shot byte ranges with working-funding
receipts and reversed match windows remain precise final-adapter gaps. Existing
adapter-local bridges and core contract development remain independent of them.
