# Current captured-text CPU attribution

R374/B765, 2026-11-15, exact qualified runtime cf754dc7 / frozen 95798eb9.
One 5s cpu-clock/fp capture at 199Hz after 8 guest-seconds warmup, unchanged
complete text script, worker, optional 0.4 adapter and catalog. Diagnostic JIT/map
overrides are explicit; this is not an elapsed or allocation comparison.

935 selected guest leaves / 4.71 guest CPU seconds, 76 unmapped leaves, zero
ambiguous. Nearest-frame families include 438 ordinary/call (including the
already-fused loop), 196 text, 105 native helper/slot, 68 admission and 21 file/
iteration samples. These include useful/inlined work and are not removable shares.
The hottest leaf is TryExecuteDictionaryLoop (192), then ordinary dispatch (98),
cased-word preparation (87), native RunCore (44) and UTF8 casing (43). Same-process
native-body mapping is unique: 5,937 bytes / 150 instruction groups, all 44 leaves
mapped. It does not explain the internals of the 192-leaf bulk loop.

Source confirms existing raw-word histogram consumption, deferred private word/
total publication, one-probe native dictionary updates, eager preflight and reused
ranges. Direct append/get/write/CSV dispatch, bulk CSV scanning/admission/adoption,
inline strings/windows and final UTF8 output already exist. No missing arena or
removed text-conversion stage may be claimed again.

The source gate selected a bounded callback-free segment prototype (R376) to
remove repeated per-word activation/revalidation/retirement using existing owners
and mechanisms. Its benefit is unmeasured here. The review also found two real
[overlapping-binding bugs](../2026-11-15-histogram-binding-aliases/README.md), fixed
before relying on that invariant. No generic controller, value rewrite or new
profiling framework follows. Preserve slower call-controller/arena and cased-buffer
lessons; a percentage of samples is no speedup forecast.

All identity/golden/file/budget/quiet/join/archive gates pass. RAM traces are
archived and verified persistently under lease before unmount, all users joined.
Two exact duplicate B762/B763 JSON leaves are deduplicated only with committed,
local/remote archive/member hashes and path/UID/link proof; compressed originals,
local Git evidence and images/catalogs/toolchains remain. Floors unchanged.
[Raw](observations.tgz), [leaf map](attribution-text.json),
[native groups](native-body-text-instruction-groups.json).
