# Native rows in async CSV writerow, 2026-10-18 UTC

Async writerow now selects the existing synchronous scalar converter for exact
native lists/tuples, while continuing to await publication and custom/generator
row protocols. This removes unnecessary async iteration wrappers from that CPU
path. **Elapsed time and whole-job/CPython speedup are unmeasured.**

Baseline: `bca120a4a1cad064d81a7bffe0b83f68bcf93042`. Candidate: that revision
plus the [one-file patch](candidate.patch), with source/binary identities in
[validation](validation.json). This is a fixed diagnostic source comparison,
not a measurement of an unspecified later HEAD.

| Native helper phase / allocated bytes per 128 rows | Baseline | Candidate |
| --- | ---: | ---: |
| Conversion only, sync control | 46,080 | 46,080 |
| General async conversion, unchanged control | 55,296 | 55,296 |
| Publication only, synchronous | 98,304 | 98,304 |
| Publication only, completed asynchronous | 98,304 | 98,304 |
| Shared raw-row helper, synchronous | 144,384 | 144,384 |
| Shared raw-row helper, completed asynchronous | 153,600 | 144,384 |

The isolated baseline places the 9,216-byte difference in conversion, rather
than publication. The candidate's shared async helper avoids the general async
converter for this native input. That generic converter remains necessary for
other inputs and its control deliberately stays unchanged. No unrelated iterator
implementation or ownership machinery is removed. This change does not select
native conversion inside writerows or DictWriter.

The table uses medians; [before](allocation-before.json) and
[after](allocation-after.json) retain every sample, including one extra 128-byte
baseline synchronous-helper sample. All execution-step counts are unchanged:
384 conversion, 256 publication and 640 shared-helper steps per 128 calls.
These allocations do not establish interpreter, public invocation, complete-job
latency, embedding impact, compilation/startup cost or a CPython ratio.

The [temporary fixture](allocation-diagnostic.cs) separates the existing
converter, publication helper and actual shared raw-row helper on one fixed
two-string native list. It prepares a delegate to the private general converter
outside the counter; that managed test access changes no production visibility.
Eight warmups precede eight samples. Context, funded input, writer/callback and
preconverted control-row setup are outside the thread-allocation counter. It
includes only 128 native helper operations; callbacks complete synchronously and
perform no actual host I/O. Output byte totals, borrowed cell identity, retained
output funding, scratch release, step counts, no history and owner cleanup to
zero are checked. No GC was requested. The fixture was removed before final
Release builds. This uses no new benchmark framework or VM campaign.

The development workstation is Windows build 26200, x64, Intel Core i7-14700KF
(20 cores / 28 logical processors). [Toolchain](dotnet-info.txt) records the
SDK/runtime; validation retains assembly/source hashes and bounded process receipts.

The exact native classes cannot override iteration, and CSV cell conversion
supports native scalars with explicit rejection of unsupported cells. Thus the
native converter cannot suspend. The captured writer callback still can: the
existing WriteRowAsync remains awaited, retaining its inputs/frame and all
existing admission/cleanup behavior. Generic custom/generator rows keep the
async converter. The converter, scalar formatting, history, rendering, callback
and ownership bodies remain unchanged. No new decoder, buffer, cache, allowance,
dispatcher or lifetime infrastructure is introduced. See [MEMORY](../../../MEMORY.md).

[Baseline guards](guard-baseline.trx) and [final guards](guard-candidate.trx)
preserve 16 exact native list/tuple/empty/wide/error matrices and the previous 12
ordinary/exposed call matrices. Wide/error rows cross the 64-move checkpoint
boundary. Both execution APIs preserve spans, effects, failure/publication order
and unwinding. Existing tests cover suspended custom row acquisition, output
callbacks/cancellation, shared allowances, retained history/results and refusal
before publication, plus complete CSV/text/JSON/file jobs and managed stack safety.

The existing ten [CPython semantic references](../2026-10-18-csv-writerow/cpython-oracle.json)
are reused by the public regressions. There is no new CPython timing capture.
Validation records focused checks; platform/package qualification is batched at
delivery. No existing assertion, timeout or Stress scale was relaxed.
