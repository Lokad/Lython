# Current captured-text loop interior

R407/B793,2026-11-15, committed **43bce13a / frozen9e890d18**. One5s199Hz
cpu-clock/frame-pointer capture after8guest-seconds warmup, unchanged complete
3072-line/35,840B text workload. Perf maps and same-process loop JIT disassembly
are explicit diagnostic overrides; no elapsed/allocation/CPython gain claim.

993guest leaves/5.01guest CPU seconds;96unknown leaves,0ambiguous. Exactly151
leaves map into the unique optimized **9912B** dictionary-loop body, with every
sample assigned to a validated instruction group. The largest group has15leaves
and copies the compact word; other groups mix metadata, hash, bucket/key probing
and scalar updates. They do not establish a dominant removable complete call/frame
or justify another arena, dispatch controller or isolated struct-copy tweak.
Inlining/skid and mixed groups limit source attribution; zero samples mean no
proof of zero cost. Earlier R342 reaches a similar conclusion at its named source.

Cased preparation has115resolved leaves, UTF8 case writing33 and scalar mapping28.
These separate leaves include useful work and are not removable fractions.
Source still inventories original byte ranges, then cases each word during
consumption. The revised **R408 proposal** combines boundary discovery and casing
in one preparation pass, retaining ranges over final admitted bytes, then copies
completed word bytes. It removes deferred per-word conversion/source retention.
Keep eager all-word funding, Unicode/context rules, independent escaped strings,
private-binding publication before callbacks, nested loans and awaited cleanup.

This is materially different from [withdrawnR343](../2026-11-14-bulk-cased-buffer-withdrawn/README.md),
which removed ranges but added whole-buffer scans, saved only88B/job and measured
8–23%slower with stable CSV controls. Do not replay it unchanged or forecast a
speedup. Retaining ranges avoids those new scans but adds reusable scratch and
can deny earlier for long lines. Measure that tradeoff at an explicit completion
gate before retention. Existing bulk ASCII, shared scans, cursor loans and
one-probe dictionary updates are the starting point.

All identity/golden/budget/quiet/Closed/exit/join checks pass. Every RAM byte is
archived/verified persistently under lease before unmount; no users or fixtures
remain. The initial idle preflight finds an already-retired B784 duplicate and
refuses before mutation/service start; preserve that failure. Fresh read-only
inventory then identifies one exact committed archived B776working replica,
verified by Git/local/remote hashes, path/UID/link proof under an idle lease.
Only that replica is retired; current images/canonical evidence/toolchains stay,
254/252MiB floors remain. B793 is terminal cached, never requery.

[Wider attribution](attribution-text.json),[all exact instruction groups](dictionary-loop-instruction-groups.json),
[decision/source map](decision.json),[raw evidence](observations.tgz),
[cleanup](archive-receipt.json),[procedures and original conditions](procedure.tar.xz).
