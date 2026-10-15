# Direct default string stripping

No-argument `str.strip` calls on exact builtin strings reuse receiver/operation
operands, avoiding bound-method creation and argument/builtin binding arrays across
distinct lines. Both drivers share the existing UTF-8 whitespace trimming and
result-ownership helpers. Optional arguments, exposed methods and dynamic objects
retain general binding. Fallback getters can suspend before invocation.

The shared trimming primitive now returns its input when unchanged. This restores
Python identity for unchanged strip/lstrip/rstrip results and removes the old copy,
including strip with an empty character set. Changed results retain allocation
preflights and ownership; builtin strip's chars parameter is now positional only.
No new trimming algorithm, Unicode database, cache, pool or callable framework.

| Warm `RunAsync` | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Split lines, strip and sum lengths; 32,768 lines / 425,984 UTF-8 bytes | 27.930 ms | 23.464 ms | 3.132 ms |

**No timing benefit is established:** baseline/candidate IQR-to-median is
16.5%/23.3%, larger than the observed difference; CPython spread is 0.9%. The
single [diagnostic](warm-async.json) preserves all seven rotating batches,
calibration, warmup and identities. No retries, retuning or replacement samples.
Keep the structural allocation removal and correctness fixes without an elapsed
speedup, confidence interval or overall multiplier. The job also includes text
construction, splitting, len calls and arithmetic; it does not isolate strip cost.

The owned command took 9.218 seconds including cleanup under a 30-second bound.
Timing includes in-script input construction, fresh invocation state, execution,
capture/projection, golden checks and prior-result cleanup. Compilation, startup,
transport and full response hashing are excluded. Host operations complete
synchronously in this CPU job; actual suspension/cancellation is tested separately.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
ordinary GIL/GC, `-I -S`, UTF-8 mode off and explicit UTF-8 protocol I/O. Independent
local goldens use CPython 3.13.2 with `-I -S -X utf8`. Lython's normal public
policies/shared allowance and GC/tiering remain; CPython has no equivalent
containment. No current complete-file-script multiplier is established.

## Provenance and validation

Baseline is 88e0992b883a19d0e27cd1295f63e4190fe6bf8c, containing the separate
scalar-literal diagnostic fix. Candidate was built before commit on that revision
plus seven performance files identified by [frozen hashes](comparison-declaration.json).
Both roles have the identical new pipeline case and unchanged comparison/execution
tooling. [Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match CPython at all three sizes. [Cleanup/source proof](cleanup-source-proof.json)
verifies all seven workers exited, exact source differences, identical tooling,
inactive services and machine-lease access.

Sixteen [independent Python programs](cpython-goldens.json) cover whitespace,
supplementary characters, raw escape text, identity, optional chars/None, keyword
errors after argument effects, bound/unbound/expanded calls, fresh method values,
dynamic/raw-function/non-callable attributes, comprehensions and generators.
Each passes both public APIs with shared-budget cleanup. Delayed getter I/O and
cancellation preserve lookup order and drain before allowance returns. Retained
trimmed results remain charged across another execution and release to baseline.

Internal controls verify zero-argument receiver transport/absent bound-method
caches, all eight fuel/span boundaries, and occupied 64 KiB admission. Unchanged
owned text succeeds without new storage; changed text refuses before a funded
copy and preserves old contents/accounting. All 2,196 ordinary white-box tests,
focused 86 white-box / 311 public cases and 103 governed CSV/text/JSON/file controls
pass. Groups overlap; each takes less than 30 seconds including cleanup.
[Validation](validation.json) retains receipts and TRX hashes. No full stress rerun.
Package/platform gates are recorded at delivery.

Initial checks exposed unchanged-result copying and a CLR encoder exception for
surrogate literals. Commit 88e0992b rejects unsupported surrogate units/escapes
with LA2000, including f-string text/specifiers and raw UTF-16 input; it also rejects
out-of-range eight-digit escapes without signed wraparound (LA1007). Scalar UTF-8
storage remains the contract; raw backslash text and actual supplementary scalars
remain supported. [SPEC](../../../SPEC.md) states this explicit subset limit.
Original failures and the rejected CPython surrogate-support expectation remain
recorded. A new xUnit analyzer error was fixed; checks from that failed build are
preliminary, and final selected groups were rerun after a clean Release build.
No runtime limits, assertions or timeouts were relaxed.

Larger function-local layouts and other method families remain open. This closes
default-strip dispatch and its bounded correctness prerequisites, not all AP03.
