# Native streaming text windows

Baseline **eee92b7a**, candidate that source plus [source.patch](source.patch).
Collected 2026-11-01 Paris / 2026-10-31 UTC. Exact worker/source/input identities
are preserved below. This is a bounded old/new experiment, not arbitrary later
HEAD, a replacement CPython table, or a new benchmark framework. Regex backend
0.4.0 is unchanged and unused by these scripts.

## Hypothesis, design and acceptance

Current InstallWindow still decoded UTF-8 into a CLR string, optionally normalized
that string, then re-encoded it into PyString. Latin-1 similarly produced UTF-8,
decoded it to CLR text and encoded it again. ASCII/Windows-1252/UTF-16 already
returned owned native text. This concrete pipeline survives earlier CSV work;
it does not assume old argument-array or context duplication still exists.

Consolidate streaming output onto existing admitted codec results. Delete the
read-state decoded-string/ownsDecodedWindow/FromString funnel. Extract the existing
admitted Latin-1 result wrapper for both whole-buffer and streaming callers.
Strict valid UTF-8 needing no newline conversion uses the native return path.
Other UTF-8 newline/error paths keep the existing shared decoder and governed
scratch; they can reject tighter allowances earlier. No Unicode parser rewrite.

FromUtf8 can adopt an entire array: a borrowed raw window cannot rely on incidental
carry padding to make adoption impossible. The existing internal decoder receives
an explicit copyInput ownership argument; only streaming windows request the
admitted copy. Existing immutable one-shot input reuse remains unchanged. This
is not a public mode or configuration switch. Raw, replacement and output storage
remain funded until their users finish; no cumulative-allocation substitution.

Pre-recorded acceptance: at least 15% less complete allocation in one affected
file-processing script or 10% in two, with no repeatable material complete-job/
control regression. The complete float JSON report passes at **15.186% less**;
integer is **9.959%**, correctly below the two-job 10% threshold. The fixed VM
comparison does not demonstrate a repeatable material regression; search/
numeric timings remain extremely noisy and inconclusive. Accept the native pipeline/allocation result without a qualified
elapsed or embedding-latency claim. No retiming or branch rescue.

## Focused semantics, admission and lifetime checks

Both targets pass **248 native / 618 public** focused codec/text-file/gzip/read/
BOM/chunked/encoding cases (net11 first runs 246 then the three-case ownership
family, with one overlap). Two new ownership cases decode a full borrowed array,
reuse it and verify the independently funded result stays unchanged, with and
without universal-newline translation. Scope disposal returns allowance to zero.
An additional 63 selected host/path/delayed-composition checks pass per target;
that selection overlaps the public set and is not added as unique coverage.

Existing coverage preserves scalar sizes, split UTF-8/BOM/CRLF, EOF/truncation,
Latin-1 and alternate codecs, errors/newlines, read/readline/iteration, retained
outputs, retry position, actual suspension, cancellation/refusal and cleanup.
No existing fixture, expectation or timeout is changed for this prototype.
The [focused archive](focused-checks.tar.gz) preserves exact logs and diagnostic
stderr. Full delivery/platform/package evidence is recorded separately in PLAN.md.

## Fixed before / candidate / identical-before allocation diagnostic

The [adapted existing driver](driver/Program.cs) measures public warm Run including
file fixture staging/reset, complete stdout/file goldens and result disposal.
Compilation/startup are excluded; common reflection overhead is included. Four
warmups precede seven rounds of five complete jobs. Every job returns allowance
to its compiled baseline; compiled disposal returns it to zero. Each process has
a 30-second external bound and finishes normally. Windows 10.0.26200, 28 logical
processors, .NET 11 RC3, ordinary GC and equally disabled tiering; no overlapping
local builds/tests. No forced collection or exact heap-tracing claim.

