# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/native helpers |4,096 records /57,361 B|4.401–4.454 ms|3.459 ms|1.27–1.29×|13d69be6; RAM; Diagnostic;1.19% control /1.24% IQR|
| CSV summary/ordinary helper |4,096 records /57,361 B|4.863–4.911 ms|3.410 ms|1.43–1.44×|13d69be6; RAM; Diagnostic;0.99% control /1.53% IQR|
| Text summary/helpers |3,072 lines /35,840 B|2.411–2.497 ms|1.275 ms|1.89–1.96×|13d69be6; RAM; Variable;3.46% control /1.06% IQR|
| JSON-lines/helpers |4,096 records /110,592 B|4.856–4.930 ms|4.552 ms|1.07–1.08×|13d69be6; RAM; Diagnostic;1.49% control /0.95% IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|3.643–3.681 ms|2.512 ms|1.45–1.47×|13d69be6; RAM; Variable;1.06% control /3.51% IQR|
| Config read/edit/write |1,024 paths /7,177 B|0.263–0.267 ms|0.257 ms|1.03–1.04×|13d69be6; RAM; Variable;1.58% control /2.01% IQR|
| CSV long fields |16 records /524,717 B|0.347–0.348 ms|2.449 ms|0.14×|13d69be6; RAM; Variable;0.35% control /3.05% IQR|
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS; Diagnostic; 2.7% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS; Variable; 3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS; Diagnostic; 1.5% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

First seven rows: **2026-11-05**, retained0.11development source **13d69be6**,
**CPython3.13.16**, **.NET11RC3**. They measure **compilation plus one complete
synchronous execution in an already-running host**, including mediated RAM-file
I/O, full validation and prior-result cleanup; fixture staging/reset is excluded.
**Ratio=Lython elapsed/CPython elapsed; above one means slower.**

Ranges are medians from two independent identical-build processes, not confidence
intervals. Control=their relative spread; IQR=interquartile range/median, showing
the larger Lython-role IQR. Variable/inconclusive cases remain visible; small
differences are diagnostic, not portable guarantees. Text is still about2×CPython,
report about1.5×; long-field CSV does not describe ordinary CSV.

