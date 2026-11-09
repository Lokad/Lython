# One executable function engine

R145/B502, 2026-11-09. Committed081052fa/core3f8d versus working callable
consolidation/corecce0. Shared frozen worker/adapter/dependencies. Both roles run
the same complete CSV/text/JSON scripts with class-defined lambda helpers, plus
unchanged maintained report/search controls. These are source-pair comparisons,
not CPython ratios or comparisons between different script shapes.

Compile lambda code once using the actual enclosing executable builder's capture
facts. Remaining class/annotation/lazy-type consumers keep namespace closures,
with code prepared before execution. Remove PyFunction, its synchronous mode and
177-line duplicate dispatcher, runtime lambda lowering/compilation and retained
unused lowered lambda bodies. Keep the lowered evaluators with real consumers.
No new closure-analysis policy, dispatch framework, public option or quota.

| Complete warm script | Allocation, sync / completed-I/O async | Quiet elapsed | Wider process CPU |
| --- | ---: | ---: | ---: |
| CSV validation/class helper | −23.43% / −26.40% | −9.19–11.94% | −4.00–10.00% |
| Text summary/class helper | −74.16% / −75.70% | −37.52–39.26% | −35.82–38.24% |
| JSON-lines/class helper | −56.29% / −57.29% | −12.53–14.74% | −13.16% |
| Unchanged report control | approximately unchanged | **+0.05–1.12%** | **0.00–4.44%** |
| Unchanged search control | approximately unchanged | −1.92–+0.56% | −1.01–+1.02% |

Ranges span both candidate medians against both references, not confidence
intervals. Five quiet jobs/142.598s, four independent roles, five-second warmups,
seven rotated150msrounds, ordinary GC/tiering on the reference F4as_v7/.NET11RC3.
CPU is process-wide and coarsely sampled; report neutrality is not established.
Do not use these controls to settle earlier R142 report regression debt.

Warm clocks include mediated RAM-file I/O, complete stdout/file validation and
prior-result cleanup. Compilation, startup, fixture staging/reset/protocol and
final cache teardown are excluded. Allocation comes from the separate desktop
diagnostic. Its elapsed/control drift remains evidence, including report +4.4%
against one reference and +1.0–1.3% with async; no desktop CPU claim.

Preparing formerly runtime-only code increases affected compilation allocation
**3.18–5.24%**; controls +0.05–0.06%. Retain this explicit cost for the coherent
engine deletion and affected benefit. Compilation elapsed/CPU and pending-I/O
latency are not measured here; no universal or startup speedup claim.

Focused checks pass per actual.NET11/10:357 internal and1,722 public, then46
compiler/lambda checks including two extra bound/default cases. Nine independent
CPython3.13 goldens cover class/default/global/live/deleted captures, generator
lambdas, lazy aliases and type scopes. Existing cancellation, terminal refusal,
retained aliases, shared-budget reuse and genuinely pending awaited cleanup remain.
Fixtures bypassing compilation now use the actual executable entry/owned workspace;
independent semantic/refund assertions survive. Original prototype structural-hash
stack overflow, class-cell failure and missed lazy preparation are retained, with
their corrections. Full platform/Stress/installed-package qualification batches at
delivery; publication remains postponed.

All output/identity/quiet/Closed/join/RAM-unmount/archive-under-lease/local-SHA gates
pass. Archiveba17154c27cf1261393c01fb5018dd1d1d6c057b46f1eec79e61c1b5b98a5f46.
Terminal cached; no users/fixtures. No space recovery or floor/headroom change.

[Quiet summary](summary.json), [raw observations and exact cores/tooling](observations.tgz),
[desktop results and compilation allocation](local-summary.json), [identity](identity.json),
[terminal](terminal.json), [independent script goldens](script-goldens.json),
[Python semantic goldens](python-goldens.json), [source/qualification status](delivery-source.json),
[preparation, original failures and focused checks](preparation.tar.xz).

First full delivery42586293361/source65ed6cf7 fails on both OS:4,343 internal pass/
109 raw-module fixture failures,16,885 public pass/one real compiler failure.
Lazy alias/type-parameter scopes were traversed as module scope, incorrectly
disabling implicit-super preparation elsewhere. Correct their scope classification;
keep both ordinary module reads and lazy callable preparation. Three fixture
families now compile/execute module setup, explicitly retaining the namespace when
later host calls/mutations need it. Independent assertions remain. Five additional
CPython goldens cover lazy bounds/defaults and super. Later actual10/Stress/package
stages were skipped; corrected delivery remains pending. Measured source65ed is
unchanged historical evidence, not a timing claim for the correction.
[Failed CI](failed-delivery-ci.json), [correction/source status](delivery-correction.json),
[independent goldens](lazy-super-goldens.json), [original failure and focused checks](delivery-correction.tar.xz).
