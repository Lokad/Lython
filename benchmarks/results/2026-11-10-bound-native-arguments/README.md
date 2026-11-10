# R214 — bound native operand borrowing, withdrawn

**Withdraw the complete production variant.** Exact restored runtime matches all
568 normalized C# sources of fully qualified e543bf15. Keep the independently
checked positional-method/default/alias case and exact-positional retained-result
coverage; no target-mutation path, new helper/backend or compatibility stub remains.
[Decision and restored checks](decision.json), [source/commands/builds/tests/original conditions](preparation.tar.xz).

The candidate extended existing exact positional borrowing to bound native helpers.
Its evaluated receiver temporarily occupied the target slot immediately preceding
arguments, restored in finally before fallback/error/publication. This skipped
native buffer filling, assignment-presence binding and clearing for that shape.
The general binder remained necessary. No allocation or ownership representation
change, new cache/map/packet, quota or unsafe code. Current-token, stack and terminal
refusal semantics remained active; the borrowed span could not cross suspension.

Forced Release builds pass without warnings/errors. **155 internal / 224 public
focused checks pass per actual .NET 11 and .NET 10** on both candidate and restored
source, including receiver/descriptor/default/keyword/error semantics, aliases,
genuine pending host protocols, cancellation, terminal refusal and budget reuse.
The new method-default output was independently checked under local CPython3.13.2.
Restoration used owned-file hash guards and a fresh nonincremental build. No
unchanged full-platform/package qualification was repeated; exact e543 source's
cached GREEN42898279287 remains authoritative. No candidate delivery is claimed.

## Measurements and decision

Reference fresh postcommit0e2bc55b (e543), candidate2f9b100e (f1c850fb plus preserved
patch). Both exact DLLs survive in [quiet raw evidence](quiet/observations.tgz).
2026-11-10, F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering, existing worker
and optional adapter0.4. Two complete sync-warm jobs **75.696 seconds**; four
independent source roles,5s warmup each,7rotated400msrounds. All source/fixture/full
output, ownership, quiet, Closed/join, RAM removal, archive-under-lease and local
SHA gates pass. [All medians/IQR/CPU](quiet/summary.json), [receipt](quiet/archive-receipt.json).

| Existing complete workload | Elapsed change | Wider process CPU | Reference / candidate spread |
| --- | ---: | ---: | ---: |
| scripts.text-summary-helpers.class-callable.large | -1.58..-0.80% | -3.44..-0.88% | 0.29% / 0.51% |
| scripts.text-summary-helpers.large | -0.21..+0.75% | +0.42..+1.68% | 0.24% / 0.71% |

The bound-method target has a small favorable elapsed result with supporting wider
CPU. Ordinary text retains possible small CPU regression debt; allocation and
compilation bytes are flat. Ranges include all four source pairings, not confidence
intervals. Max IQR is0.98%target/1.51%control. CPU includes reset/protocol and coarse
ticks; no general equivalence, async improvement or embedding latency is established.

The added mutable-target/finally route and extra hot-selector branch do not remove
the binder globally. The useful measured gain does not justify this narrower path
and unresolved ordinary-call debt for the current simplification milestone. This
is a complexity/whole-script decision, not a fixed-percentage veto or permanent ban
on a different calling convention. Do not automatically revive the unchanged variant.

[Desktop diagnostics](desktop/) retain all adverse results: bound sync+2.95..5.03%,
async+3.23..6.91%, unchanged compilation+5.52..8.91%, with moving controls. Other
controls' favorable/adverse values survive unchanged. These timings do not isolate
the cause of the original adversity. The first four-case setup failed at the
unchanged driver's five-case guard before any workload; its empty output/error and
catalog survive. The correction added an existing CSV control and changed no core,
driver/settings. Quiet used only target and ordinary-text control. No failed clock
was erased or selectively relabelled as a current HEAD gain.

## Initial workload relevance

One current class-renderer report capture, before any code edit, has1093execution-
thread samples/73unmapped6.68%,5.51threadCPU seconds and5.767worker execution seconds.
[Mapped leaves/all threads](profile/attribution.json), [raw maps/stacks/current core](profile/observations.tgz).
Its multi-field formatted helper uses ordinary executable work, so it does not
exercise the intended native binder. The existing R145/R192 bound Words.from_line
text helper supplies the corrected target; no new script framework or extra CPU
capture. Keep this original mismatch separate from the actual source pair.
Current numeric formatting already uses .NET11 UTF-8 TryFormat; an internal
FormatBigInteger symbol alone does not establish a temporary CLR string.

All owned users ended; no VM workers/RAM remain. Staging advanced the protected
snapshot to fully qualified0e, retiring superseded101/Dbe inode groups and one exact
raw-map duplicate only after committed canonical/all-alias/UID/path/hash proof and
verified replacement payload. [Recovery](quiet/space-recovery.json). Their immutable
R207/R211 archives remain unchanged; do not probe retired aliases. The inherited
afterRecoveryFreeBytes label describes post-extraction space, while the required
threshold precedes extraction; the [boundary note](quiet/staging-boundary-note.json)
keeps these original fields and their distinct checks explicit. Final free space
262,758,400B, fixed240MiBfloor/12MiBsetup headroom intact. Publication is postponed;
the broader runtime and independent regex work continue.
