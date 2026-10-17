# Key identity and builtin string comparison — 2026-10-17 UTC

| Engine | Measured source | Warm RunAsync job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython | One frozen key-comparer file on aa9fc0ea | 10.953 ms | 1.99% |
| CPython | 3.13.16, PGO/LTO, ordinary GIL and cyclic GC | 4.259 ms | 1.05% |

Nominal Lython / CPython: **2.572×, unqualified**. Earlier Lython aa9fc0ea
measured 11.700 ms with 1.87% spread. The candidate median is nominally 6.4%
lower in this short diagnostic, **not a qualified speedup** or embedding-latency
result. Earlier [ordinary-loads measurements](../2026-10-17-json-default-call/README.md)
and the prototype below are separate data, not a trend or pooled controls.

The existing key comparer first accepts identical non-null keys, then compares
two exact builtin strings through their existing typed UTF-8 equality. Strings
are sealed and cannot invoke guest code or recurse structurally. This omits
thread-local suppression and general numeric/structural dispatch for those pairs.
All other pairs retain the existing suppression/equality fallback. Hashing,
ownership, allocation preflights, cancellation and ordinary value equality stay
unchanged. No new mode, state, cache, owner, representation or unsafe code is added.

Independent probes found an existing bug in the immutable **aa9fc0ea Release
package**: storing a float NaN as a key and retrieving it with the identical
object raised KeyError. Python key probing accepts identity even when value
equality is non-reflexive. The fix covers dictionary/set lookup and updates with
float/complex NaNs while `nan == nan` stays false and distinct NaNs stay distinct
keys. The public specification and changelog record the correction. See the
[baseline finding](identity-finding.json), [baseline snippets](baseline-snippets.json)
and baseline probe outputs. Non-finite Decimals remain explicitly unsupported;
the [invalid proposed Decimal success control](unsupported-decimal-control.json)
and its failure are preserved, with existing rejection coverage retained.

Native diagnostic: seven rotating batches of **65,536 comparisons per shape**,
after warmup, with precreated operands. Identity/empty/ASCII/Unicode/long/unequal
strings, numeric/mixed/tuple/null and NaN controls all allocate **zero bytes**
before and after. Outcomes are unchanged except the intended key-identity
correction. **Fixed operands permit substantial JIT hoisting/folding**: elapsed
figures are not per-call costs, qualified speedups or proof of unchanged generic
costs. General distinct pairs now pay identity/type guards. The dedicated whole
job is the relevant affected-workload diagnostic. Raw [before](native-baseline.json)/
[after](native-candidate.json), process/TRX receipts and [final diagnostic source](allocation-diagnostic.cs)
are retained. Local Release measurements use Windows 11/i7-14700KF, SDK
10.0.300-preview.0.26177.108 / runtime 10.0.13 and
`GC.GetAllocatedBytesForCurrentThread`, without forced GC. Temporary probes are
removed from supported tests and final assemblies.

A first string-only prototype was captured before the independent probe exposed
the baseline NaN bug. It is superseded: its [archive](prototype-evidence.tar.gz),
[outcome](prototype-outcome.json), [native data](prototype-native.json) and failed
oracle remain visible. Its source and timing do not describe the corrected
candidate. The additional final-source capture was required by that correctness
change; there was no timing-based tuning or third capture, new trace or full lane.

The final comparison reuses **4,096 records / 110,592 UTF-8 input bytes**, the
unchanged JSON helper script, sorted 25-byte report and stdout `3072\n`. Warm
reused compilation, fresh state, completed-host public `RunAsync`, prior-result
disposal, file staging/reset, capture/projection and full file checks are timed.
Compilation, startup, transport, response hashing, real suspended I/O and embedding
latency are outside. No fixture is retuned.

Dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs, approximately 16 GiB RAM, no swap. Both Release builds
use SDK 10.0.401 / runtime 10.0.12. CPython uses GCC 13.3, PGO/LTO, ordinary
GIL/GC, no JIT/debug/free-threaded mode and `-I -S -X utf8`. Workers remove the
supervisor tiering override and retain ordinary GC/tiering, limits/checks and an
explicit unbounded shared allowance. Fuel is unset; no finite-budget/RSS claim.

The working one-file candidate is on immutable
aa9fc0eadae262c17210a59cb72e418928ca4446; it is not a later HEAD measurement.
[Source identity](frozen-source.json), [patch](candidate.patch),
[preparation](preparation.json), build logs and canonical source in the
[raw archive](raw-evidence.tar.gz) identify it. Clean baseline binaries were frozen
before overlay; the source checkout then has the one candidate modification.
Assembly revision metadata names the base. Responses/identities are in
[micro.json](micro.json).

The sole final-source comparison uses the existing 15-second collection /
30-second owned group cap and shared lease, one-second warmups, two-second
settling and seven rotating 25 ms batches. Total **9.317 s**; **36 responses /
456 invocations** match goldens/digests, twelve inputs and 55 frozen baseline
files remain unchanged, workers/fixtures are gone, the group is empty and the
lease is released. See [validation](validation.json), [ownership](ownership.json),
[launch](launch.json) and [terminal](terminal.json).

Local Release: **484 focused checks** pass, including new owned-key/hash funding,
float/complex non-reflexive keys, Unicode/protocol collisions and reentrant lookup
regressions. Existing equality/protocol/ambient/fuel, ownership/admission and
complete-file goldens pass. All 70 white-box JSON test names/frozen JSON output
records match the preceding runtime. Seven independent supported snippets match
CPython 3.13.2 through each API. See [local validation](local-validation.json),
[supported snippets](binding-snippets.json) and [source proof](source-proof.json).
No timeouts, Stress scales, denial demands or containment assertions are relaxed.
Stress is retained, not rerun. Package/platform checks belong to the delivery.
