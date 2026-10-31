# Native UTF-8 error/newline decoding

Baseline **6f964df0**, candidate that source plus [source.patch](source.patch).
Collected 2026-11-01 Paris / 2026-10-31 UTC. Exact workers, catalog, full results
and machine gates are preserved below. This compares frozen development sources,
not arbitrary later HEAD or a new CPython table. Regex is unchanged and unused.

## Hypothesis, deletion and acceptance

After the ordinary streaming-window consolidation, non-strict UTF-8 still decoded
valid scalars into temporary CLR strings and a StringBuilder, normalized a UTF-16
string, then encoded it into native text. Paired complete scripts changing only
the read-open to errors='replace' demonstrate substantial allocation traffic:
file search 0.90 -> 1.79 MB and float JSON report 0.63 -> 2.21 MB. This attributes
allocation, not sampled CPU dominance or the whole CPython gap.

Replace that pipeline with two passes over the existing malformed-unit parser:
count final native bytes, admit their existing owner, then fill one owned array.
Delete DecodeUtf8ToString, NormalizeNewlineString, AppendBackslashEscapedBytes,
per-rune strings and whole UTF-16 scratch/encoding overlap. Strict valid native
reuse and explicit independent copies of mutable streaming windows remain.
No new codec framework, public mode, pool, cache, budget, quota or tracing layer.

Newline translation follows error handling. Ignoring malformed bytes can join a
CR/LF pair; replacement/escapes separate it. Both sizing and output retain this
state, malformed-unit grouping and current cancellation checks. No output array
is allocated before admission. Retained output transfers its reservation to the
existing owner; raw/replacement lifetimes remain funded until their users finish.
Smaller or earlier admission thresholds are not a compatibility guarantee.

The pre-code gate requires >=20% lower complete allocation in two robust scripts,
without a demonstrated repeatable material regression in strict controls. Search
and float report pass at **49.77% / 71.47% less**. The fixed timing collection is
inconclusive and does not demonstrate the absence of regression; it cannot prove
an elapsed/host-latency gain. Accept the native pipeline and allocation result,
keep every increase and do not retime or add favorable branches.

## Semantic, admission, cancellation and ownership checks

Both actual targets pass **248 native / 618 public** selected codec/text-file/
stream/gzip/BOM/chunked/encoding/Latin-1 checks. Net11 initially runs 617 public
then the two-case scenario family, with one overlap; net10 includes the new case
in its 618. [Exact focused logs](focused-checks.tar.gz) preserve selections/results.

A new frozen CPython 3.13 test exercises malformed UTF-8 grouping, Unicode,
ignore/replace/backslashreplace, byte decoding, whole-file helpers and preserved
stream newlines. Both APIs preserve the golden; the async host genuinely
suspends. Result funding survives until disposal and the shared budget returns
to zero. Existing refusal-before-large-allocation, retained codec payload, raw
window independence, split scalars/BOM/CRLF, retry position and cleanup assertions
are preserved. Both native passes check the active cancellation token while
scanning and before publication; this remains cooperative, not a hard deadline. No fixture scale or timeout is relaxed.
The first diagnostic-only malformed golden capture used Windows physical stdout
CRLF; it was normalized to the existing logical stdout convention before catalog
and baseline freezing. No runtime expectations were changed to accept a mismatch.

## Fixed complete-job allocation comparison

The [existing adapted driver](driver/Program.cs) measures warm public synchronous
Run, including declared fixture reset/staging, host file I/O, full stdout/file
goldens and result disposal. Compilation/startup are excluded; common reflection
cost is included. Four warmups, seven rounds of five complete jobs; each process
has a 30-second external deadline and finishes normally. Windows 26200, 28 logical
processors, .NET 11 RC3, ordinary GC, equally disabled tiering. No overlapping local
build/test workload, forced GC or heap-tracing claim. Each execution returns to
its compiled baseline; compiled disposal returns the shared allowance to zero.

Three strict/replace pairs retain the existing complete search, CSV-helper and
float-report source and valid file bytes, changing only the read-open error mode.
Two malformed search/CSV variants retain the workload structure and independently
regenerate whole-file goldens with isolated local CPython 3.13. Inputs, exact source
and output bytes are in the [raw archive](raw-vm-results.tar.gz).

