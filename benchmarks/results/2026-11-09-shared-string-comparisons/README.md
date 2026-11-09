# Shared cooperative UTF-8 comparisons (R164)

Source pair: committed f6861aa5 versus this working change, freshly nonincremental
Release builds on Windows/.NET11RC3, 2026-11-09. Exact source/binary hashes are in
[identity](identity.json); this does not refresh the historical CPython comparison.

Consolidate the R163 private byte loop into contextual PyString equality/ordering;
direct string methods, existing structural contexts and native JSON/difflib/set
sorting pass the current caller. Delete the rendering-only singleton comparer.
Context-free native interfaces deliberately keep short whole-span primitives.
No token is cached; existing guest-hook availability is preserved across JSON sort.

Focused Release passes **108 internal/150 public per actual .NET11/10**, including
funded long equality/ordering cancellation, terminal first-refusal identity, reused
bound methods, nested structures, Unicode/boundary values, native sorts, async ambient
restoration and zero-budget cleanup. Independent Python3.13.2 goldens are retained.
A frozen-image mutation rejects the initial always-suppressed JSON prototype while
the unchanged baseline and corrected source pass. The mutation changes only copied
test outputs, never restores production source.

Two bounded same-process isolated-ALC five-script warm diagnostics use the existing
driver, two unchanged baselines, 32 warmups/32-job batches and five rotations. They
include mediated I/O, output validation and prior-result cleanup; exclude compile,
startup, fixture reset/protocol and final cache teardown. Allocation is effectively
flat. Final elapsed versus the two baselines: CSV **+2.41–5.45%** (baseline spread
2.97%); text −0.75–+1.08%; report −8.63–−3.76%; search −11.14–−8.88%; config
−4.68–+1.91%. Desktop elapsed is diagnostic; no CPU improvement/equivalence claim.

Retain the cancellation/kernel consolidation provisionally with explicit possible
CSV regression debt. Resolve/decide that debt at the coherent R165 delivery boundary
before another performance redesign. Original prototype text +4.21–6.25%, search
+1.76–3.09%, config control spread10.38%, incorrect test fixtures and all failures
remain archived. Keeping pure context-free primitives and correcting JSON policy
justify one changed-source confirmation; no demonstrated causal inlining explanation.

Independent probing also exposes pre-existing JSON sorted-item hook-count behavior:
CPython calls nested-key equality twice, Lython once. The R164 test preserves hook
availability without enshrining that count; R165 tracks the separate semantic fix.
Full platform/stress/package qualification of R164 is pending the coherent boundary;
the green f6861aa5 delivery receipt qualifies only the predecessor. Publication postponed.

[Final diagnostic](summary.json), [original diagnostic](prototype-summary.json),
[Python proof](python-proof.json), [mutation proof](mutation-proof.json),
[JSON gap](json-item-comparison-gap.json), [raw/source/cores/checks](observations.tar.xz).
