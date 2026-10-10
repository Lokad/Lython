# CSV producer ownership correction — 2026-10-10

Commit `d4a255fb` corrects unpublished CSV allocations and partial-row scratch
ownership. Registration denial now refunds a fresh field's **129 bytes** and
a fresh row's **192 bytes** of committed storage. Empty physical lines publish
independently tracked governed rows.

Partial row capacity was charged twice, and abandoned parsers could strand a
separate, unregistered row reservation. Row scratch now shares the source's
registered field reservation. Controlled partial inputs use **96 rather than
160 bytes**, and **160 rather than 288 bytes**, respectively. Abandonment returns
the previously stranded **128 bytes**. Replacement capacity is funded beside
the old array before allocation; only then is the old funding returned. A
reservation cannot release another reservation's funding. No new parser fields,
parent links, budget changes or entry-fee reductions are introduced.

Seven regression cases fail the unchanged published production source in both
Debug and Release. Those seven and two additional denial/retry and reservation
isolation controls pass the corrected source. These controlled transitions do
not establish the cause of the original intermittent public retained-row C05
failure; that investigation remains open.

## Short performance observations

Ten prospectively declared micro collections took
**6.46–7.07 seconds** each, with owned
**30-second** process-group caps and zero stop timeout. No full lane ran.
Retained-output CSV latency increased **8.82%/2.65%**; scalar CSV varied from
**+6.96% to −6.03%**, with broad within-collection spread. These observations
establish no speed gain. All results are retained, with no favorable recollection.

| Case | Repetition | Baseline µs | Candidate µs | Candidate latency change |
| --- | ---: | ---: | ---: | ---: |
| control.empty.control | 1 | 23.626 | 23.699 | +0.31% |
| loops.integer.large | 1 | 1637.635 | 1650.781 | +0.80% |
| strings.pipeline-ascii.medium | 1 | 139.125 | 136.722 | -1.73% |
| csv.retain.medium | 1 | 487.082 | 530.062 | +8.82% |
| diagnostic.csv.scalar-count.medium | 1 | 237.383 | 253.895 | +6.96% |
| control.empty.control | 2 | 23.412 | 23.402 | -0.04% |
| loops.integer.large | 2 | 1627.033 | 1622.101 | -0.30% |
| strings.pipeline-ascii.medium | 2 | 137.641 | 136.777 | -0.63% |
| csv.retain.medium | 2 | 503.444 | 516.765 | +2.65% |
| diagnostic.csv.scalar-count.medium | 2 | 233.646 | 219.553 | -6.03% |

The scalar diagnostic uses the same quoted Unicode CSV fixture as the retained
case, including its header, and prints only the **129-row** count. Its golden
was independently checked under pinned CPython before timing. The retained
case includes projection and printing of 128 dictionaries; their costs remain
inside its timed work. A count alone does not validate cell contents; the wider
CSV tests cover those semantics. All sixteen prior catalog cases are unchanged.

Baseline producer `c80ad2ab9e24bec6e6443237e20c89137bc75c42` has the same production tree as published
delivery `99aea50a`; its earlier test tree does not affect timed production.
Candidate `d4a255fb0bb5f67d48993ee57c451f9e5ce4bab6` matches the delivered production and test trees.
Both use SDK **10.0.401**, .NET **10.0.12**, default worker tiering, workstation
GC and no runtime overrides. CPython **3.13.16** is the pinned PGO/LTO build
with ordinary GIL/GC and isolated mode. Each engine executes identical trusted
source and fixtures with verified output hashes. This is a short diagnostic,
with no certified CPython multiplier or milestone qualification.

Independent auditing checked **437 responses**, completed invocation counts,
source/fixture/golden hashes, seven measured batches and two correctness
verifications per engine per collection, and recomputed **30 medians/IQRs**.
[micro-results.csv](micro-results.csv) retains all engines and their spreads;
[evidence.json](evidence.json) records producer, test and receipt hashes.

## Validation and cleanup

The corrected source passes **71 focused checks in each of Debug and Release**,
**9,120 full Windows Debug checks** (1,369 white-box / 7,751 public), and
**1,369 white-box plus 577 selected public Linux Release checks**. Matching
probes were explicitly built before public checks. Early invalid fixture
attempts are preserved and excluded from the qualifying regression evidence.

All eleven owned preparation/micro services are inactive with no main process,
every recorded worker is absent, all recorded producer/input files match their
hashes, both frozen checkouts are clean and the shared VM lease is free.
Routine rounds continue with seconds-long micros. Full comparison lanes remain
reserved for infrequent declared milestones, with a hard ten-minute lane cap.
