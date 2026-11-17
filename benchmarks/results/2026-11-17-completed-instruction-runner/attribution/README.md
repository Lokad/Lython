# Shared-runner activation and operand attribution

R495/B916b,2026-11-17 UTC. Four fixed5-second diagnostic captures: positional
calls and the complete floating JSON report, each with qualified33bd7efd/frozen5e7
and the adverse private39c11156. Normal GC/tiering, PerfMap only,8-second warmup/
2-second settle/199Hz cpu-clock/frame-pointer samples. No elapsed retiming claim.

| Capture | Active samples | Unknown leaves | Invocations | Nearest helper entry | Nearest instruction loop |
| --- | ---: | ---: | ---: | ---: | ---: |
| Reference calls | 1,004 | 62 | 2,560 | 51 | 304 |
| Private calls | 1,020 | 53 | 1,920 | 77 | 322 |
| Reference floating | 970 | 54 | 3,072 | 14 | 181 |
| Private floating | 955 | 74 | 2,816 | 22 | 181 |

This is sampled attribution, not a controlled elapsed source pair. Nearest frames
include inlined work; inclusive groups overlap and are not removable percentages.
The private call capture exposes more arithmetic/publication/control work as well
as activation. Floating remains distributed across dispatch, stack transport and
indexing. Helper metadata alone is not established as the full regression cause.
Prior Windows code-generation evidence is diagnostic and remains separate.

Select a coherent refinement of the same runner: directly reference existing PC,
live extent and return slots, cache immutable code facts in the view, and delete
persistent work/storage initialization for stack-borrowed helpers. Flatten common
transfers/returns/jumps so these instructions do not select their opcode twice.
Keep real member/protocol/exception helpers, existing async disposal and one
authoritative instruction loop. No new owner, packet, unsafe API or resource policy.
Managed ref/span lab passes actual11/10, including guarded absent owned storage
and views ending before suspension. Correctness, goldens, ownership and a changed-
source whole-script/control comparison are still required before promotion.

First staging checked for an archive helper this collector does not use; it stopped
before starting a worker. Repair validates the actual profile/collector files and
rechecks all images. The first service then exited on CRLF shell text, before any
capture; retain original source/log/status. Explicit LF repair revalidated terminal
status, idle lease, all images and empty RAM fixtures before starting the new unit.
No measurement conditions, semantic assertions or timeouts were relaxed.

All four actual-image/golden/quiet/Closed/exit/join checks pass. Complete raw traces
and JIT maps SHA-archived persistently while the lease was held, before unmount.
Joined root-owned perf files had ownership corrected without byte changes. Current
qualified/private images and primary catalog remain; exact committed older raw
replicas alone retired. Both failed setup and successful terminal results cached.
Publication held; broader objective active.

[Attribution](attribution.json),[raw](observations.tgz),[identity](identity.json),
[custody](archive-receipt.json),[procedures and managed lab](procedures.tar.xz).
