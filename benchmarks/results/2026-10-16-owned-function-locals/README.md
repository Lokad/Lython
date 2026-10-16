# Invocation-owned function locals — 2026-10-16 UTC

Eligible nonmirrored executable functions bind into their full local-capacity
buffer. This removes the second local array, full fill and parameter copy;
parameters retain their compiler prefix and only the remaining slots start unbound.
Unpooled buffers admit conservative capacity/metadata before allocation using the
existing governor, then clear/release after binding failure, rejected frame entry
or completed body/default protocols. No reservation object, ownership subsystem,
collector, new pool or stack policy is introduced. Real module/mirrored/lowered
and generator routes remain. Escaping cells, snapshots and values keep their
independent funding.

| Engine | Measured revision | Warm starred-list helper median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | cd32a96404e4bb3214aacc0148f4e0bfd806771f | 26.602 ms | 2.29% |
| Lython after | 527ff833f972a940dc9f4314162ea1a8ec11fc66 | 27.550 ms | 2.36% |
| CPython | 3.13.16 | 2.9739 ms | 2.13% |

No performance benefit is demonstrated: the Lython median is 3.6% higher in
one unqualified diagnostic. There is no confidence interval, qualified CPython
ratio or claim about later HEAD, full file jobs, embedding latency or allocations.
Small parameter buffers were already pooled; this does not establish one fewer
heap allocation per call. The delivered benefits are the consolidated buffer
lifetime and explicit capacity admission. Additional per-call admission and
cleanup have costs; this sample cannot isolate their individual contribution.

The unchanged lists.starred-merge.large script calls a three-parameter helper
16,384 times. Its fourth local precedes a starred list display; the module sums
the first value, marker and result length. Source is264UTF-8bytes, SHA256
3b93768de4502a6dc62ead9da0bb6f55745b7a27f980b4d2934662a161d5310c.
Independent goldens are N*(N+6): prints268533760\n at the measured size.
Both engines pass sizes256/2,048/16,384 before timing. Case/source/size/lane
match frozen declarations; no new producer, input files or repeated capture.

Boundary: warm public RunAsync, reused compiled code, fresh invocation state,
execution/projection and prior-result cleanup timed; compilation, startup,
transport and full response hashing excluded. Host operations complete
synchronously; real protocol suspension is tested separately. Lython cooperative
admission, cancellation and public limits remain; optional fuel is unset.
CPython has no equivalent in-process containment.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu24.04.4LTS x86_64, four AMD EPYC
9V45vCPUs,16GiB RAM, no swap. SDK10.0.401/runtime10.0.12; CPython3.13.16,
GCC13.3 PGO/LTO, ordinary GIL/GC and -I -S. Workers retain normal GC/tiering;
only supervisor tiering is disabled. Seven rounds span8.503s under a30s group
bound. All524production sources per role match frozen Git manifests; comparison
tooling and backend are identical. Seven workers/fixtures are gone, relevant
services inactive and VM lease free. No retry, retuning or full lane.

Validation:23new working-capacity lifetime/admission/error controls; six existing
keyword-default lifetime cases now have larger bodies.2,791ordinary white-box
checks pass17.306s,204public function/default/closure/unbound/admission checks
5.348s,59original stack/trace/copy checks9.245s and93representative file/CSV/text/
JSON/retention/cancellation checks15.261s.16saved fuel/span/body-effect matrices
match exactly. Sequential calls return to baseline without collection, suspended
users retain capacity through cleanup, and values remain funded after clearing.
One initial stack filter selected zero tests in the white assembly and is excluded;
the correct public assembly passes59. No guard, fixture or timeout changes.

Contract tradeoff: capacity admission can now refuse before a binding TypeError
or effectful default lookup that previously happened first. Refusal is terminal
with its first origin preserved. Funded Python behavior is unchanged. Small cleared
pools retain bounded control headroom; no exact heap reachability or tiny-budget
threshold equivalence is promised. See [MEMORY.md](../../../MEMORY.md).

[Samples](warm-async.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[unchanged guard outputs](guards-proof.json) and [local validation](local-proof.json).
