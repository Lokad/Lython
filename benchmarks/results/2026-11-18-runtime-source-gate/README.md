# Current complete-script runtime source gate

R529/B958, 2026-11-18 UTC. Fully qualified **925397f65 / frozen2c6b304b**,
normal adapter0.4 unchanged. All32 actual image hashes and the custom two-case
worker/CPython goldens pass. DefaultGC/tiering plus managed perf maps;
eight guest seconds of warmup, five seconds of capture per script. Existing
profiler/protocol only: no runtime counters, hooks, framework or source changes.

| Script | All-thread samples | Unmapped leaves | Guest capture seconds |
| --- | --- | --- | --- |
| Floating JSON report | 1052 | 153 | 4.996 |
| Helper text normalization | 1020 | 204 | 4.827 |

Sampling includes reset, mediated RAM I/O, output validation, protocol and cleanup,
wider than execution clocks. All thread CPU/counts, maps and raw samples remain
in the archive. Inclusive groups overlap bodies and inlined useful work; they
are navigation aids, not removable CPU shares. No elapsed comparison or speedup
is established here. Unknown leaves and differences in invocation counts remain
explicit in [attribution](attribution.json).

Floating work spans instruction dispatch, sequence access and native JSON number
conversion. Helper text includes substantial native-call dispatch plus useful
text transforms and cleanup. Ordinary helpers already borrow final arguments,
avoid a full invocation frame when complete, and publish native results. These
samples do not establish another redundant whole entry/controller. Exact
sum(bytes) already has a bulk kernel; no new checksum path is justified.

**Gate decision:** do not start another call arena, generic value representation
or whole compiler rewrite from inclusive samples. The precise missing fact is
where CPU falls inside native call dispatch after its useful callees are removed.
One bounded capture with matching JIT assembly may resolve this and select a
concrete operand/entry/storage deletion. Preserve uncertainty if it does not;
retain earlier slower caller-window/controller lessons without a permanent ban.
This gate does not complete the broader performance objective.

Quiet before/after, Closed responses, joined users and SHA-verified evidence
archive before RAM teardown all pass. Only exact committed B957raw/whole and
B948whole replicas retire after fresh lease/path/UID/hash/link/all-alias proof.
Current images and254/252MiB floors remain intact. Terminal cached; do not rerun
unchanged qualification. Publication remains postponed.

[Identity](identity.json),[checks](actual-worker-preflight.json),
[raw](observations.tgz),[custody](archive-receipt.json),[procedures](procedures.tar.xz).
