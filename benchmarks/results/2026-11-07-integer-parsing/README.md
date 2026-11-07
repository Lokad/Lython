# Native general integer parsing — R45

2026-11-07. Baseline production `77703a42`, frozen core `b5656982` (.NET 11).
Final prototype `d3a6c778` / `0e1674bb` (.NET 11/10), precommit metadata `3480aa55`.
Exact source inventories, images, initial/refined variants and failures are archived.
R43 qualification covers source5fb25354; combined full R44/R45 delivery is pending.
Publication and final regex-adapter qualification remain independent/postponed.

## Complete replacement and correctness

General `int()` conversion reads the original UTF-8, using a native unsigned
accumulator before exact promotion. Reserve three conservative overlapping
magnitudes before the first major allocation. After promotion, native radix chunks
replace per-character BigInteger multiply/add. Commit only the retained result;
refund working storage after all users finish. Small scalar boxes retain existing
control/container headroom. Preserve the measured short-decimal path.

Delete ParsePythonIntegerText's UTF-16 loop, decoded CLR copy and decode estimate.
Use existing Unicode decimal classification for strings; bytes retain ASCII grammar.
No database, new scalar representation, owner/arena/ledger, independent allowance,
work quota, tracing, forced collection, retry or unsafe code. Cancellation checks
cover nonallocating scans, promotion and arithmetic chunks; individual CLR
BigInteger operations remain non-preemptible.

Two new admission-cancellation tests first fail on the baseline: parsing valid
21-digit text/bytes returns after cancellation instead of throwing. Five independent
CPython probes also expose rejected Arabic, full-width, astral and Unicode-zero
forms. The replacement corrects them. A frozen189-case CPython3.13.2 corpus covers
every radix2–36 around native promotion, larger negative chunks, prefixes,
underscores, Unicode digits and string/bytes whitespace/error distinctions.

Initial focused checks pass160internal/776public per actual target after one fixture
correction. That test assumed the removed decoded-copy charge; the operation now
fits its previous budget. Replace artificial remaining-room denial with controlled
refusal of actual magnitude construction after text work, reduce input50000→4096,
and retain/strengthen first-origin, exception state, alias funding and cleanup
assertions. No timeouts or containment checks are relaxed. The original failure,
old fixture and all logs remain.

Final review removes one immediately duplicated scan-entry check. Both actual
targets pass41internal/280public affected checks. Separate original-scale in-flight
Stress passes on both targets: a running native magnitude operation keeps its
reservation until cancellation unwinds and its worker is joined before disposal.
Final observed .NET11/10 test durations54/60ms are diagnostics, not latency promises.

## Affected workloads and allocation

These are explicit affected inputs, separate from ordinary catalog controls:

| Complete script | Input | Difference from ordinary fixture |
| --- | --- | --- |
| CSV aggregation | 4,096records /191,505B | Source unchanged; signed padded32-digit amounts with edge spaces, values preserved. |
| JSON aggregation | 4,096records /278,528B | Forty-digit numeric strings; helper explicitly calls int(record['amount']). |

CPython independently validates complete stdout and output files. Ordinary CSV,
text and JSON sources/inputs remain unchanged controls. Do not attribute ordinary
CSV cost to decoding: its short decimal values already use the native fast path.

The reused complete-job diagnostic uses isolated images sharing process GC/CoreLib,
normal GC/tiering, full public invocation/I/O/goldens/result cleanup. Initial
prototype process allocation falls **14.70% sync /13.35% async for padded CSV**
and **62.99% /62.98% for numeric-string JSON**. Ordinary controls differ by at most
0.007%; compilation allocation is flat. Final refinement changes one nonallocating
check; allocation was not replayed for that one-line change. Admitted root peaks
fall only250B/43B on affected inputs and match ordinary controls: the allocation
reduction is not a comparable reduction in retained storage, RSS or embedding tails.
Desktop clocks favor affected cases but include moving/adverse controls; all survive.

## Controlled quiet-VM comparison — B399

Quiet Azure Standard_F4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering, unchanged
worker7cf37ca5/adapter27211dbe/optional PythonRe0.4. Two old/two current processes,
five-second warmups per role/job and seven rotated~150ms rounds. Seven jobs finish
**196.49s**, below270s owner+30s stop+30s margin; observation follows the bound.
All Ready/image/golden/funding/quiet/Closed/exit/join/fixture/archive gates pass.

Warm commands include RAM host I/O, complete aggregation/goldens and prior-result
cleanup. Compile-to-result also includes compilation/program disposal. Startup,
staging/reset, transport and final cached teardown are excluded. This comparison
is between Lython revisions; it is not a new CPython timing baseline. Negative
change is faster; ranges retain every current/baseline process-median pairing.

| Complete job | Change | Identical current / baseline spread | Interpretation |
| --- | ---: | ---: | --- |
| Padded CSV warm | −13.41 to−12.49% |0.49% /0.56%|Demonstrated elapsed improvement|
| Padded CSV compile-to-result |−20.72 to−13.14%|2.64% /6.75%|Favorable; magnitude variable|
| Numeric-string JSON warm |−41.98 to−41.43%|0.34% /0.61%|Demonstrated elapsed improvement|
| Numeric-string JSON compile-to-result |−42.47 to−40.99%|0.54% /1.99%|Demonstrated elapsed improvement|
| Ordinary CSV warm |−0.46 to+0.89%|0.10% /1.26%|Control-limited|
| Ordinary text warm |−5.65 to+1.61%|4.83% /2.74%|Variable; no gain/parity claim|
| Ordinary JSON warm |−0.31 to+1.29%|0.97% /0.63%|Small possible cost/uncertainty|

Coarse process CPU agrees directionally for affected cases, but is not isolated
parser CPU. Retain the complete redesign, allocation reduction, correctness and
affected elapsed improvements. Keep fresh-CSV/control uncertainty and prior debt;
do not project these gains onto ordinary inputs or claim general Python parity.
No unchanged retiming. The substantial whole-script objective continues.

[Exact identity](identity.json), [independent fixtures](fixture-validation.json),
[initial allocation/adverse clocks](local-summary.json), [B399 summary](comparison-summary.json),
[raw source/gates](b399/observations.tgz), [development sources/images/checks](development.tar.xz).
B399 is terminal/archived and locally hash-verified (`3f988c52`); no workers or
guest fixtures remain. Never service-query again. Development images are evidence,
not release packages. Full platform/Stress/installed-package qualification is the
next coherent delivery boundary, followed by the representative CPython refresh.
