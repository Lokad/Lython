# Floating JSON numeric report against CPython

R471/B881,2026-11-17UTC: private cb7472e7+retained four-file refinement/frozen
5ecb2599 versus stockCPython3.13.16. Same4,096-record complete squared-error
script;56,495BJSON input (8,192numbers),93Breport plus stdout. Exact binary
fractions from the earlier integer fixture; independent goldens agree.

| Phase | Lython | CPython | Lython / CPython | L/C controls | MaxIQR |
| --- | ---: | ---: | ---: | ---: | ---: |
| Compilation | 0.0567–0.0578ms | 0.0609–0.0611ms | 0.93–0.95× | 1.95/0.26% | 0.52% |
| Warm execution | 1.7389–1.7401ms | 0.8285–0.8303ms | 2.09–2.10× | 0.07/0.22% | 1.79% |
| Compile and execute once | 1.8412–1.8718ms | 0.9562–0.9565ms | 1.92–1.96× | 1.66/0.03% | 0.79% |

Above1meansLython slower. Ranges are all process-median combinations, not
confidence intervals. Diagnostic exact magnitude, substantial remaining gap.
No source gain inferred across separate collections; improvements are in the
[source pair](../refined/README.md). Independently warmed phases are not additive.

Quiet AzureF4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs/16GiB/no swap,.NET11RC3 natural
workstationGC/normal tiering;CPythonGIL/GC. Existing command-v2 workers, fixed
three-case catalog, two independent processes/engine,5s full-request warmups,
five rotated roughly0.5s samples. Three jobs104.54s. Warm guest time includes
synchronous Run,mediated RAM I/O,complete stdout/file checks and prior-result
cleanup; excludes compilation,startup,fixture reset/protocol/final cache teardown.
Fresh adds compilation and compiled-owner disposal. External CPU clocks are wider.
Lython applies host mediation/shared-budget admission;CPython uses native buffered
files without equivalent containment. No startup/async/RSS/tail comparison.

All identities/goldens/quiet/Closed/exits/joins/persistent-archive checks pass;
RAM removed,terminal cached. Fresh ownership/path/hash/link/all-alias proofs
retire only493,621Bof committed working raw replicas from B878/B879. Canonicals,
current images/toolchains and254/252MiBstaging/final floors preserved. Normal
adapter0.4unchanged,publication held; full runtime delivery qualification separate.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[catalog/procedures](procedures-and-catalog.tar.xz),[terminal](terminal-status.json).
