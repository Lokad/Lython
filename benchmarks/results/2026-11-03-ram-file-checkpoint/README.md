# RAM-backed config/copy comparison — B156

2026-11-03. Supplemental controlled storage condition; ordinary filesystem B152
observations remain intact. Native mediated file APIs, script/source/goldens,
options, compilation/execution/cleanup boundaries and engines are unchanged.
Only fixture temporary storage moves to /dev/shm (mounted tmpfs), using existing
TMPDIR in both workers and the controller. This still performs actual file I/O,
not an in-memory host replacement or a new benchmark option/framework.

| Complete script / phase | Earlier Lython ms | B154 Lython ms | CPython ms | B154 / CPython | B154 elapsed Δ / unchanged control |
| --- | ---: | ---: | ---: | ---: | ---: |
| Config warm |0.377003|0.327987|0.184360|1.779×|−13.00 /−0.78%|
| Config compile-run |0.488269|0.438323|0.253084|1.732×|−10.23 /+0.25%|
| Copy/manifest warm |0.227239|0.226710|0.149954|1.512×|−0.23 /+0.39%|
| Copy/manifest compile-run |0.394326|0.399897|0.238304|1.678×|+1.41 /+0.06%|

Earlier source is exact qualifiedbd3f9dee, core578496bb; B154 runtime source is
committed0f05d7c0f8791e5e94c2ed9c4f6e5d94de443209, measured in precommit frozen
corec53e91be. Both changed production source hashes still match that commit;
these are the only production changes from bd3. Exact hashes/unchanged auxiliary
normalization are in identity.json; committed rebuild metadata can differ.
CPython3.13.16 PGO/LTO, ordinary GIL/GC/no JIT/debug; .NET11RC3 ordinary GC/tiering.

Current config warm/fresh IQR0.27/1.21%, CPython0.27/0.49%; unchanged controls
−0.78/+0.25%. RAM-backed whole-config improvement is demonstrated in this condition;
ordinary-filesystem elapsed improvement remains unproven. External process CPU
warm−10.53%/control−2.80%; fresh−6.91%/control+9.71% is less precise. Copy fresh
+1.41% is adverse and retained (candidate2.07% IQR). No generalized copy gain.

Same bd3 runtime is2.045×CPython warm on RAM versus8.184×in the ordinary-filesystem
B152 measurement; fresh1.929×versus6.294×. Independent captures cannot precisely
decompose the difference. Alongside B155's actual separate ftruncate versus
CPython O_TRUNC observations, this establishes strong storage sensitivity. It
does not prove the full gap is I/O, justify deleting truncation or weakening host/
admission contracts, or predict another host/VM/filesystem. Config CPU remains
slower than CPython and the text/report gaps remain open.

Quiet dedicated F4as_v7/Ubuntu24.04.4, exact existing four-worker protocol2:
baseline/candidate/unchanged baseline/CPython,4s warmup per image, nine rotated
~100ms batches. Fresh compiles/disposes per invocation; warm reuses programs.
Actual I/O/full checks/prior-result cleanup are timed; fixture reset/startup/
transport/final result/program/process/fixture teardown outside. All declared
stdout/file goldens, identities, before/after quiet gates and cleanup/joins PASS.
Four jobs80.713s, service81.315s/exit0, lease free. No retry or missing observation.
[All observations](raw-results.tar.gz), [controls/dispersion](assessment.json).
Publication remains postponed. Full B154 qualification41609489628 is now GREEN on exact0f05d7c0.

Full delivery41609489628 is GREEN on exact0f05d7c0: Windows/Ubuntu ordinary
Release.NET11/10, original Stress and installed consumers all PASS. Every required
step is verified in the cached delivery-ci-status.json under the B154 evidence.
No unchanged replay. Publication remains postponed.
