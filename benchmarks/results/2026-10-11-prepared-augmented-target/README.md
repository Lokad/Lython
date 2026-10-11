# Prepared augmented assignment targets — 2026-10-11

**Rejected; the prototype remains isolated.** The decision uses the unchanged short-round rule: method gains
must exceed max(2%, retained spread) in both passes, with no repeated material
control regression and no repeated complete-job allocation growth above 0.5%.
All observations are retained; there is no favorable recollection or full lane.

The preceding method profiles identified capturing store delegates in augmented
assignment preparation as a concrete allocation lead. The prototype retains the
actual context, target syntax, receiver and raw keys in a readonly value target.
It removes target records and store closures from AST, lowered synchronous and
lowered asynchronous paths. Stores still resolve current descriptors after the
RHS, and async getters, RHS calls and stores still await actual guest callbacks.
Built-in async slicing also avoids a slice-key allocation that its store never
used. Guest-visible slice keys retain their original governed representation.

## Timing and controls

Fourteen paired micros use the existing seven canonical cases, twice each, with
complete output checks. Whole owned groups, including cleanup, take
8.038–8.049 seconds under 30-second caps. Negative changes mean
less time; spread is the larger retained IQR/median of the two Lython producers.
CPython participates with the same inputs and complete output checks. These are
diagnostic old/new results, without a milestone-qualified Python multiplier.

| Case | Pass 1 time change | Pass 2 time change | Retained spread, passes 1 / 2 |
| --- | ---: | ---: | ---: |
| `calls.keyword.medium` | -0.95% | +0.87% | 2.90% / 0.51% |
| `calls.method.medium` | -0.81% | -3.08% | 1.52% / 0.99% |
| `calls.positional.medium` | +0.48% | -2.87% | 1.02% / 1.16% |
| `control.empty.control` | +3.93% | -1.26% | 5.10% / 4.02% |
| `lists.stable-sort.medium` | +2.67% | -1.10% | 7.90% / 5.63% |
| `loops.integer.large` | +0.19% | +4.56% | 1.44% / 0.94% |
| `strings.pipeline-ascii.medium` | +0.01% | -6.41% | 2.56% / 2.23% |

The first method pass misses the 2% floor, so the prototype is rejected. No
control shows a repeated material timing or allocation regression. The 23–25%
reduction in method-job allocations does not override the declared CPU rule.

## Allocation and native diagnostics

Separate fixed-count allocation and native-code groups take
4.031–4.041 seconds each. Allocation is process-wide
managed allocation over complete jobs, with two 100-invocation passes per case
after 16 warmups, ordinary GC/tiering and no forced collection. It is not exact
per-guest-type accounting. Positive savings mean fewer managed bytes.

| Case | Saved bytes/job, passes 1 / 2 | Saved fraction, passes 1 / 2 |
| --- | ---: | ---: |
| `calls.keyword.medium` | 0.00 / -103.36 | 0.00% / -0.02% |
| `calls.method.medium` | 432,118.00 / 442,437.36 | 23.43% / 24.75% |
| `calls.positional.medium` | 0.00 / -1.12 | 0.00% / -0.00% |
| `control.empty.control` | 0.00 / 0.00 | 0.00% / 0.00% |
| `lists.stable-sort.medium` | -7,570.56 / 0.00 | -0.22% / 0.00% |
| `loops.integer.large` | 0.00 / 0.00 | 0.00% / 0.00% |
| `strings.pipeline-ascii.medium` | 0.00 / 0.00 | 0.00% / 0.00% |

Every emitted native body, tier, inline summary and call target is retained;
selected augmented-target bodies are included in [the evidence](evidence.json).
Code size is not used to predict CPU gains. Logical fees and checkpoint order
remain those of the corrected baseline.

## Independent compatibility baseline and validation

Before the prototype, independent probes found missing synchronous user-slice
augmentation and async slice keys escaping governed retention. Both producers
include the same correction. Seven of the original 18 public cases failed on
the old runtime; all corrected cases and the two new budget cases pass. A
400-key retention probe previously succeeded asynchronously under a 32 KiB
budget and now raises MemoryError in both modes.

Twenty-two direct class-factory checks pin actual ownership, get/RHS/store
suspension, same-run reentry, cancellation cleanup, every fuel boundary, exact
source spans and logical fees. Twenty public checks cover raw-bound and receiver
retention, mutable keys, live descriptor replacement, slicing and budgets. The
prototype also passes all 23 preceding class-method boundary checks.

All 18 normative local CPython 3.13.2 comparisons match. The earlier temporary
two-bound slice identity observations are preserved separately: CPython's
[bytecode is an implementation detail](https://docs.python.org/3.13/library/dis.html#opcode-BINARY_SLICE).
Raw bounds and evaluation order follow
[the augmented-assignment language contract](https://docs.python.org/3.13/reference/simple_stmts.html#augmented-assignment-statements).
A separate pre-existing generator probe, `target[::1] += yield 2`, recreates its
explicit-step user slice key between get and store in both modes, unlike CPython.
Its retained probe is a follow-up bug, together with an audit of that path's key
governance. This change does not modify the generator path.

Baseline Windows validation consists of the full 9,462 checks followed by its
two newly added budget checks. Candidate Windows Debug and both fresh Linux
Release producers each pass all 9,464 checks (1,619 white / 7,845 public), with
matching probes built before probe-dependent gates. Each Linux producer also
passes seven complete canonical comparisons against CPython 3.13.16 before
timing. The ordinary worker uses SDK 10.0.401 / CLR 10.0.12, workstation Interactive
GC and default tiering/PGO, without worker overrides. Python uses isolated normal
GIL/GC defaults. Builds, tests and diagnostics occur outside normal timing.

Baseline: `f77d60d1b173c2cead329014178bb337f526009c` / production `f006f62cad79f63bc1b2564494ce742b6fe74000`.
Candidate: `8501f02c825e3dbdee9d898903483981aeb17159` / production `264fe4372001ed71082e3b7c172973211b613ebb`.
Tests: `32b6163661305c9e1e10979f5b0b9656839ff948`; harness: `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

All 20 owned services are terminal; all
66 recorded PIDs are absent, both producers are clean,
inputs are rehashed and the shared VM lease is free. Raw responses, failed
original probes, binaries, journals and native listings remain private.

## CI follow-up

The initial delivery `358e9a20` passes Windows CI and all new runtime checks, but
Ubuntu fails an existing cleanup assertion when a process is reaped between
opening and reading `/proc/<pid>/stat`. The test helper now retries that
inconclusive read while preserving its five-second exit deadline. Two checks on
actual owned workers reproduce the old failure and verify that an unreadable
stat file cannot pass for a live process. All 31 supervisor checks pass on Windows
and Linux; diagnostic groups take 2.03s and 13.09s including cleanup. Production,
harness and original timing evidence stay unchanged. Details are recorded in
[the follow-up evidence](follow-up-evidence.json).
