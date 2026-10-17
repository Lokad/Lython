# Complete CSV helper job through RunAsync — 2026-10-17 UTC

| Engine | Measured revision | Warm job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython | 1b6a15318b00ec056af02e23b72fa64f861964df | 10.346 ms | 2.03% |
| CPython | 3.13.16, PGO/LTO, ordinary GIL and cyclic GC | 3.756 ms | 0.51% |

Lython took nominally **2.755 times** the CPython time in this one short
diagnostic. This is not a qualified multiplier or confidence interval, and it
does not measure later HEADs. It demonstrates a remaining gap for this declared
boundary without attributing it to a particular runtime mechanism.

Separately, earlier Lython 5b546b2edbeaba45f8fe1b79be0b542fba0b5444 measured
10.379 ms with 1.30% spread. The candidate median is 0.31% lower, within the
observed spreads: no complete-job improvement or regression is established.
The change moves the existing awaited ForNext body into the main interpreter;
the separately recorded native loop diagnostic does not predict this job's gain.

The existing [script](../../scripts/csv-summary-helpers.py) parses each amount
through an annotated helper, filters and aggregates CSV rows, writes a sorted
report and prints the accepted count. The catalog size means **1,024 blocks /
4,096 records**, not 1,024 records. Input is 57,361 UTF-8 bytes, including quoted
newlines, accented and supplementary text; source is 599 bytes. Full
[stdout](expected-stdout.txt) and [report](expected-report.csv) goldens are retained
beside the exact [source](source.py) and [input](input.csv).

Boundary: compiled code is reused in warm workers, with fresh invocation state.
Lython calls public `RunAsync`; the comparison host's operations complete
synchronously. CPython runs the same ordinary script. Timers include public
dispatch/setup, previous-result disposal, input staging/output reset, execution,
capture/projection and complete output-file checks. Compilation, worker startup,
transport and response hashing are outside. This is neither fresh compile/run nor
real suspended-I/O throughput, embedding latency or guest async syntax. Do not
infer a change from the older synchronous/fresh CSV observations.

Lython retains ordinary public limits, cancellation checks and cooperative
ownership. The workers share an unbounded allowance within each process; prior
results are disposed before the next job, final results survive response writing,
and cached compilations are disposed at exit. CPython retains ordinary reference
counting and cyclic GC without equivalent in-process containment. This run has no
denied admission and does not qualify finite-budget streaming or memory usage.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, Linux
6.17.0-1022-azure, four AMD EPYC 9V45 vCPUs, approximately 16 GiB RAM, no swap.
Both clean sparse source checkouts were built in Release on that VM using
**SDK 10.0.401 / runtime 10.0.12**. Source `global.json` was unchanged; a separate
build-launch directory selected the pinned SDK. CPython 3.13.16 was built with
GCC 13.3.0, PGO/LTO, no debug/free-threaded mode or JIT, and launched with
`-I -S -X utf8`. Loaded binary identities, actual runtime settings and full build
logs are in [micro.json](micro.json), [preflight.json](preflight.json),
[preparation.json](preparation.json), [old build](build-old.log) and
[candidate build](build-candidate.log).

Only the supervisor sets `DOTNET_TieredCompilation=0`; worker launch removes it.
All recorded worker runtime overrides are null, ordinary GC/tiering remain in
effect, and BenchmarkDotNet is not loaded. Shared-lease load observations were
low; they do not constitute milestone noise qualification.

One existing `--compare micro --lane warm-async` capture used one-second warmup
per worker, two-second settling and seven rotating-order batches calibrated to
25 ms. Its collection deadline was 15 seconds. The shared VM lease and owned
systemd unit enforced `RuntimeMaxSec=30`, `TimeoutStopSec=0` and
`KillMode=control-group`. The complete owned run took **9.320 seconds** and exited
successfully. The exact [launch](launch.json), [runner](run.py),
[ownership receipt](ownership.json) and [terminal observation](terminal.json)
are preserved. No second timing capture or long lane ran.

All **37 responses / 519 invocations** preserve complete stdout and file goldens,
source/fixture/expected hashes, and successful status. Independent local Python
CSV aggregation reproduces the exact report and count; medians were recomputed
from raw batches. [Validation](validation.json) records those checks. All twelve
timing inputs retain their hashes; both source checkouts remain clean. All three
workers and fixture directories are gone, the service is inactive with an empty
control group, and the shared lease is reusable. See [cleanup](final-cleanup.json).
The [raw preparation/capture archive](raw-evidence.tar.gz) includes the unchanged
full catalog, scripts and receipts (SHA256
`1ebc0bddae74ace59f3554cc6759f6a98f336a41c90d0e8c0b86659f10d69783`).

No runtime, dependency, regex integration or benchmark framework change is part
of this evidence delivery. The measured runtime already passed local/package and
[Windows/Ubuntu CI](https://github.com/Lokad/Lython/actions/runs/39071019179).
