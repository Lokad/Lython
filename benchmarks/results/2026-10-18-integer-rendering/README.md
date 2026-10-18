# Ordinary integer rendering, 2026-10-18 UTC

Large ordinary integer rendering now admits working storage before allocating its
CLR text. Six baseline refusal regressions allocate 41,008–41,184 bytes before
denying the final result. The fixed cases allocate 976–1,704 bytes before denying
working admission. Successful signed-64 rendering also avoids the CLR string and
UTF-8 re-encoding. **Elapsed speedup and CPython timing are unmeasured.**

Baseline: `2c381510bbddbc6f2677c072d9764fadb46fb34c`. Candidate: that revision plus
the [one-file patch](candidate.patch), with canonical identities in
[validation](validation.json). These identities describe this diagnostic, not
an unspecified future HEAD.

| Native rendering / allocated bytes per 128 conversions | Baseline | Candidate |
| --- | ---: | ---: |
| Ordinary/interpolated `42` | 18,432 | 18,432 |
| Ordinary/interpolated `123456` | 23,552 | 18,432 |
| Ordinary/interpolated signed-64 maximum | 28,672 | 20,480 |
| Ordinary/interpolated `-123456` | 23,552 | 18,432 |
| Python rendering `-123456` | 23,552 | 18,432 |
| Repr `-123456` | 31,744 median; one 31,872 sample | 26,624 |
| Ordinary/Python rendering `2**80` | 30,720 | 34,816 |
| Repr `2**80` | 38,912 | 43,008 |
| Floating repr control `12.5` | 30,720 | 30,720 |

All eight samples are preserved in [before](allocation-before.json),
[after](allocation-after.json), [positive counters before](counters-before.json)
and [after](counters-after.json). The positive-counter baseline capture filled a
material gap in the first diagnostic; it did not repeat the original cases.
Larger integer conversions add a 32-byte existing temporary-reservation object
per conversion. `42` and the float control show no allocation improvement.
No statement about a complete job's speed or a CPython ratio follows from these
byte counts.

The [temporary fixture](allocation-diagnostic.cs) directly calls already loaded
native rendering helpers in fresh funded contexts. Context/input creation and
owner cleanup are outside the thread-allocation counter; 128 conversions,
governed result storage and consumed UTF-8 lengths are inside. This excludes
public invocation setup/scheduling, executable dispatch, compilation, startup
and host I/O. Values and cleanup to zero are checked. No GC was requested.
The fixture was removed before final matching Release builds.

This is the active Windows development workstation, not the quiet Azure VM:
Intel Core i7-14700KF, 20 cores / 28 logical processors, Windows build 26200,
x64. [Toolchain](dotnet-info.txt) and validation record SDK
10.0.300-preview.0.26177.108 / host 10.0.13, assembly/source hashes and process
receipts. No extra VM capture, elapsed qualification campaign or full lane was
run for this bounded conversion/admission change.

Three duplicate ordinary integer conversion expressions now share one helper.
Signed-64 values use a fixed 20-byte managed UTF-8 span and the existing admitted
result-copy factory. Larger values keep exact BCL decimal conversion under an
existing temporary reservation held through final UTF-8 admission. The rough
estimate is `256 + 8 * (magnitudeBits / 3 + 2)`, including sign, UTF-16 text,
active conversion workspace and headroom. It intentionally permits earlier
denial; no retry or alternate path follows failed admission.

The reviewed [.NET 10 decimal formatter](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Runtime.Numerics/src/System/Number.BigInteger.cs)
uses conversion workspace and a shared pool. The reservation covers active work;
framework-pool retention remains part of host runtime headroom. This is a rough
cooperative estimate, not proof of exact physical-heap coverage. Lython-owned
retained pools/caches remain charged; no new owner or pool is introduced.
See the [memory contract](../../../MEMORY.md).

The [failed baseline](refusal-baseline.trx), [frozen baseline guards](guard-baseline.trx)
and [candidate regressions](guard-candidate.trx) preserve the evidence. Refusal
leaves input/accounting unchanged and retains the first structured failure.
Final-copy refusal releases working storage, while successful results keep their
owner. Six guard matrices preserve all steps, source spans and argument effects.
Fourteen boundary/large-number cases pass isolated
[CPython 3.13.2 goldens](cpython-oracle.json); these are semantic controls.
Both public execution APIs also cover invariant signs and custom/bool/float
precedence. Focused Release checks cover command-failing admission, retained
results/caches, stack protection and complete CSV/text/JSON/file goldens.
Counts and receipts are in validation. Platform/package checks are batched at
delivery; no assertions, deadlines or original Stress scales were relaxed.
