# Explicit positional call activations

The bounded M4 change replaces recursive CLR invocation of ordinary positional
executable functions with caller/callee links between existing interpreter
objects. It reuses binding, local storage, closure cells, opcode semantics and
exception routing. Caller operands remain funded through the callee's awaited
cleanup. General keyword/expanded calls, native callback entries and lowered
class methods retain their actual existing invocation routes. Recursion limits,
managed stack admission and large-stack workers remain unchanged.

Candidate: `4b408097ebca427dc12ab186259c7bb88c0224a0`. Control:
`c0846e6919c2d9c5095c45ac5169ae5ef2cba242`. Both use .NET 11 RC3,
SDK `11.0.100-rc.3.26522.148` and runtime `11.0.0-rc.3.26522.148`.
The change is retained for bounded ordinary-call CLR stack growth, **without a
qualified performance gain**. This does not establish native callback safety for
the separately rejected class-suite migration.

## Structural and correctness checks

The new managed regression samples the CLR stack at guest recursion depths 1
and 64. On the unchanged Ubuntu control it grows by 504 frames under Run and
1,575 under RunAsync; both tests fail. The candidate keeps the difference within
12 frames on Windows and Ubuntu. The diagnostic uses managed StackTrace only
inside tests; runtime admission adds no native telemetry or stack probing.
The [original failure](original-regression.log) also records two passing controls
for all finally blocks, guest traceback frames and allowance cleanup.

Candidate Release checks:

| Platform / target | Internal | Public | Scope |
| --- | ---: | ---: | --- |
| Windows / net11 | 3,882 | 14,991 | Full ordinary suites |
| Windows / net10 | 543 | 1,825 | Focused call/closure/generator/ownership and stack checks |
| Ubuntu / net11 | 573 | 418 | Focused calls, representative scripts, memory, resumed native stacks |

Both-target Windows builds and the Ubuntu net11 build have zero warnings/errors.
Ubuntu [build](build.log), [internal](white.log) and [public](public.log) logs are
retained. New public controls exercise cancellation during actual delayed host
work, terminal descendant admission refusal, retained results across another
execution and shared-budget reuse. Two isolated resumed-stack scenarios assign
module-defined functions to `__bool__` and `__hash__` slots. All pass.
Original stress, full net10/Ubuntu suites and current package consumers remain
delivery checks; these focused results do not replace them.

## One fixed short comparison

Collected once on 2026-10-29 using the shared lease on the dedicated Azure
Standard_F4as_v7 VM: Ubuntu 24.04.4 x64, four EPYC 9V45 vCPUs, 16 GiB RAM,
no swap. CPython 3.13.16 is GCC 13.3 PGO/LTO with ordinary GC/GIL and `-I -S`.
All engines run identical source and complete output/file goldens. No row was
retimed to obtain a favorable result. The seven jobs took 56.99 seconds; quiet
checks passed before and after. Every receipt remains performanceQualified=false.

Times below are milliseconds/job; IQR is interquartile range divided by median.
Warm execution excludes compilation; warm-async uses synchronously completed
host operations and does not measure real I/O suspension. Compile/run includes
fresh compilation and one complete execution inside an already-running host.
Invocation setup, full work, output validation and declared cleanup are included;
startup, protocol transport and fixture hashing are excluded.

| Lane / workload | Control ms | Candidate ms | CPython ms | Control / candidate IQR |
| --- | ---: | ---: | ---: | --- |
| [compile-run / CSV helper](00-compile-run-scripts.csv-summary-helpers.large.json) | 8.780 | 8.784 | 3.702 | 5.1% / 3.3% |
| [compile-run / text helper](01-compile-run-scripts.text-summary-helpers.large.json) | 37.861 | 9.476 | 1.455 | 71.8% / 15.5% |
| [warm / CSV helper](02-warm-scripts.csv-summary-helpers.large.json) | 30.603 | 7.683 | 3.487 | 98.3% / 3.6% |
| [warm / text helper](03-warm-scripts.text-summary-helpers.large.json) | 31.268 | 6.165 | 1.338 | 57.3% / 23.2% |
| [warm-async / CSV helper](04-warm-async-scripts.csv-summary-helpers.large.json) | 9.434 | 9.777 | 3.445 | 6.7% / 2.3% |
| [warm-async / text helper](05-warm-async-scripts.text-summary-helpers.large.json) | 31.346 | 12.335 | 1.330 | 56.1% / 95.8% |
| [warm / integer loop control](06-warm-loops.integer.large.json) | 1.054 | 1.056 | 0.578 | 0.8% / 1.1% |

CSV input is 4,096 records / 57,361 bytes; text is 3,072 lines / 35,840 bytes.
CSV compile/run is unchanged and async CSV is roughly 3.6% slower in this narrow
diagnostic. The control loop is stable. The large apparent improvements in other
rows have noisy controls or candidates and cannot establish speedup. There is
still a substantial CPython gap. No special optimization or favorable rerun follows.

[Collection metadata](collection.json), [initial machine gate](machine-before.json)
and [final machine gate](machine-after.json) preserve the boundary and identities.
[collect.py](collect.py) calls the existing micro harness, with a 120-second
collection budget and 30-second owned-service deadline per invocation. It uses
the unchanged [representative catalog](../2026-10-29-foundation/representative.json)
and [full catalog](../2026-10-29-foundation/full.json.gz); receipt hashes identify
the original uncompressed catalogs.
