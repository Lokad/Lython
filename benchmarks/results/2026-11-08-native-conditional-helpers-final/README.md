# Native conditional helper returns: retained refinement

The existing native-return proof now covers acyclic guarded returns. It retains
source indices, proves types before effects and evaluates only the selected branch.
Shared joins, loops, custom truth and data-query format guards use ordinary
executable dispatch. No second engine, control carrier, arena or public option.
Two immutable 32-bit node fields and compiler-only path scratch are explicit costs.

Baseline: fully qualified `a7bd4103`, core `aae80cc7`. Retained source: four-file
patch over `dee0ef1b`, core `1aa68ce9`; exact inventory and binary are archived.
The empty-list branch borrows the unchanged literal-list factory through a cold
non-inlined helper, preserving fresh values, preflight, cancellation and refunds.
The [initial prototype](../2026-11-08-native-conditional-helpers/README.md) and all
its adverse observations remain separate. Final platform qualification is pending;
the primary CPython guide still describes source `15c06863`.

| Complete script / phase | Elapsed change, four median pairings | Coarse CPU change |
| --- | ---: | ---: |
| Guarded text helper, warm sync | −13.42 to −16.75% | −13.51 to −16.00% |
| Guarded text helper, warm async API | −14.63 to −16.20% | −9.15 to −15.94% |
| Ordinary rsplit helper, warm control | −0.55 to −4.21% | −1.22 to −3.66% |
| CSV compilation control | +0.56 to +1.48% | −0.45 to +3.73% |
| CSV warm control | −0.12 to −1.92% | 0 to −2.22% |
| JSON warm control | −2.94 to +3.36% | −2.50 to +5.13% |

Guarded baseline spreads: 2.05% sync / 0.31% async; candidate 1.91% / 1.55%.
Short observations support a workload-specific CPU gain, not confidence intervals
or a universal speedup. The initial stable CSV regression does not recur here;
its cause remains unresolved. Controls show no demonstrated new material debt;
JSON is inconclusive with 4.92% baseline spread. Earlier independent regression
debts are not silently repaid.

Warm allocation is neutral (less than 0.02% diagnostic differences). Original
compiler allocation +0.08–0.53% remains; compiler source was unchanged by this
construction refinement. Windows compilation timing remains uncertain: the initial
CSV +74–79% did not recur in changed-order confirmation, but other compiler controls
were adverse/variable. All initial observations and failed two-case driver invocation
are preserved. Quiet Ubuntu does not establish a Windows compiler improvement.
Desktop elapsed remains diagnostic; refined async old-control spread reached 32.09%.

205 public / 42 internal focused Release checks pass per actual .NET11/.NET10.
Independent Python goldens cover nested/implicit returns, fresh aliases, changing
builtins, formatting, unselected errors, local forwarding and ordinary joins/loops.
Real asynchronous truth, in-flight cancellation, terminal refusal, selected error
spans, caller exception restoration, retained funding and budget reuse are covered.

Same unchanged scripts, full goldens and workers as the initial comparison:
guarded/rsplit text 3,072 lines / 35,840 B, CSV 4,096 records / 57,361 B,
JSON 4,096 records / 110,592 B. Azure F4as_v7 / Ubuntu24.04.4, four EPYC9V45
vCPUs, 16GiB/no swap, actual .NET11 RC3, normal GC/tiering, optional0.4 adapter.
Five-second warmup per role/job, two independent copies/core, seven rotated150ms
rounds; six jobs168.190s. Warm command-v2 includes execution, mediated RAM-file I/O,
full validation and prior-result cleanup. Compilation includes construction/disposal.
Startup, staging and final cached teardown are excluded; no pending-I/O/RSS/tail claim.

All source/core/auxiliary/Ready/golden/quiet/Closed/exit/join/fixture/archive gates
pass. B465 is terminal, all users joined, RAM unmounted, archive verified locally.
No evidence was deleted. The frozen binary predates its commit; inventory is exact.

[Identity](identity.json), [controlled comparison](controlled-comparison.json),
[allocation/local diagnostic](allocation-and-local.json), [status](status.json),
[source patch, binary, tests, logs, raw observations](observations.tar.xz).
