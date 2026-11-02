# Complete CPython checkpoint after the linear engine — B133

2026-11-02, exactfd7bc7e4808937b57a9531261b012f46e9a96c0f.
Fresh exact-commit Release core547825f9dfe52ddf22e24e76b8bc54ae8975428dae5b5e22052dd223624d2c42;
the B132 source-pair core84302e8d was built before its commit. Production source
is the retained B132 design; informational revision changes with the exact build.
No independent-median source speedup or new optimization is claimed.

[Plan](pre-collection-plan.md), [collector](collect.py), [identities](identity.json),
[frozen source/fixtures/goldens](catalog.json.gz), [assessment](assessment.json) and
[all raw warmups/responses](raw-results.tar.gz) preserve the complete run.

| Case | Fresh Lython / CPython ms (ratio) | Warm Lython / CPython ms (ratio) |
| --- | ---: | ---: |
| CSV/helpers |7.480 /3.847 (1.94×)|7.258 /3.687 (1.97×)|
| JSON-lines/helpers |7.646 /4.676 (1.64×)|7.631 /4.520 (1.69×)|
| Text/helpers |5.168 /1.528 (3.38×)|5.333 /1.384 (3.85×)|
| Search/early exit |1.986 /0.376 (5.28×)|1.914 /0.266 (7.18×)|
| Report rendering |9.128 /2.742 (3.33×)|8.331 /2.555 (3.26×)|

Fresh Lython IQR2.31/1.44/2.23/3.24/3.11%; identical-control drift
−0.09/+0.39/+4.53/−2.07/−7.59%. Warm IQR1.15/0.44/1.21/4.27/2.98%; controls
−0.64/+0.90/−3.47/+2.86/+1.63%. Text/report precision remains limited by drift.
Earlier90785737 search fresh270%/218% and warm138%/77% IQR/control results remain
inconclusive and preserved; the current steadier point does not explain the waiting.
Historical config/copy rows in the main guide still describe082179c4 only.

Compilation only: CSV85.65µs/54.67µs (1.57×), report59.78µs/37.92µs (1.58×).
Lython IQR1.47/1.21%, CPython0.42/0.20%, identical controls+0.52/+0.50%.
No guest work included; startup is not measured. The roughly2% B132 compilation
regression comes from its controlled source pair, not subtraction of these medians.

Dedicated Standard_F4as_v7 Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,
.NET11RC3, ordinary workstation GC/tiering; CPython3.13.16 GCC13.3 PGO/LTO,
ordinary GIL/GC, no JIT/debug, isolated -I -S -X utf8. Existing B125 worker/driver:
independent same-current Lython/unchanged-Lython/CPython processes,4s warmup each,
nine interleaved~200ms batches. Source, fixture, full stdout/files and loaded-image
identities verified. All12 jobs223.132s; quiet gates PASS, owned service exit0/
inactive, workers joined, fixtures removed, lease free. No qualification retiming.

Fresh includes per-invocation compilation/disposal plus complete public execution.
Warm excludes compilation/startup. Both include fixture reset/staging, actual
mediated file I/O with efficient ranged reads/appends, full output/file checks and
previous-result disposal. Final result/cached-program/process/fixture cleanup is
outside batches and joined. Compile-only retains its final compilation outside
its timer. These boundaries match the existing public comparison worker.

Full delivery41540517622 qualifies exactfd7bc7e4 independently. Missing regex
producer capabilities defer only affected final adapter qualification. The remaining
whole-script gaps remain substantial; publication stays postponed.
