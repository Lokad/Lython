# Shared CSV field backing: first controlled phase

Uncommitted R308/frozen21d0be4f against fully qualified14dd45b7/93a6d134.
One production file packs all non-inline text fields into one immutable row array
using existing UTF8 windows. Short fields remain inline; admission/working overlap,
grammar/error order and row adoption unchanged. No new ownership infrastructure.
Retaining one field pins the row's long payload; the batch already retains charges.

117internal/372public actual.NET11 Release CSV checks pass: new row-admission
refusal/cancellation, long Unicode offsets/aliases, later input and scratch cleanup,
genuine delayed reads plus existing numeric/header/error/protocol coverage. An initial
test-host conditional type error is preserved and corrected before measurements.

Eight complete scripts/both APIs pass. The new text-rich invoice uses the exact
original script/output:4096records/823388B, three realistic description/path/note
columns, independent CPython3.13.2 goldens. It closes a fixture gap; original short
invoice/CSV/text/JSON/report controls remain. No new benchmark framework.
Rich allocation−3.50% both APIs, other allocation flat. Desktop rich elapsed is
adverse(+2.8..4.7%sync,+6.2..11.3%async), with moving/adverse controls preserved.

| Whole script / phase | Quiet elapsed | Wider process CPU | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Text-rich invoice warm |−2.41..−0.28%|−1.75..+0.13%|0.86/1.31%;1.29%|
| Text-rich invoice compile + run |−1.71..0%|−1.85..+1.85%|1.63/0.09%;1.67%|
| Original invoice warm |−3.95..−0.49%|−5.09..+0.31%|3.34/0.15%;1.06%|
| Captured text warm |−7.91..+0.11%|−7.85..+0.29%|7.65/0.39%;1.43%|

B663 completes four jobs169.459s within300s owner,2026-11-13UTC, quietF4as_v7/
Ubuntu24.04.4/fourEPYC9V45vCPUs/16GiB/no swap, ordinary.NET11RC3 GC/tiering,
unchangedfec79worker/adapter0.4. Five rotating500ms rounds,5s warmups(12captured),
two processes/image. Ranges all four medians, not confidence intervals. Warm includes
mediated RAM-file I/O, full stdout/file checks and prior-result cleanup; fresh adds
compile/compiled-owner disposal. Startup/reset/staging/final cache cleanup excluded.
Wider CPU includes reset/protocol. No CPython performance or embedding-latency claim.

All identity/golden/balance/quiet/join/persistent-SHA archive gates pass. No users
or RAM fixtures remain. To stage exact93a6reference plus21candidate, only superseded/
unqualified628ab680/2f34a48f cores are retired after tracked-canonical archive bytes,
all aliases/inodes/UIDs and fresh idle lease verification. Qualified cores remain;
OS240MiB/evidence12MiB/staging floors are unchanged. All proofs survive in source
evidence. Final free267374592B.

Decision is refinement, not promotion or a CPU failure verdict. Controlled results
do not reproduce desktop adversity, but CPU/fresh cross zero and captured reference
spread is7.65%. One coherent simplification follows: derive sizing from completed
field descriptors, deleting persistent byte counters/reset invariants. Retain
this original phase and allow one changed-source confirmation before a final
retain/refine/withdraw decision. No unchanged retiming/full qualification replay.

[Raw observations](observations.tgz), [computed rows](summary.json),
[exact source/bits/checks/failed conditions](source-checks.tar.xz).
