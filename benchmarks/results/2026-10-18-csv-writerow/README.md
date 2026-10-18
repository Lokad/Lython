# Ordinary CSV writerow call transport, 2026-10-18 UTC

Ordinary one-positional native CSV writerow calls now use existing receiver and
argument operands. They avoid member wrappers and general binding, and share
the original row-conversion and publication bodies with exposed/general calls.
The evidence below measures allocation only. **Elapsed time, whole-job benefit
and CPython performance remain unmeasured for this change.**

Baseline: `75c03d0226c8d0aa5b242c41af118bb61e7814a6`. Candidate: that revision
plus the [six-file patch](candidate.patch), identified by canonical source hashes
in [validation](validation.json). It is not a measurement of an unspecified later
HEAD.

| Native interpreter, allocated bytes per 128 rows | Baseline | Candidate |
| --- | ---: | ---: |
| Ordinary call, synchronous | 153,488 | 149,144 |
| Ordinary call, async interpreter with completed callbacks | 190,104 | 158,360 |
| Exposed method, synchronous control | 153,464 | 153,464 |
| Exposed method, async interpreter control | 162,680 | 162,680 |

[Before](allocation-before.json) and [after](allocation-after.json) retain all
eight samples per case. Every sample within each case is identical. These byte
counts cannot establish whole-script latency, compilation cost, embedding impact
or a CPython ratio. Representative CSV aggregation scripts emit relatively few
summary rows, so a source-level call improvement does not establish their speedup.

The [temporary fixture](allocation-diagnostic.cs) executes a fixed lowered/
executable loop over 128 identical two-field rows. Eight warmups precede eight
samples. Context, governed source row and writer/callback setup stay outside the
thread-allocation counter. It includes executable dispatch, loop/call machinery,
row conversion/rendering and the completed native callback. The exposed method
is captured once inside the measured source as the unchanged control. There is
no actual host I/O. Call counts, UTF-8 byte totals, last complete output, retained
output ownership, absent in-memory history, frame cleanup and owner return to
zero are checked. Public API scheduling/setup, compilation and startup are
excluded; async callbacks do not suspend in this diagnostic. No GC was requested.
The fixture was removed before final Release builds.

The local development workstation is Windows build 26200, x64, Intel Core
i7-14700KF (20 cores / 28 logical processors). [Toolchain](dotnet-info.txt) records
the SDK/runtime; validation includes source/binary hashes and process receipts.
This uses no Azure VM, full lane, new framework or elapsed qualification campaign.

Only exact native CsvWriterObject receivers with one ordinary positional argument
use this path. Exposed methods, keywords/expanded calls, custom receivers and
DictWriter keep the general binder. Eligible calls no longer construct a bound
member or bind arguments into a fresh object array. Small raw call-argument arrays
were already pooled; the change removes their packaging without claiming they
previously allocated on every call. The old duplicate conversion lambdas and
unreachable fixed-signature body arity guard are removed. All routes share the
same conversion/rendering/history/callback code; those bodies remain unchanged.

The new guarded entry retains the same frame and two checkpoints, exception
translation and result observation. Custom row iteration and the captured output
callback can suspend, so async entry awaits conversion and publication before
releasing the call frame or inputs. Cell conversion still explicitly supports
native scalars only. This does not add custom cell stringification, alter host
capabilities or introduce a binder, dispatcher, cache, pool or ownership system.
Row/history/result preflights and owners remain; see [MEMORY](../../../MEMORY.md).

[Baseline](guard-baseline.trx) and [candidate](guard-candidate.trx) preserve 12
exact fuel/effect/error matrices through both APIs and ordinary/exposed forms.
They include successful rows, noniterable input and unsupported cells. Additional
checks cover direct source path and absent bound-member caches, refused conversion
before history/publication, rebinding the receiver during a suspended argument,
and retained rendered output during suspended/cancelled callbacks. Existing delayed
row iteration, cancellation, history/alias and complete-job tests remain intact.

Ten isolated [CPython 3.13.2 semantic references](cpython-oracle.json), launched
with `-I -S -X utf8`, cover Unicode/quoting, exposed/parenthesized/expanded calls,
wrong arity/keywords, custom property evaluation, receiver rebinding, arbitrary
write returns, iterator failure before publication and DictWriter fallback. The
public suite checks both execution APIs. These are semantic goldens, not timings.

Focused regressions and delivery qualification are recorded in validation.
No existing assertion, timeout or Stress scale was relaxed.
