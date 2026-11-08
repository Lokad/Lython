# JSON record capacity — initial construction prototype

Superseded by the [retained growth refinement](../2026-11-08-json-record-capacity/README.md).

Historical initial core `eb144463`, baseline fully qualified `7265538c` / core
`1aa68ce9`. This prototype reserves a prior immutable layout's entire value capacity
at nonempty object construction. Source, binaries, all adverse observations and
fixture/tool corrections are preserved. One refinement moves the decision to actual
ordered-prefix growth; the initial version is not separately fully qualified.

An eight-record complete read/parse/render/write diagnostic proves 21 replacement
admissions become zero, with seven initial record admissions growing from512 to2528B.
Full stdout/file goldens and allowance baseline pass; stack-capture diagnostics
are never timed. 897 public / 193 internal focused Release checks pass per actual
.NET11/.NET10, including nine new public shape/real-async/retained/shared-budget/
cancellation/refusal/awaited-cleanup cases. No runtime semantic test failed.

| Complete script | Controlled elapsed, four median pairings | Coarse CPU |
| --- | ---: | ---: |
| Repeated 24-field report, warm sync | −10.70 to −15.53% | −10.53 to −15.50% |
| Repeated 24-field report, warm async API | −6.35 to −7.91% | −5.26% |
| Alternating 24-/3-field report, warm sync | **+4.31 to +10.82%** | **+7.69 to +15.38%** |
| Original short report, warm control | −1.78 to +3.85% | −4.35 to +2.27% |
| CSV summary, warm control | −0.75 to +1.29% | −4.07 to +2.27% |
| JSON summary, warm control | −2.37 to +0.99% | −6.25 to 0% |

Allocation: repeated wide records −11.04–11.05% across both APIs; alternating
records **+14.72–14.73%**, short report/CSV neutral, JSON control −2.68%.
Desktop elapsed is variable (old controls up to19.51% spread), not CPU confirmation.
Controlled baseline spread4.67% on wide sync /4.14% alternating limits precision;
all pairings and coarse CPU support the affected gain and adverse tradeoff, not
a universal improvement or causal explanation. Ordinary short-report control is
inconclusive. This evidence justifies a bounded source refinement, not silently
accepting spare short-row capacity or retiming unchanged code favorably.

Two affected scripts keep the existing report source and3072 original rows,
adding21 unused numeric fields to every row or alternating rows. Complete original
formatting/output remains. Three unchanged report/CSV/JSON controls. Independent
CPython3.13 full stdout/file goldens; no CPython timing claim for the new fixtures.
Actual fixture bytes and all hashes are in the archived catalog.

Same frozen worker7cf37ca5/optional0.4, Ubuntu24.04.4/AzureF4as_v7/four EPYC9V45
vCPUs/16GiB/no swap/.NET11 RC3, normalGC/tiering. Five-second warmup per role/job,
two independent processes/core, seven rotated150msrounds. Six jobs170.878s.
Warm command-v2 includes execution, mediated RAM I/O, full validation and prior
result cleanup. Compilation/startup/staging/final cached teardown excluded.
Local allocation includes host reset/full validation/cleanup; local clocks are Run
only. No startup/heap/RSS/pending-I/O/embedding-tail claim.

All source/core/aux/Ready/golden/quiet/Closed/exit/join/fixture/archive/local gates
pass; B468 is terminal, users joined and owned RAM unmounted before archive.
Initial fixture generator used the wrong file digest and raw Windows stdout;
corrected to the existing length-prefixed protocol/LF before collection, preserving
original catalogs/source. An unused theory-label analyzer stopped the first test
build; its message now identifies failures and original log remains. No relaxed
timeouts, deleted regression, new policy/cache/owner or framework.

[Controlled results](controlled-comparison.json), [allocation/local](allocation-and-local.json),
[identity](identity.json), [status](status.json), [sources, binaries, tests, raw and failures](observations.tar.xz).
