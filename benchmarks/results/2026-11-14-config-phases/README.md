# Small configuration edit: compilation and complete execution

2026-11-14UTC, R336/B707: committedcdd92944/frozenf4c4f027 versus stock
CPython3.13.16. Focused R335Release11/10 checks pass; full platform/stress/package
qualification is[still running43536449801](https://github.com/Lokad/Lython/actions/runs/43536449801).
This is a new phase-selection observation, not a runtime prototype or speedup
against an older Lython. No unchanged source qualification is repeated.

| Phase | Lython µs | CPython µs | Lython/CPython | Process spread Lython/CPython | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
|compile|36.59–37.31|34.31–34.36|1.065–1.088×|1.94/0.17%|1.97%|
|warm|75.26–76.43|59.60–59.69|1.261–1.282×|1.56/0.15%|1.55%|
|compile-run|136.51–137.35|121.39–124.21|1.099–1.131×|0.61/2.27%|3.65%|

The existing `scripts.config-edit.empty` case reads96B of JSON, changes enabled/
retries, appends a path, writes sorted UTF8 JSON plus newline, and prints the path
count. `empty` means an initially empty paths collection; it is a complete357B
file-processing script, not an empty program. It preserves non-ASCII data and a
nested field. Input differs from the181B historical config row, which is retained.
Current fixed worker renders the existing catalog; script/fixture/output identities
are exact. Independent local CPython3.13.2 and frozen-worker goldens pass first.

B707completes101.301s/all gates under one240s owner, quiet
AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary workstation GC/tiering and CPython
GIL/GC. Two independent processes per engine/phase, five-second full-request wall
warmups and five rotating0.5s wall-sized batches. Ratios span all four process
median pairings, not confidence intervals. All raw samples and wider CPU survive.
Fresh CPython process spread2.27% and one Lython fresh IQR3.65% remain explicit.

Warm clocks include mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup; compilation/startup, fixture reset/protocol and final caches
are excluded. Compile is compilation/compiled-owner disposal only; compile-run
adds both to execution. Each lane warms independently. **Do not add/subtract these
medians to attribute costs within one execution.** Wider process CPU includes
reset/protocol; its near-equal fresh medians are not isolated interpreter CPU.
Cancellation latency, shared-budget contention, RSS and embedding tails are not
measured here. Both engines use the same Python source/fixtures/goldens.

## Phase decision and stopping condition

Compilation is relevant for this short job but no disproportionate compiler cliff
is demonstrated:36.6–37.3µs versus34.3–34.4µs CPython. Warm and fresh gaps remain.
These observations do not establish repeated frontend facts, builtin construction
or a particular call/frame allocation as the dominant removable cost. Existing
[R297owning-source evidence](../2026-11-13-config-cpu-source-gate/README.md)
has prominent mediated resolution/open work and substantial unknown kernel leaves;
its CPU shares are historical, not inferred current fractions. Do not bypass host
containment or claim those costs are core interpreter work.

This bounded gate ends without a shared-type/cache or frontend-visitor prototype.
Current source already prepares shared lexical facts. Continue whole-script
construction/transport work from concrete remaining phases; materially different
evidence can justify refinement. No broad retiming or new benchmark framework.
No allocation reduction, source-pair gain or performance-objective completion is
claimed. The useful phase/CPU uncertainty is retained rather than declared failure.

All identity/golden/balance/quiet/join/archive gates pass. Exact current core/
581source inputs live in the[R335source archive](../2026-11-14-incremental-xml/source-checks.tar.xz).
No new producer/core build or pin change. Initial local selection found no config
in an older trimmed catalog; frozen catalog rendering then needed its script data.
Those setup-only conditions precede timing; the original missing-data log survives.
The corrected renderer uses the same fixed worker and existing script data.
The VM collection itself completes once; all users join, RAM fixtures unmount and
persistent evidence SHA verifies. No image/evidence retirement or disk-floor change.
Publication remains held.

[Summary/wider CPU](summary.json), [raw](observations.tgz),
[collection sources and setup conditions](collection-sources.tar.xz).
