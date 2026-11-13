# Native-entry instruction attribution

2026-11-13, B695: committede698656c/frozen660a0338, fixed B685 worker/optional0.4
adapter, quiet F4as_v7/Ubuntu24.04.4/.NET11RC3. The existing complete search script
has one five-second cpu-clock capture at199Hz after eight guest seconds warmup.
PerfMap and native-entry JIT disassembly are explicit diagnostic overrides. This
is attribution, not a timing/allocation/source-pair comparison or CPython ratio.

770guest leaf samples /3.91guest CPU seconds;175unknown leaves (22.73%, including
129kernel), zero ambiguous leaves. Native entry has46samples (5.97%). The unique
Tier1 perf-map range and the same-process disassembly both have4082bytes; all46IPs
map exactly to recorded instruction groups. Group granularity may include several
source operations and sampling skid; zero samples do not establish zero cost.
Full worker/reset/protocol/other-thread CPU and unresolved leaves remain raw.

The largest entry group has10samples at0x26f–0x2be, calling dictionary key preparation
and reading its native-layout/protocol metadata for live keyword defaults. Further
sampled groups probe buckets, keys and default storage. Entry prolog and epilog
have3samples each; other entry setup has4. These are useful work and mixed groups,
not removable-time forecasts. The entry also transports its bound-call descriptor
into StartInline, but its2sample group does not establish a dominant packet/copy cost.

Stop this gate without selecting another call arena, parameter representation or
controller. Source already has final slots, deferred frames and exact transfer;
caller spare capacity remains smaller than callee demand. R326's source-pair search
is effectively flat. A new default cache would address another small local path;
the evidence does not support a substantial complete activation removal. This is
a current decision, not a permanent design ban. Bulk text hashing also already
uses cancellable HashCode.AddBytes chunks; no naive scalar-hash rewrite is selected.

The owned120-second collection finishes under its bound, with image/golden/quiet/
join/archive checks passing. All users end and the raw SHA archive is verified
before RAM fixtures unmount. The initial space preflight refuses before staging;
only exact committed-archive working copies are later retired under a fresh idle
lease. Original failure, commands and space conditions survive; floors and current
images are preserved. Publication remains postponed.