| Complete script | Before B | Candidate B | Identical-before B | Reduction |
| --- | ---: | ---: | ---: | ---: |
| file-search.large | 962,761.6 | 899,227.2 | 962,761.6 | 6.599% |
| config-edit.large | 942,275.2 | 927,905.6 | 942,275.2 | 1.525% |
| csv-summary-helpers.large | 9,193,923.2 | 9,085,243.2 | 9,193,923.2 | 1.182% |
| text-summary-helpers.large | 5,583,107.2 | 5,519,547.2 | 5,583,107.2 | 1.138% |
| numeric-report.integer.large | 728,521.6 | 655,968.0 | 728,521.6 | 9.959% |
| numeric-report.float.large | 744,688.0 | 631,600.0 | 744,688.0 | 15.186% |

[Before](before.json), [candidate](after.json) and [identical-before](before-control.json)
retain every sample, balance and assembly identity, plus unqualified local elapsed
values. All six use existing independent file/stdout goldens and unchanged source.
The exact catalog/input/output bytes are in the raw VM archive. Each large numeric
report loads two arrays of 4,096 values, sums each once and writes JSON. No repeated
artificial parsing or aggregate loop inflates this gain. File inputs are 42,010 B
for search, 36,231 B for integer and 56,495 B for float; record fixture bytes define
the remaining inputs. CSV/text provide smaller affected controls.

## Fixed VM comparison

Two observations, twelve predetermined jobs, **96.20 seconds** within the
180-second outer bound. Both quiet gates and all complete goldens pass. Dedicated
Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3. CPython
3.13.16 is an independent control in each job. Existing micro tooling uses
warmup/settling and seven rotating 25-ms batches, ordinary GC and equally disabled
tiering. Warm execution excludes compilation; compile-run adds fresh compilation
inside a warm host. Both include declared fixture reset, host I/O, validation and
cleanup. Compilation-only text is an unaffected frontend control; its verification
phases still execute the full golden. Startup is excluded. No unrelated-median
phase subtraction or additional timing jobs.

Microseconds per complete job; delta is candidate / baseline minus one:

| Lane / script | Observation 1 old / new | Delta | Observation 2 old / new | Delta | Old/new IQR, observations 1; 2 |
| --- | ---: | ---: | ---: | ---: | --- |
| warm / file-search.large | 2,006.7 / 1,929.8 | -3.8% | 2,089.0 / 2,088.5 | -0.0% | 49.1/121.4%; 66.3/124.9% |
| warm / numeric-report.integer.large | 2,239.4 / 2,128.9 | -4.9% | 2,403.3 / 2,378.2 | -1.0% | 10.4/91.0%; 33.1/149.7% |
| warm / numeric-report.float.large | 2,382.1 / 2,290.2 | -3.9% | 2,281.4 / 2,266.8 | -0.6% | 8.6/116.9%; 7.1/114.1% |
| warm / text-summary-helpers.large | 6,332.3 / 6,173.5 | -2.5% | 6,376.9 / 6,176.7 | -3.1% | 5.7/5.2%; 5.3/5.6% |
| compile-run / config-edit.large | 3,333.3 / 3,287.6 | -1.4% | 3,373.2 / 3,375.5 | +0.1% | 50.7/67.5%; 60.4/58.8% |
| compile / text-summary-helpers.large | 118.0 / 115.6 | -2.1% | 118.6 / 118.1 | -0.3% | 5.9/2.8%; 3.9/2.4% |

Search/numeric spreads reach 91–150%; config increases 0.1% in the second
observation and its spreads are 50–68%. Keep all increases/reversals and raw
CPython controls. These diagnostic timings establish no qualified speedup and
do not update docs/BENCHMARKS.md's primary external-baseline table. The unchanged
frontend control must not be described as a compiler improvement.

[collection.json](collection.json) summarizes every job. The [raw archive](raw-vm-results.tar.gz)
contains all receipts/logs, gates, exact catalog and [collector](collect.py).
Workers and outer service complete normally; the VM lease is released. No
production profiler/native probe is introduced. Stop this redesign after its
coherent delivery checks; preserve the exact result instead of retiming branches.
