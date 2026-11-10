# R190-source CPython checkpoint

2026-11-10. Committed source e5731404, measured precommit core e364e4d9 from the
existing nonincremental private freeze. All567 production source files match main
after explicit EOL normalization; no postcommit binary identity or arbitrary later
HEAD measurement is implied. [Source proof](source-equivalence.json) and exact
[image identity](identity.json) preserve this distinction. Local ordinary and
installed Release consumers pass; CI42783134761 original Stress was pending at
the collection snapshot. Final delivery is separately [cached GREEN](../2026-11-10-dictionary-accumulation/qualification/delivery-ci.json)
on Windows/Ubuntu, actual11/10, original Stress and installed consumers; the
collection snapshot remains unchanged.

F4as_v7 Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,16GiB/no swap, actual.NET11RC3,
stock isolated CPython3.13.16. Same workers/adapter0.4/dependencies/scripts/goldens
as the preceding checkpoint. Four independent processes/job, two per engine;
five-second warmup/role, seven rotated150ms rounds, ordinary GC/tiering.

| Complete script | Lane | Lython ms | CPython ms | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| scripts.csv-validated-records.large | compile | 0.0708–0.0711 | 0.0656–0.0662 | 1.07–1.08× | Diagnostic; L0.40%, P0.90% / 2.38% |
| scripts.csv-validated-records.large | warm | 4.5685–4.5700 | 4.2580–4.2828 | 1.07× | Diagnostic; L0.03%, P0.58% / 0.63% |
| scripts.csv-validated-records.large | compile-run | 4.6680–4.7144 | 4.4373–4.4587 | 1.05–1.06× | Diagnostic; L0.99%, P0.48% / 1.00% |
| scripts.text-summary-helpers.large | compile | 0.0448–0.0452 | 0.0417–0.0419 | 1.07–1.08× | Variable; L0.90%, P0.51% / 3.52% |
| scripts.text-summary-helpers.large | warm | 1.7126–1.7514 | 1.1809–1.1817 | 1.45–1.48× | Variable; L2.22%, P0.07% / 2.81% |
| scripts.text-summary-helpers.large | compile-run | 1.7960–1.8556 | 1.2819–1.3016 | 1.38–1.45× | Variable; L3.21%, P1.53% / 1.01% |
| scripts.file-search.large | compile | 0.0295–0.0297 | 0.0324–0.0326 | 0.91–0.92× | Diagnostic; L0.78%, P0.43% / 1.15% |
| scripts.file-search.large | warm | 0.2464–0.2502 | 0.1641–0.1643 | 1.50–1.52× | Diagnostic; L1.53%, P0.15% / 2.45% |
| scripts.file-search.large | compile-run | 0.3282–0.3312 | 0.2168–0.2212 | 1.48–1.53× | Variable; L0.93%, P2.03% / 1.99% |

Ratios above one mean slower. All four process-median pairings, not confidence
intervals. L/P indicate absolute same-engine process-median spread. Diagnostic/
Variable describe observed stability, not design acceptance rules. Text compile
IQR3.52%, warm L spread2.22%, complete L spread3.21% and search complete P spread
2.03% remain visible. No retiming or single-change attribution selected here.

Compile constructs/disposes code under the borrowed allowance, without guest I/O.
Warm reuses code; compile-run compiles once and executes once. Both execution
lanes include efficient mediated RAM-file I/O, complete stdout/file validation and
prior-result cleanup. Fixture reset/staging, protocol, process startup and final
cache teardown are excluded. Lython applies capability/path checks and isolated
imports; CPython uses builtin open and retains imported modules. Independent phase
medians need not add up. Wider process CPU includes reset/protocol and is not an
internal execution-cost measurement. Pending I/O, RSS and embedding tail latency
remain unmeasured. Search stops at1025/2049 lines; it is the keyword/default variant,
not the historical literal-prefix source.

Nine jobs complete246.994seconds. All identity/output/ownership/quiet checks pass;
workers are joined, owned RAM removed, persistent archive created/verified under
lease and SHA verified locally. [Receipt](archive-receipt.json), [terminal state](terminal-status.json),
[raw evidence](observations.tgz), [collection/source/setup](preparation.tar.xz),
[all results](summary.json). The original insufficient-space condition and refined
exact-copy retirement receipt remain; only canonically archived terminal copies
were retired, preserving current/reference images. No restart or abandoned user.

The [previous guide](prior-BENCHMARKS.md) preserves historical observations.
Publication remains postponed; normal optional-adapter pins remain unchanged.
