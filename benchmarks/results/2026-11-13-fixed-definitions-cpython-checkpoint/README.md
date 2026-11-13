# Qualified fixed-definition runtime against CPython

2026-11-13 UTC. Runtime cb8736ff, test correction90de765d, frozen core5a184a1d,
stock CPython3.13.16. The [source-matched delivery](../2026-11-13-builtin-core-definitions/delivery/README.md)
passes Windows/Ubuntu actual.NET11/10, original stress and installed consumers.
All573frozen inputs match that exact CI commit. The later positional ZIP prototype
is excluded. Generated metadata does not imply byte identity with CI binaries.

The existing quiet Azure F4as_v7 VM has four AMD EPYC9V45 vCPUs,16GiB/no swap,
Ubuntu24.04.4 and.NET11RC3. Ordinary workstation GC/tiering and CPython's GIL/GC
remain enabled. Existing fec79comparison worker and adapter0.4 are unchanged.

| Complete workload / phase | Lython ms | CPython ms | Lython / CPython | L/P process spread; maximum IQR |
| --- | ---: | ---: | ---: | --- |
| CSV invoice warm |7.596–7.646|6.012–6.069|1.25–1.27×|0.66/0.95%;1.41%|
| Invoice compilation |0.1024–0.1039|0.09334–0.09365|1.09–1.11×|1.50/0.33%;2.42%|
| Invoice compile-to-result |7.686–7.806|6.310–6.365|1.21–1.24×|1.56/0.86%;2.59%|
| Captured-helper text warm |1.438–1.442|0.8862–0.9001|1.60–1.63×|0.24/1.55%;1.70%|
| JSON-lines helpers warm |3.424–3.473|4.023–4.089|0.84–0.86×|1.42/1.61%;3.24%|
| Keyword-default file search warm |0.2524–0.2552|0.1486–0.1498|1.68–1.72×|1.10/0.84%;1.98%|
| JSON-to-Markdown report warm |2.829–2.876|2.309–2.347|1.21–1.25×|1.65/1.58%;5.63%|

Two processes per engine,5s warmup per process (12s captured text/report), five
rotated500ms rounds. Ranges span process medians; ratios include all four pairings,
not confidence intervals. Complete collection takes296.496s within its360s owner.
All identity, full stdout/file golden, quiet-before/after, actual joined-user and
archive/SHA checks pass. Report and JSON retain visible sample variability.

Warm clocks include mediated RAM-file I/O, full output validation and prior-result
cleanup. Compile-to-result also compiles and disposes the compiled owner.
Compilation alone excludes execution. Startup, fixture staging/reset, protocol and
final cache teardown are excluded; wider process CPU additionally includes reset
and protocol. Worker stdout/file-output equality is checked every invocation.
Standard standalone options are used; this does not measure shared-budget
contention, pending-I/O latency, RSS, heap peaks or embedding tail latency.

The material captured-text/search/report gaps persist. JSON is faster here, with
variability retained. Differences from older collections do not isolate a change;
do not multiply historical optimization percentages into these ratios. This is a
representative diagnostic comparison, not a universal performance claim.

The initial launch refuses before any mutation because the unchanged VM space
floor cannot admit staging. No collector ran in that attempt. Original command,
configuration and failure log survive in collection-checks.tar.xz. A bounded
confirmation retires only obsolete catalog/core working copies whose exact bytes
already exist in committed canonical archives. Under a fresh idle lease it verifies
resolved paths, UID, inode, hashes and every hard-link alias before unlinking.
The252MiB floor plus2MiB extra staging headroom remains unchanged. All actual users
join and RAM traces are archived/verified before unmount; no VM users/fixtures remain.

[Raw observations](observations.tgz), [summary and wider CPU](summary.json),
[ownership/archive receipt](archive-receipt.json), [terminal status](terminal-status.json),
[collection and original failed conditions](collection-checks.tar.xz),
[previous guide and all historical rows](prior-BENCHMARKS.md).
Publication remains postponed; the performance roadmap continues.
