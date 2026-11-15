# Matched helper boundary attribution

R414/B808, 2026-11-15, committed **2865f619 / frozen55ba4c8a**. The existing R410
no-counter inline/helper scripts use identical data and output/file goldens. Each
has one 5s199Hz cpu-clock/frame-pointer capture after 8guest-seconds warmup. Perf
maps are an explicit diagnostic override; no controlled elapsed comparison or
source-change speedup follows.

| Diagnostic | Inline | Helper |
| --- | ---: | ---: |
| Complete invocations | 5,632 | 4,608 |
| Guest-thread CPU | 4.79s | 5.12s |
| Active-thread samples | 950 | 1,016 |
| Unknown leaves / ambiguous leaves | 80 / 0 | 93 / 0 |
| Nearest ordinary instruction-prefix frame | 121 | 124 |
| Nearest native helper Call / RunCore frame | 0 / 0 | 49 / 38 |
| Nearest StartNativeCall / StartInline frame | 0 / 0 | 20 / 20 |

Samples include useful/inlined work; captures have different invocation counts.
Inclusive entry/body groups overlap preparation and cannot be added or treated as
removable percentages. Zero samples do not prove zero cost. All raw stacks, thread
CPU, unknown leaves and method lists remain in [attribution](attribution.json).
Preparation, memory admission, hashing and dictionary accumulation remain sizable
in both scripts. Logical/physical managed stack protection stays intact.

Actual [compiler facts](compiler-facts.json), read from copied measured55ba4c8a,
confirm both main scopes unmirrored; the five-slot helper is native eligible.
The ordinary instruction prefix already loops completed operations outside an async
state machine. Re-proposing that behavior as missing, a namespace cliff, or another
argument array/arena is unsupported. The source does not isolate a dominant removable
binding allocation. Prior R403 controller and R411 workspace/clear/copy adversity
remain relevant.

Close this bounded attribution with the [precise remaining question](decision.json):
can a complete call/operand representation displace native/ordinary control
duplication while preserving final-slot speed, distinct logical admission and real
suspension, without adding the measured controller/clearing costs? A prototype needs
that whole invariant and explicit removal target first. No isolated opcode tweak,
stack/cancellation weakening or unchanged sampling series is selected. This is not
a permanent design ban or completion of the broader performance objective.

Quiet F4as_v7 / Ubuntu24.04.4 / four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 normal
tiering/workstation GC. Existing public worker/mediated RAM I/O/full goldens/shared
allowance. CPU samples include fixture reset/protocol/validation beyond execution
clocks. No startup, pending-I/O, RSS, contention or embedding-tail claim.

Full images/catalog, goldens, allowance reuse, quiet before/after, Closed/exit/join
pass. RAM traces/maps are archived and verified persistently under the lease before
unmount; no users or RAM fixtures remain. B808 is terminal cached; never requery.
Original missing retired-core read-only inspection, diagnostic-headroom refusal and
local missing-output-argument failure remain in the procedure. Refusal precedes
mutation; only exact committed archived replicas are retired after fresh UID/path/
hash/link/all-alias proof. Canonical evidence, current images and disk floors stay.

[Raw evidence](observations.tgz), [identity](identity.json),
[procedures/original conditions](procedure-and-original-conditions.tar.xz).
