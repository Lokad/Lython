# Rejected/inconclusive complete-script JSON float-entry prototype

Baseline **0aa81ee2**, candidate that source plus [source.patch](source.patch).
Collected 2026-11-01 Paris / 2026-10-31 UTC. **The runtime prototype is removed.**
Its standalone entry-point gain is not promoted into a production speed claim;
previous improvements, cancellation/admission and independent correctness coverage
remain. No CPython table update, alternate parser, quota or fallback is introduced.

## Concrete hypothesis and semantic gate

The [B20 component assessment](../2026-11-01-float-entry-assessment/README.md)
passes a fixed >=20% entry-point criterion against the current invariant UTF-8
parser. The saved B13 capture locates a substantial numeric parsing boundary,
without proving a removable CPU fraction. The nine-line prototype uses public
UTF-8 Utf8Parser only for callback-free JSON floating tokens already validated
by the existing Python prefix lexer. It requires complete consumption; failure
is an internal invariant defect, never a fallback to double.Parse. Integer,
parse_float callback/retained raw lexemes, active-token grammar scans, major
storage preflights and existing owner lifetimes are unchanged. BCL conversion
remains atomic, without a hard cancellation deadline.

[Semantic-only driver/corpus](semantic/Program.cs) extends the earlier fixed
corpus with huge signed/zero-prefixed exponents and long mantissas: **2,191**
CPython binary64/consumption goldens match both BCL entries on actual .NET 11
RC3 and .NET 10.0.8. No repeat of component timing. Exact [corpus](semantic/tokens.json),
[project](semantic/semantic.csproj), [net11 result](semantic/semantic-net11.0.json)
and [net10 result](semantic/semantic-net10.0.json) preserve this finite check.

The prototype passes 167 native / 713 unique public selected JSON cases on
net11. The initial public selection is 712 pass plus one new fixture setup
failure: string[] is not a supported global collection. Correcting the new
fixture to List<object?> makes its 24-case number family pass, with 23 overlaps;
no normalization contract, runtime expectation or timeout is changed. Existing
callbacks, real suspension/cancellation, terminal refusal and retained owners stay
covered. This is not a net10 prototype qualification; the native API corpus and
retained public regression separately cover that target.

The independent new public rounding test remains after removal. Fifteen frozen
CPython cases cover top-level/nested/raw-prefix decoding, halfway rounding,
subnormals, signed zero, overflow/underflow, huge exponents and long tokens, through
both APIs. Returned values remain funded until result disposal, then allowance
returns to the compiled baseline and finally zero. Its existing 24-case family
passes on **both** restored-runtime targets, within a 26-case selection including
two unrelated worker-lifetime checks. [Exact logs](focused-checks.tar.gz).

## Pre-code complete-job acceptance

One fixed before / candidate / identical-before diagnostic: complete existing
float JSON report affected, integer report and CSV-helper controls. Four warmups,
seven samples of **128 complete jobs** per case; each case is an independent
owned process with a **30-second** hard bound. The nine processes have a combined
**180-second process-runtime budget**, excluding intervening development/build/test
work; actual combined process runtime is **107.27 seconds**. No overlapping local
build/test, forced collection, hidden allowance, repeated observation or VM retiming.

The [adapted existing driver](jobs/Program.cs) measures public warm synchronous
Run, real efficient contained file-host I/O, fixture reset/staging, full stdout/file
goldens, result disposal and shared-budget baselines. Compilation/startup are
excluded; common reflection overhead is included. Process CPU includes all
managed runtime/worker threads. Wall/allocation are recorded separately. Every
CPU sample must exceed 100 ms to resolve Windows process-clock granularity.
Windows 26200 / 28 logical processors, .NET 11 RC3, ordinary GC, equally disabled
tiering for these local diagnostic workers. No quiet-machine or host-latency claim.

Fixed gate: >=10% lower complete float-report median CPU against **both** before
observations, before/control medians within 5%, every CPU sample >100 ms, no
repeatable >5% control CPU increase against both baselines or material allocation/
ownership regression. A miss or inconclusive control removes the runtime patch;
no threshold adjustment, favorable rerun, new branch or promotion from micro results.

## All complete-job results and rejection

CPU milliseconds per complete job, before / candidate / identical-before:

| Script | CPU medians | Candidate vs before / control | Identical baseline drift |
| --- | ---: | ---: | ---: |
| numeric-report.float.large | 3.6621 / 2.3193 / 2.3193 | -36.67% / +0.00% | -36.67% |
| numeric-report.integer.large | 2.8076 / 1.9531 / 2.1973 | -30.43% / -11.11% | -21.74% |
| csv-summary-helpers.large | 11.8408 / 11.8408 / 11.8408 | +0.00% / +0.00% | +0.00% |

The unchanged float baseline improves **36.67%**, exceeding the 5% stability
bound. Candidate CPU equals the final control: **0% improvement**. Integer's
unchanged baseline improves 21.74%; CSV remains equal. All CPU samples resolve
the required granularity, all complete stdout/files match and execution/compiled
owners return their allowance to baseline/zero. Exact allocation medians are
unchanged except a sub-byte CSV background variation. All wall times remain raw
and unqualified; they are not used to rescue the failed CPU gate.

Reject as inconclusive/no demonstrated material whole-job gain and restore the
original runtime exactly. Stop this candidate without another timing attempt.
The public Python regression remains independently useful. No production helper,
option, cache, dormant alternate path or new accounting mechanism remains.

[Acceptance calculation](acceptance.json), [nine-process chronology](diagnostic-state.json),
[all observations/catalog/goldens/identities](raw-diagnostics.tar.gz), [fixed runner](run-jobs.py)
and [driver project](jobs/complete-jobs.csproj) preserve the finite experiment.
No newer regex producer capability or release was needed for this work.
