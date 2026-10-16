# Direct synchronous generator advancement — 2026-10-16

Ordinary synchronous executable generator pulls now enter and execute their
retained frame directly, without the async advancement state machine. Both modes
share validation, managed stack admission, frame entry/exit, exception restoration
and result reading. Terminal synchronous cleanup still bridges the shared helper;
async cleanup remains awaited. General generator-method parsing and yield-from
forwarding retain their existing async helpers. This removes one hot wrapper,
not all async machinery from synchronous execution.

| Engine | Measured revision | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | b52be7e493c2594202658169eb36d33975d8f55f | 4.118 ms | 3.67% |
| Lython after | 44199dd36bc394100e9b055094514575eb83f59a | 3.912 ms | 1.55% |
| CPython | 3.13.16 | 0.5586 ms | 0.39% |

One short diagnostic observed a **5.0% lower Lython median**. No noise-qualified
ratio, confidence interval, overall speedup or attribution of the remaining gap
is established. Historical measurements identify these revisions, not later HEAD.
The previous executable-body capture is a separate source pair, not an extra
sample in this comparison.

The unchanged `math.dyadic-fsum.large` workload sums 16,384 float terms, 73 UTF-8
source bytes, with exact output `15360.0\n`:

```python
N = 16384
import math
print(math.fsum((i % 16) / 8.0 for i in range(N)))
```

Boundary: warm public `Run`, compiled code reused with fresh invocation state,
output checking and prior-result cleanup timed. Final results stay owned through
the response. Compilation, startup and protocol transport are excluded. The host
supplies a shared unbounded allowance; admission/scope accounting and ordinary
public limits remain active, optional fuel unset. CPython has no equivalent
in-process containment. Fresh compile-to-result, startup and embedding-process
latency remain unmeasured.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, SDK 10.0.401/runtime 10.0.12. Frozen Release Lython workers retain
workstation GC, Interactive latency and ordinary tiering. CPython 3.13.16 uses
normal GIL/GC, `-I -S`, GCC 13.3 PGO/LTO. Only supervisor tiering is disabled.
Seven rounds span 7.176 s under a 30 s systemd process-group cap; one capture,
no repeat, retuning, new producer or full lane. Both roles passed independent
correctness at 256, 2,048 and 16,384 terms before timing. All 524 production sources
per role match frozen manifests, with identical comparison tooling. Seven workers
and their fixtures are gone, relevant services inactive and exclusive VM lease
free. LF-validated setup completed with an explicit zero exit receipt.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), correctness ([before](baseline-correctness.json),
[after](candidate-correctness.json)), [pre-capture gate](before-proof.json) and
[cleanup/source proof](cleanup-source-proof.json) preserve evidence. Source
archives, scripts and local receipts remain in private delivery notes.

The change also fixes non-None sends to a generator closed before starting:
exhaustion now precedes first-send validation, matching CPython. One of three
independent CPython lifecycle controls failed on the baseline and passes after;
invalid first send leaves the generator usable and reentrant next stays terminal.
Ten native controls cover caller exceptions/frame counters and refused
memory/cancellation/function/interpreter re-entry after yield, without another
native move and with deterministic storage release. All 2,672 ordinary white-box
and 344 focused public checks pass; the complete white-box process took 17.677 s
including cleanup. Retained aliases, admission preflights and managed stack guards
remain. No stress-scale, current-HEAD, full-lane or host-latency claim follows.
