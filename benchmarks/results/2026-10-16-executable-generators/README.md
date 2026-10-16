# Executable generator expressions — 2026-10-16

Ordinary generator-expression bodies now use resumable child instructions and
existing capture cells. This short diagnostic establishes **no speedup**:
4.330 → 4.252 ms is a 1.8% median change, within the candidate's 3.8% IQR/median.
CPython remains substantially faster. No confidence interval, qualified workload
ratio, overall gap attribution or application-wide improvement is claimed.

| Engine | Measured revision | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | c74f53c9b1f1d96f3cd5b513847086a14034cdf3 | 4.330 ms | 1.45% |
| Lython after | fa2b72af37e7c50ccf8ffa556683ea29833c257c | 4.252 ms | 3.83% |
| CPython | 3.13.16 | 0.5611 ms | 0.26% |

The unchanged `math.dyadic-fsum.large` case sums 16,384 dyadic float terms,
73 UTF-8 source bytes, and prints exactly `15360.0\n`:

```python
N = 16384
import math
print(math.fsum((i % 16) / 8.0 for i in range(N)))
```

Boundary: warm public `Run`, compiled code reused with fresh invocation state,
output checking and prior-result cleanup timed. The final result stays owned
through the response. Compilation, startup and protocol transport are excluded.
The comparison host supplies a shared unbounded allowance; admission/scope
accounting and ordinary public limits remain active, optional fuel unset.
CPython has no equivalent in-process containment. Fresh compile-to-result,
startup and embedding-process tail latency are unmeasured.

Machine: dedicated Azure VM from [BENCHMARKS.md](../../../BENCHMARKS.md), Ubuntu
24.04.4 LTS x86_64, four AMD EPYC 9V45 vCPUs. Both Lython workers are frozen
Release sources built with SDK 10.0.401/runtime 10.0.12, workstation GC,
Interactive latency and ordinary tiering. CPython uses normal GIL/GC and `-I -S`.
Only supervisor tiering is disabled. Seven rounds span 7.219 s in the supervisor
timestamps, under a 30 s systemd process-group cap; one capture, no repeat,
retuning or new producer. Both roles passed independent correctness at 256,
2,048 and 16,384 terms before timing. All 524 production sources per role and
shared comparison sources match the frozen manifests. All seven workers and
fixtures are gone; relevant services are inactive and the exclusive VM lease is free.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json) and [cleanup/source proof](cleanup-source-proof.json)
preserve the evidence. Source archives, scripts and local build/test receipts
remain in the private delivery notes. Scripts were LF-validated before upload;
preparation completed with an explicit zero exit receipt before timing.

The implementation shares eager/lazy loop compilation and the existing generator
frame/protocol, removing the ordinary lowered creator overlay, forwarding and
walrus-only capture scan. Source checks demonstrate real ForNext/Yield/slot
instructions, live captures and no fallback-only parent mirroring. Fourteen new
controls cover five CPython lifecycle programs in both APIs, actual suspended
filter truth success/cancellation, IR/cells, native unstarted close/throw,
cancelled/refused entry, before-acquisition admission and held owner funding.
All 2,657 ordinary white-box and 319 focused public checks pass. Required
class/lowered routes retain their earlier iterator-only method interface; this
is not complete retirement of every execution route. Conservative larger frame
estimates can deny earlier. No new memory-management infrastructure or native
code was introduced. Historical timings do not describe later HEAD.

Subsequent CI found that this revision overcharged a large unadvanced body by
instruction count. The following operand-capacity correction preserves the fixed
1 MiB regression cap and is not measured by this capture. These timings identify
fa2b72af explicitly; they must not be attributed to the corrected delivery HEAD.
