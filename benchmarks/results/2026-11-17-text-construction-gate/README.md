# Current text construction and invocation source gate

R480/B894, 2026-11-17 UTC. Qualified **d8611c85 / frozen0315de0a**;
no production change or new runtime prototype. Actual copied 32-file worker passes
both custom catalog/independent CPython goldens before staging.

| Complete workload | Active samples | Unknown leaves | Nearest private drain / join frames |
| --- | ---: | ---: | ---: |
| Original text normalization, generator trim/join | 951 | 174 | 1 / 27 |
| Same task through a helper and list | 939 | 128 | 6 / 17 |

Private materialization still owns a List<object>, grows capacity repeatedly and
keeps a funded snapshot through join validation/output admission. Its cursor's
known count is unused there. But the capture does **not establish this growth as
a dominant cost**, so it does not justify a buffer framework or a growth-only
optimization. Invocation/dispatch and native UTF-8 operations remain prominent.
The inclusive call/frame group has 356/542 samples, overlapping useful operations;
it is not a removable CPU fraction. Nearest frames, all leaves and unknowns are
preserved. No speedup, allocation saving or timing ratio is inferred.

Source checks preserve completed work: eligible native helpers already borrow
short arguments and defer contexts; bound values already become locals. Generator
drains already batch. Trimming already uses UTF-8; sufficiently large same-owner
slices share funded immutable arrays. Public lists already own their final storage.
Join deliberately drains arbitrary iterables before validating string items,
preserving later effects/errors. Mutable sources cannot simply be borrowed across
host admission reentry. Do not propose any of those completed changes again.

The bounded gate is complete: no further unchanged capture, buffer prototype or
internal architecture test is started. Continue the complete call lifecycle and
operand-transport design from concrete source/stack evidence, naming a removable
handoff and semantic contract before implementation. This is uncertainty about
the next design, not a permanent rejection of construction improvements or the
completion of the broader performance objective.

Quiet Azure F4as_v7 / Ubuntu24.04.4 / .NET11 RC3. Existing perf cpu-clock 199 Hz,
frame-pointer protocol; DOTNET_PerfMapEnabled=3, eight seconds of guest-clock
warmup, about five seconds active capture per case. All thread CPU/raw stacks
are retained. Captured whole-worker batches include reset/protocol/validation
beyond execution clocks. No default-runtime elapsed, CPython ratio, startup,
asynchronous-I/O, RSS or embedding-tail comparison is made.

All goldens/Closed/exit/join/quiet gates pass. Persistent archive SHA and every RAM
trace are verified before unmount; terminal cached. Fresh path/UID/hash/link/
all-alias proof retires only the committed withdrawn be7497de core's two working
aliases; qualified current0315, toolchains, canonical evidence and 254/252 MiB
floors remain intact. A local early retrieval guard rejected an observation before
its deadline without contacting the VM; the same completed collection was then
retrieved. No timing repeated or runtime qualification replayed. Publication held.

[Source gate](source-gate.json), [full attribution](attribution.json),
[raw stacks/maps](observations.tgz), [identity](identity.json),
[custody](archive-receipt.json), [procedures](procedures-and-checks.tar.xz).