| Complete script | Before B | Candidate B | Identical-before B | Delta |
| --- | ---: | ---: | ---: | ---: |
| file-search.large | 899,227.2 | 899,227.2 | 899,227.2 | +0.00% |
| file-search.replace.large | 1,790,480.0 | 899,300.8 | 1,790,480.0 | -49.77% |
| csv-summary-helpers.large | 9,085,243.2 | 9,085,243.2 | 9,085,243.2 | +0.00% |
| csv-summary-helpers.replace.large | 10,595,704.0 | 9,085,283.2 | 10,595,704.0 | -14.26% |
| numeric-report.float.large | 631,600.0 | 631,611.2 | 631,600.0 | +0.00% |
| numeric-report.float.replace.large | 2,214,200.0 | 631,640.0 | 2,214,200.0 | -71.47% |
| file-search.replace-malformed.large | 1,820,443.2 | 910,187.2 | 1,820,443.2 | -50.00% |
| csv-summary-helpers.replace-malformed.large | 10,632,640.0 | 9,105,843.2 | 10,632,640.0 | -14.36% |

[Before](before.json), [candidate](after.json), [identical-before](before-control.json)
retain every sample, assembly identity, steady balance and unqualified local time.
The 11.2 B strict-float candidate variation is background-counter noise. Other
strict allocation controls are identical. Robust search includes Unicode text;
this is not a synthetic character loop or repeated parse inflating the reduction.

## One fixed VM collection

Twelve predetermined jobs, two observations, **94.74 seconds**, within a
180-second outer bound; all full goldens and both quiet gates pass. Dedicated
Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3.
CPython 3.13.16 remains an independent sequential control in each job. Existing
micro tooling uses warmup/settling, seven rotating 25-ms batches, ordinary GC
and equally disabled tiering. No additional timing jobs or favorable selection.

Warm excludes compilation; fresh compile/run includes it in a warm host. Both
include the declared I/O, reset/checks and cleanup. Compilation-only strict float
is an unaffected frontend control, whose verification still executes the full
golden. Startup is excluded. No unrelated-median subtraction or compiler claim.
The collector froze these exact jobs before launch; a Windows default-encoding
error delayed mirroring that final list into local PLAN.md until after launch.
It changed no source, inputs, jobs, acceptance threshold or collection schedule.

Microseconds per complete job; delta = candidate / baseline minus one.

| Lane / script | Observation 1 old / new | Delta | Observation 2 old / new | Delta | Old/new IQR, observations 1; 2 |
| --- | ---: | ---: | ---: | ---: | --- |
| warm / file-search.replace.large | 2,190.6 / 1,997.1 | -8.8% | 2,101.0 / 2,581.9 | +22.9% | 54.3/125.5%; 48.9/94.8% |
| warm / numeric-report.float.replace.large | 2,625.0 / 2,859.0 | +8.9% | 2,609.9 / 2,677.7 | +2.6% | 9.1/110.1%; 18.0/91.1% |
| warm / csv-summary-helpers.replace.large | 7,645.7 / 7,565.5 | -1.0% | 7,443.4 / 7,607.8 | +2.2% | 2.0/5.6%; 2.8/4.6% |
| warm / numeric-report.float.large | 2,411.9 / 2,376.9 | -1.5% | 2,183.0 / 2,235.2 | +2.4% | 11.9/111.9%; 19.8/123.1% |
| compile-run / file-search.replace.large | 3,505.6 / 2,285.6 | -34.8% | 2,431.3 / 4,707.6 | +93.6% | 65.9/148.4%; 108.9/80.1% |
| compile / numeric-report.float.large | 106.8 / 101.6 | -4.9% | 105.9 / 109.6 | +3.5% | 6.6/7.0%; 5.6/10.0% |

Numeric/search candidate IQRs reach 91–148%. Fresh search reverses from -34.8% to
+93.6%; robust numeric medians increase 8.9% / 2.6%, with 110% / 91% candidate
IQRs. CSV reverses -1.0% / +2.2%. These remain visible; no elapsed improvement,
absence-of-regression or current CPython multiplier is qualified. Inspect the
saved measurement boundary/variance before another timing-led redesign rather
than repeating this comparison. The primary CPython table remains historical.

[Collection summary](collection.json), [raw receipts/catalog/machine gates](raw-vm-results.tar.gz),
[fixed collector](collect.py), [driver project](driver/native-utf8-errors.csproj)
and exact source patch preserve the finite experiment. The VM lease is released;
no abandoned worker or background operation remains.
