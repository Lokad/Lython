# Reclamation-tier funding correction — 2026-10-10

Two memory accounting defects are fixed in `a3837821` and `c80ad2ab`. Exhaustion
relief can change a tier while its growth reservation is being funded. Re-check
the required capacity before insertion, return excess funding and release owned
reservations when funding fails. Budgets, entry fees and runtime defaults stay
the same.

The published baseline deterministically bills **96 bytes for 64 bytes** of
actual tier arrays after pruning. Ordinary registration, prefunded split-item
escape and both CSV reader modes exercise this transition. A pinned-entry
control still denies growth without publishing or leaking funding.

A separate reentrant promotion reproducer fails the published baseline with
**34 entries for 33 identities**, **256 uncharged bytes** of array backing and
**64 stranded reserved bytes** after denial. A nested relief sweep enlarged and
filled the destination while the outer promotion held stale funding. The fixed
denied insertion preserves 33 entries and exact backing charges; a funded retry
promotes the same identity once and leaves no reservation behind.

Six new white-box cases pass. The CSV cases deliberately force a tier transition;
they do **not** reproduce or establish the cause of the intermittent public C05
retained-row failure. That investigation remains open.

## Short performance guards

Six prospectively declared micros completed in **6.39–6.95 seconds** each,
under owned 30-second systemd process-group caps and zero stop timeout. No full
lane ran. These guard a correctness correction; they establish no speed gain
or certified CPython multiplier. Keep the second loop's **+5.26%** observation
alongside the first loop's **−0.20%**; do not recollect to seek a favorable result.

| Case | Repetition | Baseline µs | Candidate µs | Candidate latency change |
| --- | ---: | ---: | ---: | ---: |
| control.empty.control | 1 | 23.543 | 23.270 | -1.16% |
| loops.integer.large | 1 | 1629.367 | 1626.063 | -0.20% |
| strings.pipeline-ascii.medium | 1 | 139.271 | 141.006 | +1.25% |
| control.empty.control | 2 | 23.490 | 23.838 | +1.48% |
| loops.integer.large | 2 | 1616.443 | 1701.530 | +5.26% |
| strings.pipeline-ascii.medium | 2 | 140.538 | 138.422 | -1.51% |

Baseline producer `5be2e72befc3b22663b7e0002c6542f49489106c` has the same production tree as delivery
`85d78a21`. Candidate producer `c80ad2ab9e24bec6e6443237e20c89137bc75c42` matches the delivered runtime.
Its test tree precedes the test-only CI stabilization described below. Both
producers use SDK **10.0.401**, .NET **10.0.12**, default worker tiering,
workstation GC and no runtime overrides. The supervisor's tiering setting does
not propagate into workers. CPython **3.13.16** uses the pinned PGO/LTO build,
ordinary GIL/GC and isolated worker mode. All engines execute the same trusted
source/fixtures and independently verified output goldens; fourteen canonical
cases are unchanged. The adapter, worker and library identities are recorded
in [evidence.json](evidence.json).

Independent audits verify **262 responses**, invocation counts, all source/fixture/output
hashes, seven measured batches and two correctness verifications per engine per
receipt, and recompute **18 medians/IQRs**. [micro-results.csv](micro-results.csv)
retains every engine's median and spread, including CPython. These are short
diagnostics, with milestone qualification still pending.

## Validation

The original five-case tier test set gives four failures and one passing pinned
control; the separate promotion case also fails the unchanged published baseline.
The final source passes **85 focused checks in each of Debug and Release**,
**9,110 full Windows Debug checks** (1,360 white-box / 7,750 public), and
**1,360 white-box plus 1,033 selected public Linux Release checks**. Matching
probes were built before public checks. TRX and timing receipt hashes are in the
evidence manifest; immutable raw records remain in local agent notes.

All eight owned preparation/micro units are inactive with no main process, every
recorded worker is absent, producer files/libraries match their hashes, all three
producer checkouts are clean and the shared VM lease is free. The initial
overcharge-only candidate is retained as a superseded precursor; it has no timing
collection and is not presented as the final source.

## CI lifetime-test stabilization

The first delivery `590e6ef0` failed Windows CI run `38014099842` in
`AsyncFileRowRemaindersStayBounded`: the 100,000-row peak was 13,076,008 bytes,
versus 1,296,164 bytes for 10,000 rows. Ubuntu passed. This peak-ratio test lets
CLR collection timing affect the result. The terminal failure and its original
log are retained; that head was not rerun.

The test now collects at asynchronous host text-read boundaries. Its input,
10,000/100,000 row counts, default-budget case and 8× peak bound remain. An
additional **4 MiB** case requires abandoned remainders to reclaim before denial.
A private diagnostic disables registration only for three-cell remainder lists:
the ratio-only case still passes, while the capped case fails with the expected
memory-budget denial. This demonstrates why the stronger case is useful.

The follow-up changes only tests and their host fixture. The runtime remains
the frozen measured candidate; all six timing receipts are unchanged and no
new performance collection is needed. The manifest records the follow-up checks
and fault patch. The original intermittent C05 cause remains unproven.
