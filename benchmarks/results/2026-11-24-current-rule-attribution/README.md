# B1627 Attribute the remaining rule costs

These three six-second CPU profiles use the sealed B1624 worker
images. The eager-rule pair compares `48790b2c7` and `6466fbb52`.
The defaulted-rule profile uses `6466fbb52`. Each worker warms for
at least eight seconds of guest elapsed time, then samples once per
5 ms of worker CPU. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3 on November 24, 2026 UTC. The recording covers entire
requests and serves source attribution.

| Profile | Invocations | CPU samples | Resolved runtime ancestry | Unmapped leaf |
| --- | ---: | ---: | ---: | ---: |
| Eager rules, before operand forwarding | 12,288 | 1,191 | 1,181 | 86 |
| Eager rules, after operand forwarding | 13,056 | 1,180 | 1,176 | 86 |
| Defaulted rules, after operand forwarding | 6,656 | 1,201 | 1,196 | 96 |

The current defaulted workload has 562 samples under definition
construction, of 1,196 with resolved runtime ancestry. The nearest
runtime method is `CreateClosedPairMember` in 239 samples. The 156
nearest reservation samples include 92 with `CreateFunctionValue`
as the nearest explicit caller and 56 with
`AllocateExecutableDefaults`. The earlier B1621 census distinguishes
one-time module retention from the repeated funding requests.
This locates a cost around construction and its funding, after
the operand transport work. [Admission census](../2026-11-24-json-default-prefix/README.md#closed-pair-construction).

Both eager-rule images have identical tapes. Their factory call
can complete as a forwarded computed definition and the consumer
has the existing integer-call update. The profile sees both routes
and at most 13 samples nearest entry and operand movement, including
unrelated small call sites in the script. The eager timings under
profiling move in the opposite direction from the earlier uninstrumented
CPU comparison. These samples do not reproduce that timing effect
or isolate a changed call-path cost. Keep the earlier adverse
measurements and revisit attribution if a later runtime checkpoint
isolates a repeatable hotspot. [Earlier timing](../2026-11-24-forwarded-positional/framed-entry/README.md).

The first collection reached 7.87 seconds of guest warmup in its
128 batches, below the required eight seconds. It stopped before
sampling. The retry used 256 invocations per batch and retained
the warmup target, profile duration and cleanup deadline. The
failed run, including its complete cleanup, remains archived.
All retry goldens, image checks, worker joins, fixture cleanup and
quiet gates passed. Staging preserved a byte-identical immutable
catalog to reclaim sufficient disk space.

[Attribution](attribution.json) · [Eager tape census](eager-tape-summary.json) ·
[Image identity](identity.json) · [Raw profiles](observations.tar.xz) ·
[Procedures and failed run](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
