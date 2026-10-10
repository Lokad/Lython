# Streaming reservation progress

A successful temporary reservation now permits the next allocation to collect
remaining garbage, even when no commit happened between them. This corrects a
collection-sensitive failure of the existing 100-line streaming fixture at its
unchanged **64 KiB** limit. Three new governor regressions, focused Debug/Release
checks and all **9,155 Debug tests** pass. This is a correctness change, with no
performance claim or full benchmark lane.

## Controlled reproduction

The original public test scans 100 lines of 999 ASCII bytes plus newline from a
100,000-byte file, discards prior lines and returns 100,000. It uses ordinary
runtime defaults, a synchronous host and a 65,536-byte execution budget; both
`Run` and `RunAsync` exercise the same guest source.

An initial, separately bounded diagnostic declares 24 collection schedules in
advance and tests both execution modes: no scheduled collection, or one collection
at a chosen emission boundary. It captures reservation stacks and numeric pool
snapshots without retaining targets; weak-target observations are isolated in
methods that cannot inline. Both modes fail when collection occurs after emission
29. The other 46 invocations finish successfully. The full diagnostic process
takes 1.72 seconds under an external 30-second process-group bound.

Removing all tracing and pool observations preserves the failure. These smaller
diagnostics retain only the conditional collection-schedule hook in a copied
reader. The governor and pool are unchanged, and the same script, input, limit
and defaults apply. Baseline Debug and Release each fail both modes at boundary
29 and pass the adjacent 28/30 boundaries and unscheduled controls. Repeating the
declared boundary cases on the exact original namespace source produces the same
failure in Debug. Its isolated worktree stays untouched and clean.

| Source / configuration | No scheduled collection | Boundary 28 | Boundary 29 | Boundary 30 | Whole diagnostic process |
|---|---|---|---|---|---:|
| Delivered baseline / Debug | Both pass | Both pass | Both fail | Both pass | 0.387 s |
| Delivered baseline / Release | Both pass | Both pass | Both fail | Both pass | 0.343 s |
| Original namespace candidate / Debug | Both pass | Both pass | Both fail | Both pass | 0.367 s |
| Corrected source / Debug | Both pass | Both pass | Both pass | Both pass | 0.651 s |
| Corrected source / Release | Both pass | Both pass | Both pass | Both pass | 0.464 s |

All successful untraced invocations explicitly check the complete returned value
100,000. Failed cases deny 1,128 bytes, with accounted peak 65,272. Every group has
eight fixed invocations and an external 30-second bound; there is no failed-case
retry. These process durations measure neither throughput nor invocation latency.
The original full-suite failure had no reservation/reference trace, so its exact
historical collection chronology cannot be recovered from the old result. The
controlled original-source reproduction establishes a concrete defect in that
fixture; it does not retrospectively add observations to the historical receipt.

## Denial mechanism and correction

The captured failure reaches a multi-window line after 49 emissions:

1. Accounted committed memory is 65,008 bytes. `EmitAccumulated` requests 4,032
   bytes of temporary builder scratch, so the governor attempts relief.
2. A partial drain refunds three previously collected line entries, 3,768 bytes.
   Committed memory falls to 61,240, enough for the pending scratch. Relief returns
   without collecting the 22 remaining pool entries (27,632 bytes of charges).
3. The scratch reservation succeeds, bringing accounted memory to 65,272. The
   following fresh line asks for 1,128 bytes: a total of 66,400, above the cap.
4. The old progress flag records commits only. Because scratch has reserved but
   not committed, the second request skips collection and raises `MemoryError`.

The governor now records a successful positive reservation as allocation progress,
alongside commits and prefunded publication. The next pressure event can collect
what a partial drain left behind. The correction adds one progress update to
`Reserve`; it changes no charges, ownership transfers, limits, window sizes,
cooperative checkpoints or host mediation.

Zero-byte reservations and capacity checks alone do not manufacture progress.
Repeated pinned denials without new allocation still fail fast. The existing
reentrancy guard still prevents nested relief, and bookkeeping performed inside
relief cannot trigger another attempt for the same pending allocation.

## Source and validation

Corrected source `fd58dde1b3f5272ae62853c0174c6ed4ace24200` has production tree
`121846918199fcf4df8f237ec15bc7fa2894affe`, tests
`57a0eb7a10273f0bf402f5806948241a904c56bb` and unchanged comparison tree
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. Delivered baseline is
`35793a9e0671d777a9dde4c31887944d177ad945`; original namespace source is
`8d20133d1c8e8a077879e2c830993c103b84c8ae`. Local SDK is
10.0.300-preview.0.26177.108, CLR 10.0.12, with ordinary runtime/GC options.

Three deterministic white regressions fail before the correction in both Debug
and Release. Direct and temporary scratch paths must collect remaining garbage
before a following reservation, while a pinned target must receive one relief
opportunity and then keep repeated denials cheap. Exact charges/reservations,
peak limit, pool retirement and denial attribution are checked. The tests use the
existing exclusive allocation-sensitive collection to prevent sibling collections
from changing their intentionally precollected/uncollected state.

Focused white Debug and Release each pass 84 checks. After matching probe builds,
39 selected public Release checks and all 9,155 frozen Debug checks pass (1,402
white / 7,753 public), including the unchanged public streaming fixture and retained
line denial controls. A literal shared TRX filename in the full command would
overwrite the first suite's receipt; both complete receipts are copied and hashed
before overwrite, with their original logger output retained. No test is rerun.

The independent [evidence audit](evidence.json) verifies all fixed schedule/mode
rows, explicit successful values, denied requests/peaks, captured reservation and
pool arithmetic, source trees and TRX counters/hashes. Raw sources, binaries,
declarations, traces and process/receipt cleanup remain private under
`.git/agent-notes/streaming-relief-progress-20261010/`. No VM collection, interpreter
override, normal timing sample or full lane is used. No Lython/CPython ratio is
claimed. The held namespace optimization still requires validation on a new
combined source; original CSV and pipeline causes remain separate investigations.
