# Shared zero-argument string call entry — 2026-10-16

Immediate exact-string `strip()`, `split()` and `lower()` now share one guarded
entry. Three untyped markers and three duplicated entry helpers are deleted;
each interpreter has one dispatch branch instead of three. Five production files
change by 24 insertions and 54 deletions. Three fixed static operation delegates
capture no receiver or execution data and are reused across lookups. Kernels,
compiler arity restrictions, exposed/general/custom-object calls, preflights,
result ownership and the original two call checks remain unchanged.

| Engine | Measured revision | Warm complete-job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | df9093cf548a7cede9cc119750f74d86b2f5ea12 | 6.193 ms | 4.44% |
| Lython after | ffdac05d17db65f7f64c57e7cc3bd66b46f69146 | 6.293 ms | 9.04% |
| CPython | 3.13.16 | 1.3010 ms | 1.94% |

**Inconclusive.** The candidate median is 1.6% higher, within its 9.0% spread.
This one short diagnostic establishes neither an elapsed benefit nor a regression.
The change is retained for source simplification; the indirect delegate call's
individual cost is unmeasured. No qualified ratio, allocation count or confidence
interval is claimed. BD and AY remain separate historical source pairs.

The unchanged [complete text-summary job](../../scripts/text-summary.py) reads
3,072 lines / 35,840 UTF-8 bytes, calls lower().split(), counts 6,144 words, writes
four sorted report lines and prints `6144\n`. Independent stdout/file goldens pass
at 0, 16 and 1,024 fixture repetitions for each engine. Actual case, source hash,
size, byte/line counts and lane match the declaration before and after capture.

Boundary: warm synchronous public Run, reused compiled code, fresh invocation
state, file staging/read/count/write, exact output checks and prior-result cleanup
timed. Last-result ownership survives through its response. Compilation, worker
startup and transport are excluded. Lython limits, conservative ownership and
cancellation stay enabled; optional fuel is unset. The host supplies efficient
ranged I/O and a shared unbounded allowance. CPython has no equivalent in-process
containment. Async, fresh compilation and embedding latency are unmeasured.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, 16 GiB RAM, no swap; SDK 10.0.401/runtime 10.0.12. Workers keep ordinary
GC/tiering. CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO. Supervisor
tiering alone is disabled. Seven rounds span 7.744s under a 30s group cap. One
capture, no retry/retuning/new producer/long lane. All 524 production sources per
role match frozen Git manifests; comparison tooling/backend are identical. Seven
workers/fixtures are gone, services inactive and VM lease free.

Validation: 2,734 ordinary white-box tests pass (18.101s including cleanup), 138
focused string/casing/ownership checks (1.193s), 205 public casing/ownership/
terminal-refusal/shared-budget checks (2.517s), and 57 original stack/attribution/
copy controls (8.161s). Existing eight-step fuel/spans, retained/exposed methods,
first-refusal origin, real receiver suspension/cancellation and owner cleanup
remain covered. No tests, original stress scales, stack policy or timeouts change.
Five independent CPython cases match through both APIs; native-str subclassing
remains an explicit separate unsupported class-header case.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[frozen guards](baseline-guards.json), [candidate guards](candidate-guards.json)
and [local validation](local-proof.json) preserve evidence. Frozen archives,
LF-validated scripts and detailed local receipts remain in private notes.
