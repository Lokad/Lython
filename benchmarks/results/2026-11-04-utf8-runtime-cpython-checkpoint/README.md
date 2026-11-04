# UTF8 runtime delivery: representative CPython checkpoint

2026-11-04. Committed production source **a00fe76d**, exact frozen precommit core
**1dcca6f2** (assembly metadata65e0871d). Source snapshots/hashes are authoritative;
postmeasurement commit metadata does not imply rebuilt measured bits. Current
source includes native byte-extent truth/text EOF and shared UTF8 search windows.
Reference **97bc43b2/frozen0d452f95** is fully qualified D5. Core/auxiliary identities
are checked in every fresh process; runtime platform qualification is separate.
[Exact identities](identity.json), [samples/summaries](summary.json), [all raw
evidence and sources](observations.tgz), SHA256
`d04a536da9e07abed74b8870450ac9ed17516b42c8ed42b497bde359d3c38e10`.

CPython3.13.16, actual.NET11RC3, quiet AzureF4as_v7/Ubuntu24.04.4, normal GC/tiering.
Twelve jobs328.59s below420s. Four independent workers/job: current, same-bits
control, qualified reference and CPython;5sec warmups/seven rotated150ms samples.
Both quiet gates, goldens, exact loaded libraries, normal process closes/joins and
fixture cleanup pass. Persistent evidence separate from RAM guest files.

## Fresh compilation plus one complete execution

| Complete script | Input | Lython | CPython | Lython/CPython | Current control / Lython IQR |
| --- | --- | ---: | ---: | ---: | ---: |
|CSV summary/helpers|4,096 records/57,361B|5.5159ms|3.7799ms|1.46×|1.36%/1.93%|
|Text summary/helpers|3,072 lines/35,840B|3.3189ms|1.3528ms|2.45×|1.22%/0.89%|
|JSON-lines/helpers|4,096 records/110,592B|5.2902ms|4.2186ms|1.25×|1.24%/0.64%|
|JSON-to-Markdown report|3,072 records/189,441B|4.6470ms|2.7347ms|1.70×|0.23%/3.07%|
|Config edit/write|1,024 paths/7,177B|0.2809ms|0.2577ms|1.09×|0.62%/1.85%|

Above one means Lython slower. IQR is interquartile range/median. Current fresh
report3.07%IQR and warm report3.65%/control6.14%IQR remain variable. These ratios
are current observations, not gains calculated from separately collected older
ratios. The substantial text/report gap remains; no broad CPU gain is established.

## Warm execution and separate compilation

| Script | Lython warm | CPython warm | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | ---: |
|CSV summary/helpers|5.2434ms|3.5230ms|1.49×|1.53%/0.94%|
|Text summary/helpers|3.1638ms|1.2566ms|2.52×|0.59%/1.35%|
|JSON-lines/helpers|5.2632ms|4.1030ms|1.28×|1.05%/0.41%|
|JSON-to-Markdown report|4.4646ms|2.6310ms|1.70×|0.48%/3.65%|
|Config edit/write|0.1951ms|0.1855ms|1.05×|0.74%/1.29%|

Compilation alone:

- csv-summary-helpers: 80.08µs versus 54.23µs, **1.48×**; current control0.35%, LythonIQR1.81%.
- report-render: 56.11µs versus 37.95µs, **1.48×**; current control1.27%, LythonIQR2.55%.

Do not subtract independent fresh/warm medians to estimate compilation. Startup,
genuinely suspended host I/O, physical RSS/equal heaps and embedding latency are
unmeasured. External process CPU ticks are retained diagnostics, not guest-only
CPU attribution or a replacement for the elapsed boundary.

## Coupled source decision

Current/control relative to qualified D5 in this same collection (not additive
D6/D7 deltas):

| Script | Warm change | Fresh change |
| --- | ---: | ---: |
|CSV summary/helpers|-3.6..-2.2%|-2.3..-1.0%|
|Text summary/helpers|-2.5..-1.9%|-4.4..-3.3%|
|JSON-lines/helpers|+0.1..+1.2%|-5.5..-4.3%|
|JSON-to-Markdown report|-0.1..+0.4%|-2.9..-2.6%|
|Config edit/write|+0.7..+1.5%|-0.7..-0.1%|

CSV/text net observations improve; JSON warm/config warm remain slightly adverse;
report is variable. Compilation CSV+2.6..2.9% vs reference2.74%IQR, report−0.1..1.4%
vs2.56%IQR: magnitude uncertain. Keep these costs explicit. D7's isolated possible
CSV+0.6..3.6% debt and D6 small text adverse observations remain preserved; a net
D6/D7 comparison does not erase them. Retain coherent deletions and affected-script
gains, without claiming this closes the broader performance objective.

## Included work and retained setup failure

Actual mediated RAM-file reads/writes, complete stdout/file checks and prior-result
cleanup are inside both batch clocks. Fresh also compiles/disposes per invocation.
Fixture reset/staging, startup/transport/hash/final cached teardown are outside;
all processes and users joined. Fresh namespaces; CPython module caches persist,
Lython execution caches isolated. Existing comparison protocol2/tools unchanged.

The initial launch rejected a loaded-core mismatch BEFORE any timed job: executable
symlinks selected the old shared application directory, frozen3522b972, despite
correct role-directory files. Retain the empty failed record, original/corrected
Ready identities and correction script. Copy only the executable/deps/runtimeconfig
bytes into each role directory; preserve all exact core/auxiliary bytes. Corrected
current/reference Ready identities pass before collection and each worker checks
again. D6/D7 explicit-ALC identity-qualified collections are independent/unaffected.
[Pre-measurement plan and setup correction](pre-measurement-plan.md).

Windows/Ubuntu11/10 ordinary/original Stress/installed Release consumers pass
separately on a00fe76d in [41874093956](../2026-11-04-utf8-search-window/delivery-ci-status.json),
all eight gates and terminal workflow success cached; timing is separate qualification.
Publication/tagging remains postponed. [Previous external checkpoint](../2026-11-04-executable-cpython-checkpoint/README.md)
keeps its original source, conditions, failed measurements and debt decisions.
