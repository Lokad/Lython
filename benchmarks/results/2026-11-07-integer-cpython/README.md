# Representative CPython checkpoint after R45 — B400

2026-11-07, committed8c9fd0d7 production. Frozen d3a6c778 has the exact checked
560-source inventory with precommit SourceLink3480aa55. Core/auxiliary/source
identities are in identity.json; worker7cf37ca5/adapter27211dbe/normal PythonRe0.4
are reused. No development producer or new benchmark framework. Full combined
R44/R45 CI42371564043 was pending at setup and at this evidence collection; the
later terminal receipt, when present, establishes its actual delivery status.

Five unchanged maintained scripts plus two explicit affected inputs. Padded CSV
changes only its fixture (191505B); the637-character source is identical. Numeric
JSON strings change the helper to call int, and use278528B input. These are
separate workloads, not a replacement baseline or a causal CPU attribution.
Both engines run identical source/data and validate complete stdout/file digests.

Quiet AzureStandard_F4as_v7/Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,16GiB/no swap.
.NET11RC3 normal GC/tiering; CPython3.13.16 GCC13.3 PGO/LTO, normal GIL/GC,
no JIT/debug, -I -S -X utf8. One CPython and two identical Lython processes/job,
5s warmup/role/job,7rotated150ms rounds.21jobs completed 434.37s;540s owner,
30s stop/30s margin, observed after600s. All Ready/image/source/aux/golden/funding/
quiet/Closed/exit/join/fixture gates pass. Raw observations archived/verified under
the collection lease, then independently hash-verified locally; no workers remain.

Warm includes mediated RAM-file I/O, complete guest work, full validation and
prior-result cleanup. Compile-to-result adds compilation/program disposal; compile
alone excludes execution/I/O. Startup, staging/reset, transport and final cached
program/result/process teardown are excluded. CPython retains imported modules;
Lython isolates execution imports. No subtraction of independent medians, startup,
RSS, pending-I/O, embedding-tail or per-change saved-CPU claim. Ratio above1 means
slower. Ranges are two role medians, not confidence intervals. Control is absolute
role difference/candidate; IQR is the larger Lython-role value. Variable results
remain visible; labels are measurement descriptions, not design veto thresholds.

## Compilation plus one execution

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 4.065–4.190 ms | 3.368 ms | 1.21–1.24× | Variable; 2.99% / 0.69% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.105–2.129 ms | 1.263 ms | 1.67–1.69× | Diagnostic; 1.13% / 0.58% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.305–4.329 ms | 4.186 ms | 1.03–1.03× | Diagnostic; 0.55% / 0.91% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.608–3.665 ms | 2.509 ms | 1.44–1.46× | Variable; 1.56% / 3.30% |
| Configuration read/edit/write | 16 paths / 181 B | 147.184–147.313 µs | 125.544 µs | 1.17–1.17× | Diagnostic; 0.09% / 2.76% |
| CSV with padded numeric fields | 4,096 records / 191,505 B | 4.569–4.586 ms | 4.325 ms | 1.06–1.06× | Diagnostic; 0.37% / 1.15% |
| JSON with numeric strings | 4,096 records / 278,528 B | 7.767–7.807 ms | 4.639 ms | 1.67–1.68× | Diagnostic; 0.52% / 1.62% |

## Warm execution

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.907–3.936 ms | 3.211 ms | 1.22–1.23× | Diagnostic; 0.72% / 1.29% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.002–2.050 ms | 1.158 ms | 1.73–1.77× | Variable; 2.33% / 0.85% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.191–4.214 ms | 4.075 ms | 1.03–1.03× | Diagnostic; 0.54% / 1.17% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.499–3.509 ms | 2.337 ms | 1.50–1.50× | Variable; 0.28% / 4.13% |
| Configuration read/edit/write | 16 paths / 181 B | 75.156–77.331 µs | 59.996 µs | 1.25–1.29× | Variable; 2.89% / 1.21% |
| CSV with padded numeric fields | 4,096 records / 191,505 B | 4.407–4.470 ms | 4.160 ms | 1.06–1.07× | Diagnostic; 1.41% / 1.02% |
| JSON with numeric strings | 4,096 records / 278,528 B | 7.601–7.671 ms | 4.507 ms | 1.69–1.70× | Diagnostic; 0.92% / 1.82% |

## Compilation/program disposal only

| Workload | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 68.093–69.668 µs | 59.441 µs | 1.15–1.17× | Variable; 2.26% / 1.41% |
| Text summary/helpers | 49.968–50.839 µs | 41.556 µs | 1.20–1.22× | Diagnostic; 1.71% / 0.82% |
| JSON-lines/helpers | 60.878–61.180 µs | 50.915 µs | 1.20–1.20× | Variable; 0.49% / 6.51% |
| JSON-to-Markdown report | 47.135–47.427 µs | 37.822 µs | 1.25–1.25× | Diagnostic; 0.62% / 1.00% |
| Configuration read/edit/write | 42.327–42.742 µs | 34.603 µs | 1.22–1.24× | Diagnostic; 0.98% / 0.85% |
| CSV with padded numeric fields | 68.578–69.275 µs | 59.421 µs | 1.15–1.17× | Diagnostic; 1.01% / 1.81% |
| JSON with numeric strings | 61.658–61.716 µs | 52.021 µs | 1.19–1.19× | Diagnostic; 0.09% / 1.08% |

[Raw observations](observations.tgz), [summary](summary.json), [source/images](identity.json),
[receipt](archive-receipt.json), [terminal state](terminal-status.json) and
[predeclared conditions/commands](development.tar.xz). The immutable raw archive
also preserves catalog, source patch, machine gates, setup plan and complete
warmups/samples. Historical B395 and all adverse source-pair results remain.
Publication is postponed; this checkpoint does not complete the performance goal.
