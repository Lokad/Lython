# Rejected compiled parameter-layout sharing

The prototype prepared immutable def parameter partitions and slot maps once in
compiled storage, borrowing them in ordinary, lowered and generator definitions.
Live defaults stayed per function. It deleted temporary lists and an unused name
map, but introduced a shared-layout object with forwarding getters in call binding.
Conservative compilation admission increased before allocating that metadata.

**The prototype is removed.** Its affected fresh compile-to-result diagnostic
observed a higher candidate median. No retry, retuning, replacement samples or
second layout candidate follows this capture. Whether forwarding reads, code
layout, compilation or another component accounts for the difference is unmeasured.

| Fresh compile and synchronous execution | Earlier Lython | Rejected prototype | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Default-argument helper, 256 calls | 185.643 us | 293.721 us | 37.302 us |

The candidate median is 58.2% higher; baseline/candidate IQR-to-median is
10.4%/6.7%, CPython 0.4%. This is one diagnostic, **not a qualified regression,
current-HEAD multiplier or confidence interval**. The owned command took 7.285
seconds including cleanup under a 30-second bound. All seven rotating batches,
calibration and warmup remain in [the receipt](compile-run.json).

Both engines freshly compile and run the complete helper/loop script in an
already-running process, with fresh invocation state, output/projection, cheap
golden checks and prior-result cleanup. Process startup, transport and full response
hashing are outside the timer. This is not warm execution; it must not be combined
with warm-only rows. The helper defines a positional default and a keyword-only
default. Correct output is independently specified by the existing catalog.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 using
ordinary GC/GIL, `-I -S`, UTF-8 mode off and explicit UTF-8 protocol I/O. Lython's
ordinary public policies and GC/tiering remain; CPython has no equivalent containment.

Baseline is 3b34fad26ff6847073a96aa3c87e39404d0a44b4. Ten modified performance
sources are frozen in [the declaration](comparison-declaration.json) and preserved
as [a rejected patch](rejected-candidate.patch). Benchmark catalog/execution tooling
and Python worker are unchanged. [Baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json) match CPython at all three existing sizes.
[Cleanup/source proof](cleanup-source-proof.json) confirms seven worker processes
exited, exact source differences, identical tooling, inactive services and lease
availability. These source identities describe the rejected prototype only.

The prototype's full ordinary public run passed 8,890 cases and failed three
imported-code success fixtures under its larger estimate. Compact fixtures passed
all 22 import controls and two original-scale stress controls without changing
budgets. Those fixture changes and the estimate were restored when the prototype
was rejected. All prototype-only layout classes, fields, forwarding, constructor
plumbing and two architecture-specific facts were deleted. Initial build and
public failures remain in [validation](validation.json); no timeout was relaxed.

The retained runtime cleanup only deletes an unused binding name table and unused
parameter-list field. The canonical slot map, original layout construction,
capacity estimates, default ownership and guards remain. That smaller final
change is **unmeasured**, so none of the prototype timings describe it.

Four public controls remain: compile refusal before large frontend allocation;
independent defaults/closures/generator behavior on repeated sync and async runs;
and two concurrent executions sharing an allowance while retaining distinct
defaults/results. [Independent CPython output](cpython-golden.json) matches the
language witness. Final-source ordinary white-box checks pass 2,212 cases in
11.453 seconds; the focused public group passes 277 cases in 14.805 seconds,
including cleanup. Groups overlap earlier controls. Full public/platform/package
qualification follows at delivery; the failed prototype run is not a final pass.
No full stress rerun is claimed.

Layout sharing stays open with a stopping condition: another proposal must remove
per-definition work without adding reads to ordinary argument binding and must
fund compiled storage before allocation. Do not retune this producer or reopen
heap tracing. Larger-frame entry/copy work retains its separate guard-order gate.