Exact frozen core **ec4b6378** (built before commit metadata), source/fixture hashes,
raw samples and checks: [retained checkpoint](../benchmarks/results/2026-11-05-runtime-cpython-refresh/README.md).
Full local4143internal/16004public pass per actual.NET11/10. Source13d platform/
original stress/installed-consumer [CI42074585232](https://github.com/Lokad/Lython/actions/runs/42074585232)
is GREEN on Windows/Ubuntu, actual11/10, original stress and installed consumers.
The [terminal receipt](../benchmarks/results/2026-11-05-frame-exit-cleanup/delivery-ci-status.json)
is cached; no unchanged replay. Publication remains postponed.

Four earlier file rows measure **2026-11-03/bd3f9dee**, ordinary filesystem, and
are not updated-source results. Filename scripts filter768 supplied names/write256
selections; they do not traverse768files. Earlier90785737 search remains inconclusive
(fresh270%IQR/218% control drift, warm138%/77%): [historical evidence](../benchmarks/HISTORY.md).

## Warm execution, compilation and startup

Same13d source and invocation/I/O/validation/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/native helpers |4.202–4.310 ms|3.299 ms|1.27–1.31×|Variable;2.57% control /0.97% IQR|
| CSV summary/ordinary helper |4.605–4.720 ms|3.219 ms|1.43–1.47×|Variable;2.45% control /2.20% IQR|
| Text summary/helpers |2.311–2.322 ms|1.199 ms|1.93–1.94×|Variable;0.48% control /3.47% IQR|
| JSON-lines/helpers |4.707–4.826 ms|4.350 ms|1.08–1.11×|Variable;2.47% control /0.83% IQR|
| JSON-to-Markdown report |3.466–3.491 ms|2.345 ms|1.48–1.49×|Variable;0.71% control /3.49% IQR|
| Config read/edit/write |0.195–0.196 ms|0.193 ms|1.01–1.02×|Variable;0.75% control /4.78% IQR|
| CSV long fields |0.204–0.205 ms|2.299 ms|0.09×|Diagnostic;0.51% control /1.85% IQR|

Warm text/report/config dispersion limits small claims. Do not subtract warm and
fresh medians to infer compilation. Compilation/disposal alone, execution/I/O excluded:

| Compile-only script | Lython | CPython | Ratio | Status |
| --- | ---: | ---: | ---: | --- |
| CSV |69.920–71.692µs|54.857µs|1.27–1.31×|Variable;2.47% control /4.07% IQR|
| Report |50.048–50.778µs|37.522µs|1.33–1.35×|Variable;1.46% control /3.36% IQR|

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain unmeasured.**

## Machine and included work

Dedicated Azure **Standard_F4as_v7**, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB RAM/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, launched `-I -S -X utf8`. SDK/runtime:
**11.0.100-rc.3.26522.148 /11.0.0-rc.3.26522.148**. Independent sequential processes,
normal worker GC/tiering, quiet endpoints and unchanged controls.16jobs completed
in440.25s within480s target/540s owned bound; all workers joined/final reservations zero.

Both clocks include invocation, actual guest I/O, complete stdout/file reads/checks
and prior-result disposal. Fresh Lython also disposes each per-invocation compilation.
Staging/reset is outside both clocks but inside wall deadlines. Final result,
cached programs and remaining owners stay funded through delivery, then dispose
outside that batch timer. Startup/transport/response hashing/final cache-process-
fixture teardown excluded. Fresh globals each run; CPython module caches persist,
Lython execution caches are isolated. The host supplies efficient byte-file I/O.
Containment, Python conversion and host work are included; this is not dispatch-only.

## Improvements against earlier Lython

Separate source-pair observations; not CPython ratios, additive gains or per-change attribution:

| Measured change | Result | Limits and evidence |
| --- | --- | --- |
| General result transport, after a637 | Basicloop elapsed−2.45/3.02%;JSON−.93/1.98% | [Source pair](../benchmarks/results/2026-11-05-ordinary-result-transport/README.md). Possible small CSV debt; text disagreement and variable report. Initial larger gains were not reproduced consistently. Not included in the representative tables; afc3189e fully qualified on Windows/Ubuntu, both runtimes, original Stress and installed consumers. |
| Local augmented-update transport, after13d | Basicloop elapsed−16.05/16.54% | [Source pair](../benchmarks/results/2026-11-05-local-augmented-update/README.md). CSV confirmation+0.77/+0.99% possible debt; initial adverse/control spread and report uncertainty retained. Not included in representative tables above; a63708a7 fully qualified on Windows/Ubuntu, both runtimes, original Stress and installed consumers. |
| Retained13d group versus9c1d | Warm ordinaryCSV−6.5–8.8%,text−9.1–9.5%;freshJSON−7.0–8.4% | [Same checkpoint](../benchmarks/results/2026-11-05-runtime-cpython-refresh/README.md). Fresh report+.65/1.72% adverse/variable; config/compile small or inconclusive. Older debts remain. |
| Funded function workspace | OrdinaryCSV process allocation−6.67/6.69% | [Qualified source pair](../benchmarks/results/2026-11-05-owned-function-workspace/README.md). Elapsed mixed, admitted peak+208B, cleanup boundaries differ. |
| Native helper returns | CSV/JSON allocation−19.9/19.3% | [Historical qualified pair](../benchmarks/results/2026-11-02-native-function-returns/README.md). Original elapsed diagnostic. |
| DirectUTF8 formatting/storage | Allocation−8.67%,controlled elapsed/CPU−5–8% | [Historical confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). Original adverse15.125ms remains unexplained. |

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves other improvements,
rejected designs, all adverse observations and qualification details. The
[previous guide snapshot](../benchmarks/results/2026-11-05-runtime-cpython-refresh/previous-guide.md)
and [earlier checkpoint](../benchmarks/results/2026-11-05-current-cpython-checkpoint/README.md)
retain their measured revisions; the snapshot's relative links use its original docs location.
[Comparison procedures](../benchmarks/COMPARISON.md) document the existing tooling.
CPython remains the primary external baseline; substantial text/report gaps remain.
