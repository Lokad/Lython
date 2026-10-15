# Direct JSON value-dispatch forwarding

The JSON value wrapper now returns the existing parser `ValueTask` directly after
its unchanged whitespace and incomplete-input validation. This deletes one
redundant async forwarding state machine and await per value. Every caller already
awaits inside its existing parser scope, preserving validation failures, genuine
hook suspension, input ownership and cleanup. No second parser or execution route.
Deeper recursive async parsing and remaining token conversion work stay open.

| Complete JSON-lines aggregation file job, warm synchronous execution | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| 4,096 records / 110,592 UTF-8 input bytes | 9.425 ms | 9.692 ms | 4.260 ms |

**No elapsed-time benefit is established.** The observed 2.8% higher median is
within baseline/candidate IQR-to-median 14.3%/7.3%; CPython spread is 1.2%.
Retain the wrapper deletion without claiming a speedup, regression, confidence
interval or overall multiplier. One [diagnostic](warm.json) preserves seven
rotating batches, calibration and warmup. No retry, retuning or replacement
samples. The owned command took 10.353 seconds including cleanup under a
30-second bound.

The [ordinary script](../../scripts/json-summary.py) reads JSON lines, parses and
filters records, updates a dictionary and writes a complete JSON report. Both
worker timers include matched disk input/output staging, fresh state,
execution/projection, exact report-byte checks and prior-result cleanup.
Compilation, process startup, transport and full response hashing are excluded.
The timed default parser has no hooks. Real hook/host suspension is validated
separately, without injected-delay speed comparisons. This is a finite complete
job; sustained streams use bounded host-driven executions.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
ordinary GC/GIL, `-I -S -X utf8` and explicit UTF-8 protocol I/O. Lython's ordinary
policies, shared allowance and GC/tiering remain; CPython has no equivalent
in-process containment.

Baseline is 505833275217e87f2322cfa0a276495885f69f1b. The working candidate adds
only the one production source frozen in [the declaration](comparison-declaration.json).
The benchmark catalog, execution tooling, Python worker and script are unchanged.
[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent stdout/report-byte goldens at all three existing sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies seven workers and file
fixtures cleaned, exact source differences, identical tooling, inactive services
and lease availability.

All 238 focused public JSON regressions and two existing white-box JSON controls
pass, in 7.328 and 1.187 seconds including cleanup. They cover malformed input,
depth/strictness and prefix positions, default/hooked parsing, truly suspended
callbacks and selected classes, cancellation, reuse after cancellation and owned
storage. No architecture-specific test is added for the two-line forwarder.
[Validation](validation.json) retains receipts. The preceding uppercase source
passed 2,230 ordinary white-box tests; that is separate evidence, not a fresh full
JSON-source run. Full platform/package qualification is batched with uppercase.
No full stress or local full-public rerun is claimed.

This closes one forwarding wrapper, not all default-path async overhead, JSON
performance or AP05. Remaining work keeps one shared parser and real callbacks;
do not introduce duplicate parsers or a broader frame redesign for this slice.
