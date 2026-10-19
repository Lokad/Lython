# Native list concatenation: admission and copy correction

Revision `30998066` funds a fresh native-list concatenation through the active
execution owner. The old branch inherited an input governor (or no governor for
prepaid internal views), cloned the left backing, then snapshotted the right input
and could replace the just-created backing. The replacement admits one checked
final capacity and copies references with cancellation checks. Existing private
rollback refunds an unpublished destination; source charges never move or refund.

The real `difflib.SequenceMatcher.b2j` producer supplies the regression witness.
Its prepaid index lists have no individual governors. Before the correction,
retaining their concatenations could bypass admission. All four white-box controls
fail before and pass after; both public retained-copy controls likewise change
from unexpected success to terminal refusal. Independent semantic controls pass
before and after. Tests preserve cycles, aliases, source funding, separate backing
lifetimes, cancellation after admission, retained results and sequential reuse.
See [checks and process identities](checks.json). No test budget or timeout was relaxed.
Focused Release selections pass 60 white-box storage/ownership checks, 50 public
concat/repetition/provider/cancellation checks, 31 shared-contract controls and all
123 representative CSV/text/JSON/file-script checks.
The [original white-box](red-white.txt) and [public](red-public.txt) failures are
retained. An [independent CPython cycle/alias control](concat-control.py) produces
the same values as both public execution APIs; its [receipt](cpython-semantics.receipt.json)
pins the reference executable and completion.

One short comparison uses the existing micro tool and a single local catalog case:
construct two 16,384-item lists by repetition, concatenate them and print size and
endpoints. The independent golden is `32768 1 2`. Construction, execution/result
cleanup and golden checks are included; compilation, startup and transport are
excluded. Repetition and the rest of invocation setup are identical in both
Lython revisions. This measures the complete kernel, not the copy alone.

| Engine | Warm synchronous kernel | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `db86f36f` | 0.773 ms | 10.81% | Before; runtime source matches pre-change `9ef38d28` |
| Lython `30998066` | 0.776 ms | 16.80% | After; diagnostic only |
| CPython 3.13.2 | 0.044 ms | 4.55% | External context; diagnostic only |

The single capture is inconclusive: no demonstrated speedup, allocation reduction
or embedding-latency claim. The correctness fix is justified by the admission and
ownership witnesses, independently of timing. No repeat capture or full lane.

Measured 2026-10-19 UTC (2026-10-20 local), shared LOKAD-0399 Windows build 26200,
x64, 28 logical processors, .NET 10.0.13 with workstation GC. Seven alternating
rounds follow one-second warmup under an explicitly unbounded timing root and
without forced collection. The owned parent and all workers finish in 7.432 seconds.
The before worker was frozen before the change; [scope](scope.json), [baseline
identity](baseline.json), [catalog](catalog.json), [all samples and loaded
identities](micro.json) and [process receipt](process.receipt.json) are retained.

Delivery **30998066** is fully qualified by
[CI 39338126431](https://github.com/Lokad/Lython/actions/runs/39338126431): Windows
and Ubuntu Release builds, complete ordinary suites, original Stress and both
explicit Release package consumers pass. All required steps were inspected;
[pinned terminal gate proof](delivery.json) is cached permanently. Four status
queries were spaced at least three minutes apart. Later documentation/evidence
changes do not change runtime sources, tests, build or CI configuration. Publication
and further backend qualification remain deferred.
