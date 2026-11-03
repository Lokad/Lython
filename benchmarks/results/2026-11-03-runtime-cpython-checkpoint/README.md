# Retained-runtime CPython checkpoint — B152

2026-11-03. Exact committed runtime/worker **bd3f9dee3d0f372f6738c1973cb042bd12903e20**,
core **578496bb2cd47ba826729dca9fe2cbadf277597caafe518d745e36a2d2e2f92c**.
Frozen after a joined nonincremental Release build,0 warnings. Includes B150 CSV
and retained B151 bulk casing; both scanner prototypes are absent. Later uncommitted
work is excluded. Full delivery41600267923 runs independently; qualification is
recorded separately. No publication, causal source speedup or guest-only CPU claim.

| Complete script | Fresh Lython / CPython ms (ratio) | Warm Lython / CPython ms (ratio) | Identical Lython control fresh / warm |
| --- | ---: | ---: | ---: |
|CSV summary/helpers|5.545 /3.551 (1.56×)|5.235 /3.305 (1.58×)|+0.58% /+2.99%|
|JSON-lines/helpers|5.829 /4.271 (1.36×)|5.820 /4.126 (1.41×)|+1.78% /-4.07%|
|Text summary/helpers|3.796 /1.297 (2.93×)|3.717 /1.188 (3.13×)|-5.53% /-5.71%|
|Search with early exit|0.508 /0.243 (2.09×)|0.386 /0.149 (2.60×)|-3.18% /+3.75%|
|JSON-to-Markdown report|5.923 /2.557 (2.32×)|5.809 /2.383 (2.44×)|+0.02% /-1.44%|
|Unicode filename filter|0.897 /0.419 (2.14×)|0.808 /0.340 (2.38×)|+2.01% /-1.49%|
|ASCII filename filter|0.789 /0.292 (2.70×)|0.675 /0.211 (3.20×)|+3.30% /+1.11%|
|Config read/edit/write|2.101 /0.334 (6.29×)|1.974 /0.241 (8.18×)|+1.71% /+4.67%|
|Copy/checksum/manifest|0.526 /0.312 (1.69×)|0.344 /0.202 (1.70×)|+0.05% /+1.49%|

CPython is3.13.16 PGO/LTO, ordinary GIL/GC, no JIT/debug, isolated `-I -S -X utf8`.
Machine is quiet dedicated Standard_F4as_v7, Ubuntu24.04.4, four EPYC9V45vCPUs,
16GiB/no swap. .NET11RC3 ordinary GC/tiering with no overrides. Identical Lython
images keep baseline/candidate labels from the reused collector; neither is older
source. Nine rotated ~100ms batches follow4s warmup/image. Do not silently adjust
ratios with controls. All IQRs and original samples are retained.

Fresh includes per-invocation compilation/disposal and one synchronous execution;
warm reuses compiled code. Both include actual mediated I/O, full stdout/file checks
and prior-result cleanup. Fixture reset/staging is outside both clocks but inside
wall deadlines. Final results stay funded through responses then dispose outside
the timer; cached programs/owners/processes/fixtures join outside. Startup, transport
and response hashing are excluded. Fresh globals, persistent CPython module/library
caches versus execution-isolated Lython caches. Efficient host reads/appends/copy;
this is not interpreter-only work or suspended-I/O performance.

CSV/report/config compilation only:86.55/60.77/57.84µs versus54.94/37.79/34.68µs,
ratios1.58/1.61/1.67; controls+2.41/−2.17/+0.63%. Compile cost is small relative to
config execution; do not attribute its6.29×fresh/8.18×warm gap to frontend passes.
Config warm IQR6.77%/control11.84%, text identical controls differ5.5–5.7%: keep this
uncertainty. Earlier90785737 search stalls remain unexplained; no retrospective
correction or erasure. Cross-checkpoint medians do not establish source gains.

Seven canonical source/fixtures are reused unchanged from B145. Existing config/
copy sources/fixtures are reintroduced from the earlier representative catalogue,
verified against current tracked script source. Their old v1-clock rows were a
material gap. Every image verifies both full stdout and files before measurement;
no missing result was dropped. Twenty-one phase jobs finish **391.128s**; owned
service exit0 in391.733s, all workers joined/owned fixtures removed, before/after
quiet gates PASS, shared VM lease free. No timeout, retry or recollection.

[Raw responses/gates](raw-results.tar.gz), [catalogue](catalog.json.gz),
[exact images](identity.json), [all medians/IQR/controls](assessment.json) and
[unchanged-tool collector](collect.py) preserve evidence. The previous f7dd/02efd
checkpoint stays at2026-11-03-corrected-cpython-checkpoint. Current guide is
[docs/BENCHMARKS.md](../../../docs/BENCHMARKS.md). Publication remains postponed;
substantial execution gaps and unique-JSON-name debt remain active work.
