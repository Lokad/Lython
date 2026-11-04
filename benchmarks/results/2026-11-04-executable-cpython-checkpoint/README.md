# Executable-storage CPython checkpoint B208

2026-11-04, runtime source **0ffc9ac6**, frozen core **0ee77cd0**. This precedes
the separate copy-context correction c6ac2ae5; it is not a measurement of later HEAD.
CPython **3.13.16**, actual.NET11RC3, same dedicated AzureF4as_v7/Ubuntu24.04.4
as B202. [Full identities](identity.json), [samples and summaries](summary.json),
[raw archive/source snapshots](observations.tgz).

Twelve checked jobs complete328.72s. Four independent processes per job: current
Lython, identical-source Lython control, qualified bf0d38f3/frozen454 reference,
and CPython. Normal GC/tiering,5s warmups and seven rotated150ms samples. Quiet
gates before/after, all goldens/hashes, normal closes, process joins and fixture
cleanup pass. This is a diagnostic development comparison; runtime platform
qualification is independent. No startup, async-host or embedding-latency result.

## Fresh compilation plus one complete execution

| Script | Input | Lython | CPython | Lython/CPython | Same-bits control / candidate IQR | Status |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records/57,361B|5.146ms|3.464ms|1.49x|3.75%/0.21%|Control-limited|
| Text summary/helpers |3,072 lines/35,840B|3.158ms|1.283ms|2.46x|1.13%/1.12%|Variable CPython3.40%IQR|
| JSON-lines/helpers |4,096 records/110,592B|5.430ms|4.193ms|1.30x|0.74%/0.88%|Diagnostic|
| JSON-to-Markdown report |3,072 records/189,441B|4.647ms|2.513ms|1.85x|2.15%/2.77%|Variable control4.58%IQR|
| Config edit/write |1,024 paths/7,177B|0.286ms|0.253ms|1.13x|1.28%/2.13%|Diagnostic|

Ratio is Lython elapsed/CPython elapsed; above one is slower. IQR is interquartile
range/median. Controls and variability limit small improvement claims.

## Warm execution and compilation

| Script | Lython warm | CPython warm | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV |4.976ms|3.303ms|1.51x|1.11%/0.73%|
| Text |3.041ms|1.204ms|2.53x|0.97%/0.54%|
| JSON-lines |5.216ms|4.097ms|1.27x|1.81%/1.05%|
| Report |4.535ms|2.360ms|1.92x|0.69%/5.09%; variable|
| Config |0.194ms|0.185ms|1.05x|0.25%/0.27%|

Separate compilation: CSV90.20us versus54.79us (**1.65x**), report61.67us
versus37.89us (**1.63x**). Current controls differ1.38/1.17%; candidate IQRs
1.99/2.26%. Do not infer compilation cost by subtracting independent warm/fresh
medians. Startup remains unmeasured.

## Coupled source-pair decision

Against qualified454 in this same collection, current source/control ranges:

| Work | Warm change | Fresh change |
| --- | ---: | ---: |
| CSV |+0.6..1.8%|-0.6..+3.2%|
| Text |-3.4..-2.5%|+0.2..1.3%|
| JSON-lines |-3.1..-1.3%|-1.3..-0.5%|
| Report |-3.4..-2.8%|-2.9..-0.8%|
| Config |-1.9..-1.7%|-0.5..+0.8%|

Warm text reference IQR4.46% and current report IQR5.09% limit magnitude. CSV
fresh controls diverge3.75%; report fresh control IQR4.58%. Retain this coherent
representation for removed duplicate transport/graph/frame storage and modest
observations; no broad CPU gain is established. The substantial CPython gap remains.

Compilation still has possible **CSV+1.6..3.0%, report+0.5..1.7%** elapsed debt
versus454, with2–3% IQRs. Keep it explicit. Initial text compilation was+2.4..5.5%
before block consolidation; text compilation alone was not retimed here, so that
uncertainty is not silently closed. The initial async-report prototype regression
and its readonly-borrow correction remain in [B207](../2026-11-04-executable-storage/README.md).
Neither source-pair percentages nor earlier accepted debt add to these external ratios.

## Boundary and controls

Same B202 sources, inputs and checks. Included: invocation, actual mediated RAM-file
I/O, full stdout/file verification and prior-result disposal. Fresh includes
compilation/disposal per invocation; compile-only includes compilation/disposal
without execution/I/O. Excluded: startup, fixture staging/reset, response hashing
and final cached-program/process teardown. All users finish before owners close.
Process-CPU counters also cover work outside the guest clock; no guest CPU gain is
inferred from them. Physical IO, equal heap/RSS and genuinely suspended hosting
require separate evidence. CPython uses normal GIL/GC, no JIT/debug, isolated
`-I -S -X utf8`, GCC13.3 PGO/LTO. No runtime overrides or forced collection.

## Correctness delivery

Initial full31339f0b delivery41831306188 fails13 old buffer fixtures on both OS,
plus one Ubuntu copy-context failure. c6ac2ae5 preserves semantic/funding assertions
while correcting fixtures and native copy provenance; five expired-context cases
reproduce then pass, new hash/IO goldens match CPython. Corrected full delivery
41838965356 is pending. The performance samples above verify these five scripts'
outputs; they do not claim to qualify every runtime path. Publication is postponed.
