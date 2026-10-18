# Shared native CSV conversion and bulk pipeline, 2026-10-18 UTC

Single and bulk writers now share native-row selection in the existing async
converter. Synchronous bulk calls reuse the raw-row helper; asynchronous bulk
publication stays inline to avoid another awaited frame. Custom/generator rows,
the outer loop and its checks remain. **Elapsed time and whole-job/CPython
performance are unmeasured for this change.**

Baseline: `6bec0f3487e4b644fce7600a5e9ddead6f76689f`. Candidate: that revision
plus the [one-file patch](candidate.patch), identified in [validation](validation.json).
It is not a measurement of an unspecified later HEAD.

| One native writerows call / 128 two-field rows, allocated bytes | Baseline | Candidate |
| --- | ---: | ---: |
| Native rows, synchronous | 144,568 | 144,568 |
| Native rows, completed asynchronous | 153,856 | 144,640 |
| Guest-generator rows, synchronous control | 144,568 | 144,568 |
| Guest-generator rows, completed asynchronous control | 154,880 | 154,880 |

The table shows medians. [Before](allocation-before.json) and
[after](allocation-after.json) retain every sample. Execution-step counts remain
646 for native rows and 2,310 for guest-generator rows in either mode. The async
native reduction is 9,216 bytes per batch, not an elapsed speedup or a qualified
CPython ratio. The remaining 72-byte sync/async batch difference is not profiled
or attributed here; no follow-up campaign is implied.

The [temporary fixture](allocation-diagnostic.cs) loads one native writerows
callable and prepares a governed list of 128 rows. Native rows share one existing
two-field list. Control rows are 128 fresh, one-use guest generators created by
an already compiled factory before the counter. Eight warmups precede eight
samples. Context, input/arguments, callable/writer/callback setup, factory
compilation/loading and generator creation stay outside the thread-allocation
counter. The counter includes binding, bulk outer iteration, row/generator
advancement, conversion/rendering and completed native callbacks. It excludes
the public invocation scheduler and outer executable loop, compilation/startup
and actual host I/O. It checks None, call counts, byte totals, last exact output,
retained output ownership, no history, scratch/frame cleanup and owner return to
zero. No GC was requested. The fixture was removed before final Release builds.

The workstation is Windows build 26200, x64, Intel Core i7-14700KF (20 cores /
28 logical processors). [Toolchain](dotnet-info.txt) records SDK/runtime;
validation retains source/assembly hashes and bounded process receipts. No VM,
full lane, new measurement framework or qualification campaign was introduced.

An initial [rejected patch](rejected-pipeline.patch) called the shared async
raw-row helper from the bulk loop. Completed callbacks looked favorable, but
one genuinely pending output callback revealed extra awaited-frame allocation:

| One pending row / synchronous-prefix allocated bytes | Baseline | Rejected | Final |
| --- | ---: | ---: | ---: |
| Native row | 2,248 | 2,328 | 2,176 |
| Guest-generator row | 2,256 | 2,408 | 2,256 |

[Before](suspension-before.json), [rejected](suspension-rejected.json) and
[final](suspension-after.json) retain all eight samples after eight warmups.
The [fixture](suspension-diagnostic.cs) measures only the initiating thread's
synchronous prefix through creation of the pending call. Setup, TCS creation,
release and awaited completion stay outside the counter. The callback receives
funded text, and completion/cleanup finish before owner disposal, including
fixture assertion failures. Candidate, baseline and final were captured in that
order. This does not measure total suspended-run allocation or continuation
allocation, and supplies no elapsed/embedding-latency conclusion.

The final implementation moves native list/tuple selection from the raw-row
helper into existing ToCsvRowAsync. Both single and bulk callers share that
selection; native rows use ToCsvRow, other inputs retain the same async loop.
Synchronous bulk calls reuse WriteRowFromValue. The two bulk async awaits stay
inline deliberately, avoiding the measured added frame. No rejected alternate
path, new method, converter, dispatcher, pool, cache, configuration or ownership
infrastructure is added. The outer acquisition
loop, 64-row check, stop-on-failure behavior, ignored callback returns and None
result stay unchanged. Rendering, scalar support, history, output callbacks,
funding and generic iteration remain on their previous implementations. DictWriter
and CSV format behavior are outside this patch. See [MEMORY](../../../MEMORY.md).

[Baseline](guard-baseline.trx) and [final](guard-candidate.trx) records preserve
20 exact bulk ordinary/exposed sync/async matrices, alongside 28 preceding native
and ordinary-call guards. The bulk cases use two/zero/65 rows, unsupported cells
and callback failures before later pulls. Exact final events and stable SHA-256
digests of checkpoint prefixes keep the wide fixture bounded without dropping
ordering checks. Only the test fixture computes those digests.

Existing regressions cover guest generators, delayed outer/inner row acquisition,
output cancellation, refusal before publication, retained history/results, shared
allowances and complete CSV/text/JSON/file jobs. Focused counts and delivery
qualification are recorded in validation. No assertion, timeout or Stress scale
was relaxed. There is no new CPython timing capture.
