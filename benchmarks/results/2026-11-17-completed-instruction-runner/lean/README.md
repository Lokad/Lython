# Lean shared-runner activation and common transfers

R495/B917,2026-11-17 UTC. **Private source; not promoted. Joint runner milestone continues.** Qualified
33bd7efd/frozen5e7058a3 versus9d59fb57 on22d5a5f3. This refines the same shared
runner after the [initial adverse pair](../README.md) and targeted
[activation/operand attribution](../attribution/README.md); no unchanged retiming.

Borrowed helpers now keep only scalar PC/live/return locals and their final values.
The ephemeral managed view references those locals or the existing owned fields;
there is one authoritative PC/live extent/return. It caches immutable code facts,
not guest values, and ends before suspension. Delete ForBorrowedPrefix, persistent
borrowed work/storage initialization and displaced instance-span overloads.
Synchronous retirement is shared with array-backed storage; async disposal keeps
its existing queue/ownership. Native helper-return funding and the corrected
compiler bound for private returned iteration loans remain. Closed cased-text
iteration stays available to borrowed helpers through its existing kernel.

Common constants/globals/name stores/duplicates/retirement, jumps and returns
execute directly in the one instruction loop, removing their second opcode
selection. Real member/protocol/protected exception helpers remain. The earlier
duplicate native loop/call/format/publication/integer dispatch, generator-only
bridge and whole-code eligibility scan stay deleted. No second backend, arena,
packet, owner, unsafe API, hidden limit or public option.

Actual Windows11/10 each pass1,006internal/4,217public focused checks after a
nonincremental Release rebuild. Five actual copied32-file worker goldens pass;
both allocation/allowance reuse diagnostics pass. These are focused regressions,
not full platform/stress/installed-package delivery qualification.

Windows11 diagnostic helper-entry code/stack: qualified1,099B/216B,
initial1,763B/440B, refined1,362B/264B. This confirms smaller activation construction,
not Linux causal attribution or a CPU gain. Codegen, initial compile-port failures
and all original conditions remain in the procedures archive.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference/candidate process spread |
| --- | --- | ---: | ---: | ---: |
| scripts.numeric-squared-error.floating.large | warm | +14.01..+18.45% | +13.00..+18.52% | 1.91/1.95% |
| scripts.numeric-squared-error.floating.large | compile-run | +8.09..+18.14% | +5.44..+17.31% | 6.95/2.19% |
| scripts.text-normalization.large | warm | +0.03..+3.69% | +0.00..+4.65% | 1.17/2.46% |
| scripts.json-summary-helpers.large | warm | +6.92..+9.70% | +6.15..+10.32% | 0.63/1.96% |
| calls.positional.large | warm | +20.69..+24.96% | +19.18..+26.87% | 0.73/2.78% |

191.84seconds/five jobs on quiet AzureF4as_v7,
Ubuntu24.04.4/.NET11RC3, six-second rotated full-request warmups/five rounds/four
processes, natural GC/tiering. Ranges compare all process medians; negative is
lower. Same-source controls/distributions are retained. This is not a causal
initial-versus-refined elapsed comparison or a CPython refresh.

Warm clocks include complete Run, mediated RAM I/O, full stdout/file goldens and
previous-result cleanup; fresh adds compile/compiled-owner disposal. Startup,
fixture reset/protocol and final cache teardown excluded; external CPU wider.
All actual-image/golden/quiet/Closed/exit/join checks pass. RAM SHA-archived
persistently under lease before unmount. Only superseded39c VM inode aliases and
exact committed completed raw replicas retire, after fresh idle lease/path/UID/
hash/link/all-alias proof. Qualified5e7/refined9d59/current catalog/toolchains and
canonical evidence remain. Terminal cached; publication held/objectiveactive.

[Summary](summary.json),[allocation](allocation-summary.json),[source](candidate-sources.tar.xz),
[prototype](prototype.patch.gz),[identity](identity.json),[checks](checks.json),
[raw](observations.tgz),[procedures/codegen](procedures-and-checks.tar.xz).

The refined footprint remains adverse against the qualified runtime. Smaller
activation metadata is a structural improvement, not proof of better elapsed or
CPU time. Initial-versus-refined observations are separate source conditions;
do not infer a causal improvement by subtracting their percentage ranges. Clear
floating/helper/call adversity needs no unchanged retiming. Keep both sources and
all control distributions; no permanent design rejection or automatic percentage
threshold is imposed.

Next concrete deletion map: consolidate existing closed arithmetic/sequence/
comparison selection into the one opcode loop. Delete the three selector frames;
keep pure numeric/index kernels and real protocol/async continuations. Completed
comparison results should remain bools instead of ValueTask wrap/unwrap traffic.
Retirement, exception/cancellation/refusal order and pending users must stay intact.
This addresses a demonstrated operation/operand stage; it adds no primitive
specializations, owner, arena, dispatch framework or public mode. Require focused
checks, frozen verification and one changed-source whole-script/control pair
before a new retention/debt decision. [Decision](decision.json).
