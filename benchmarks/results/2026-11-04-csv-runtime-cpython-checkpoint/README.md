# CSV/newline runtime CPython checkpoint

2026-11-04. Committed runtime source **5c827e68**, frozen precommit core**21cddae7**
(assembly metadata9562510b); exact source snapshots/hashes are authoritative.
Reference qualifieda00fe76d/frozen1dcca6f2. CPython3.13.16, actual.NET11RC3,
quiet AzureF4as_v7/Ubuntu24.04.4, ordinary GC/tiering. Four independent workers/job:
current, identical current control, qualified reference and CPython.

Fourteen jobs384.53s below480s. Existing tool/protocol2,5sec warmups/seven rotated
150ms samples. Six complete scripts warm/fresh plus CSV/report compilation;
long-field input/source/goldens reused from B150 to fill an external baseline gap.
Every loaded core, output/file golden, process join/fixture cleanup and before/
after quiet gate passes. No failed launch or timing-policy correction. [Exact
identities](identity.json), [all metrics](summary.json), [raw evidence/sources](observations.tgz),
SHA256 `265400b8cdb98e85b5033100b4569260a6092753390c25eb5edbf1eda3b2fb84`.

## Fresh compile plus one complete execution

| Script | Input | Lython | CPython | Lython/CPython | Current control / Lython IQR |
| --- | --- | ---: | ---: | ---: | ---: |
|CSV summary/helpers|4,096records/57,361B|5.0817ms|3.4385ms|1.48×|0.52%/1.48%|
|JSON-lines/helpers|4,096records/110,592B|5.3606ms|4.1394ms|1.30×|2.19%/1.25%|
|Text summary/helpers|3,072lines/35,840B|3.1072ms|1.2969ms|2.40×|2.65%/1.03%|
|JSON-to-Markdown report|3,072records/189,441B|4.5358ms|2.5354ms|1.79×|2.20%/1.06%|
|Config edit/write|1,024paths/7,177B|0.2786ms|0.2554ms|1.09×|0.18%/0.79%|
|CSV long fields|16records/524,717B|0.4172ms|2.5163ms|0.17×|3.84%/5.31%|

Ratio=Lython/CPython elapsed; above one means slower. The long-field case is a
real complete script; its ratio does not describe all CSV work. Long-field source
pair improvement and external ratio are distinct. Ordinary text/report gaps remain.
Long-field5.3%IQR/report control6.2%IQR limit precise small-version claims.

## Warm execution / separate compilation

| Script | Lython warm | CPython warm | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | ---: |
|CSV summary/helpers|4.9939ms|3.3322ms|1.50×|2.79%/0.42%|
|JSON-lines/helpers|5.3333ms|4.3052ms|1.24×|3.02%/0.52%|
|Text summary/helpers|3.0169ms|1.1496ms|2.62×|0.74%/1.03%|
|JSON-to-Markdown report|4.4674ms|2.3978ms|1.86×|0.66%/3.47%|
|Config edit/write|0.1978ms|0.1871ms|1.06×|0.86%/3.00%|
|CSV long fields|0.2546ms|2.4268ms|0.10×|0.95%/5.52%|

Separate compilation (never inferred by subtracting warm/fresh medians):

- scripts.csv-summary-helpers.large: 79.96µs vs 55.01µs, **1.45×**, control0.22%, LythonIQR3.83%.
- scripts.report-render.large: 56.41µs vs 37.67µs, **1.50×**, control3.65%, LythonIQR4.69%.

## Net qualified-reference decision

Current/control relative to a00 in this same collection; do not add independent
source-pair deltas:

| Script | Warm change | Fresh change |
| --- | ---: | ---: |
|CSV summary/helpers|-2.0..+0.8%|-1.1..-0.6%|
|JSON-lines/helpers|+0.9..+3.9%|-1.0..+1.2%|
|Text summary/helpers|+1.0..+1.8%|+0.0..+2.7%|
|JSON-to-Markdown report|+0.6..+1.3%|-1.5..+0.8%|
|Config edit/write|-0.0..+0.8%|-1.2..-1.0%|
|CSV long fields|-87.9..-87.8%|-82.7..-82.0%|

Long-field gains persist in independent processes; common cases are near
unchanged or control-limited. CSVwarm straddles reference (−2.0..+0.8%); original
possible+1..3.5% three-image debt remains retained rather than erased. Small text/
JSON adverse directions and variable report/config remain visible. Compilation
CSV−0.7..0.9%, report−0.4..+3.2%, with3.8–5.6%IQR: uncertain, unchanged frontend.
Retain coherent parser/newline deletion and correctness fixes without claiming
broad CPU/RSS/embedding latency gains or closure of the whole-script objective.

## Boundary and qualification

Both clocks include invocation, actual mediated RAM-file I/O, complete stdout/file
checks and prior-result cleanup. Fresh compiles/disposes per invocation; fixture
reset/staging/startup/hash/final cache teardown excluded. All worker processes and
fixtures joined. Fresh globals, persistent CPython module caches and isolated
Lython execution caches remain explicit. No async-host or startup result.

Long-field regression controls independently compare all retained field contents
and SHA256 against CPython after iterator exhaustion: multiple64KiB/quoted/multiline/
NUL/Unicode payloads, both APIs/actual targets. [Full-content control](retained-long-fields-assessment.json)
complements the benchmark's whole-script summary/output goldens.

Windows/Ubuntu actual11/10 ordinary, originalStress and installed Release consumers
pass on 5c827e68 in 41889236498. [Cached delivery result](delivery-ci-status.json);
timing remains separate evidence.
Publication remains postponed. [Previous exact external checkpoint](../2026-11-04-utf8-runtime-cpython-checkpoint/README.md)
retains its source, all observations and debt decisions.
