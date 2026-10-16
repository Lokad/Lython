# Immediate generator bindings — 2026-10-16

One unchanged generator-expression reduction observed a **17.1% lower Lython
median** after removing per-yield binding propagation. This is one short
diagnostic, without milestone/noise qualification or a claimed confidence
interval. Generator-expression bodies still use lowered interpretation. CPython
remains substantially faster on this workload.

| Engine | Measured revision | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | b387be6dc3fbeacdbd787dccddf7391da1ea21b3 | 4.919 ms | 0.64% |
| Lython after | 569a653a49c1aba9663c2197ab4984aa0f43eae1 | 4.077 ms | 1.11% |
| CPython | 3.13.16 | 0.5695 ms | 0.22% |

The existing `math.dyadic-fsum.large` case sums 16,384 dyadic float terms and
prints exactly `15360.0\n`. Independent before/after checks include 256, 2,048
and 16,384 terms. Source, fixture, golden output and comparison tooling are
unchanged. The measured script is:

```python
N = 16384
import math
print(math.fsum((i % 16) / 8.0 for i in range(N)))
```

Boundary: warm public `Run`, with compiled code reused and fresh invocation
state/output checking. Prior-result cleanup is timed; the final result stays
owned through the response. Compilation, process startup and protocol transport
are excluded from each invocation. The comparison host supplies a shared
unbounded allowance while retaining normal admission/scope accounting and other
public defaults; instruction fuel is unset. This measures the deployed invocation
contract, without equivalent in-process containment in CPython. Compile-to-result,
startup and embedding-process tail latency are unmeasured here.

Machine: the dedicated Azure VM described in [BENCHMARKS.md](../../../BENCHMARKS.md),
observed Ubuntu 24.04.4 LTS x86_64 with four AMD EPYC 9V45 vCPUs. Both Lython
workers use Release SDK 10.0.401/runtime 10.0.12, workstation GC, Interactive
latency and ordinary tiering. CPython uses normal GIL/GC and `-I -S`. Only the
supervisor disables its own tiering; worker overrides are absent. Seven rounds
completed in 7.223 s of reported supervisor collection/cleanup, under a
30 s systemd process-group cap. No repeat, retuning, extra lane or new producer.

[Raw samples](warm.json), [frozen declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture proof](before-proof.json) and [cleanup/source proof](cleanup-source-proof.json)
preserve binaries, source/archive hashes, public limits and all observations.
All seven workers exited, temporary fixtures disappeared, relevant services are
inactive and the exclusive VM lease is free.

[Failed setup](prepare-first-journal.txt): Windows-written CRLF shell scripts made
Bash reject `pipefail` before checkout or compilation. A collected unit's inactive
lookup was initially misread as success; the receipt preflight prevented timing.
Original scripts and journal remain in the private evidence; the declaration
records byte counts and the correction. LF-only scripts and an explicit exit
receipt preceded successful source/correctness/cleanup checks. Timing ran once.

The runtime change prepares walrus global/nonlocal metadata once, reuses existing
name storage, removes target-name scratch/sets and namespace scans at every yield,
and preserves immediate writes through filters/errors. Nonlocal maps are admitted
before allocation; retained cells survive suspended generator frames. Generator
functions and expressions share escaping StopIteration conversion and original
cause/context. Async terminal cleanup is awaited, including scope-admission
failure. No new memory-management layer or ownership tracing was added.

Forty-eight new controls cover 42 CPython programs in both execution modes,
four actual host suspension/cancellation cases, retained results from two
executions sharing an allowance, and denied admission awaiting outer cleanup.
All 2,638 ordinary white-box tests and 265 focused public checks pass. Earlier
lexical/cell checks also passed. Processes stay below 30 s; original stress scales
remain separate. Correctness/ownership controls support the implementation;
this timing does not establish complete interpreter-path retirement or an
end-to-end application speedup. Historical measurements do not describe later HEAD.
