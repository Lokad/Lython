# Native operation facts — R273, retained for delivery

2026-11-12 over qualified8ec7bfab/beeb828e, worktree50004849. NativeReturnValue no
longer embeds an ordinary ExecutableInstruction, its physical positions or opcode.
It keeps the existing semantic operation, original source spans, one operation
operand, two graph inputs and shared method metadata. The conditional alternative
uses its otherwise unused operation operand. Compiler emission validates/extracts
these facts once. Runtime native consumers no longer decode ordinary instruction
getters. Existing native guard/evaluator and ordinary executable routes remain;
no controller, arena, type solver, cache, option, provider or public API is added.

1,032 focused checks pass on actual.NET11/10 (99internal/417public each), covering
native returns/conditions, constants/globals/live cells, defaults/keywords,
formatting, errors/original spans, reentry/refusal/cancellation, genuine async
protocols, retained aliases/results and allowance cleanup. Six complete native/
guarded search, CSV, captured text, JSON and report scripts pass both APIs,
compilation and compile-plus-one-run with full stdout/files/balances/cleanup.
All568 source hashes still match the frozen final snapshot.

Host-only managed allocation diagnostics, outside guest clocks, compare arrays
of128/256nodes without Unsafe.SizeOf or a layout assumption. Original arrays use
9,240/18,456B (derived72B stride); final6,168/12,312B (48B). Native-node storage
falls33.3%; whole-script compile allocation only−0.06..−0.18% for affected scripts
and report flat. Warm allocation effectively flat; borrowed host admission estimates
are unchanged and remain conservative. This is not a33%whole-script memory gain.

## Preserved first form / desktop uncertainty

The first implementation removed the embedded instruction but retained its opcode
and a separate third input, measuring56B/node. It passes1,032checks and all scripts;
compile allocation−0.04..−0.12%. Further source deletion produces the48B final form,
without padding or a new mode. A build caught two remaining uses of the old Read
kind before tests; original error log is kept. Corrected emission handles all four
semantic read kinds, and unknown compiler shapes fail explicitly.

Every first/final desktop observation remains. Final guarded Run elapsed−4.44..−3.78%
with0.69%control, captured Run−10.95..−5.04% with6.23%control, but captured Async
+2.28..+2.66% with0.36%control and report Run+4.99..+8.09% with2.96%control. Fresh
captured+15.53..+38.02%, control16.30%. Other controls move up to19.52%; these are
diagnostic elapsed observations, not CPU conclusions. No adverse result is erased.

## B627 bounded quiet comparison

Same fec79 worker/adapter0.4, normalGC/tiering, F4as_v7 /Ubuntu24.04.4 /.NET11RC3.
Reference is the fully qualified R271 frozen core; before568 raw source hashes
exactly match. Candidate is final semantic-fact snapshot. Four jobs, two processes/
image,5swarmup(12s captured),7rotated250msrounds.184.637seconds inside a240s owned
service/30sstop/30smargin. All identities/goldens/balances/quiet/join/unmount/archive/
localSHA gates pass. No live VM users or sole RAM evidence remain; terminal cached.

| Complete script / phase | Elapsed change | Wider processCPU change | Ref / candidate spread |
| --- | --- | --- | --- |
| Guarded search / Run | −6.06..+1.57% | −5.64..+1.12% | 6.00/1.61% |
| Captured text / Run | −7.87..+3.23% | −6.98..+2.07% | 7.58/3.44% |
| Captured text / RunAsync | −4.18..−0.19% | −4.76..+1.36% | 4.04/0.11% |
| Report control / Run | −3.04..−0.52% | −3.61..−1.27% | 2.48/0.11% |

Ranges span all four process-median pairings, not confidence intervals. Moving
reference controls leave CPU impact uncertain; no substantial CPU gain or equivalence
claim. Warm includes mediated RAM I/O/full validation/prior-result cleanup; excludes
compile/startup/reset/protocol/finalcache. Wider CPU includes reset/protocol/JIT/GC.
Retain the representation deletion and established node-storage reduction, with
explicit CPU uncertainty. Existing CPython rows remain source-labelled; do not
extrapolate source-pair gains. Full platform/stress/installed consumers follow.
The broader whole-script CPU objective remains open; publication stays postponed.

The first launch preflight was8,016Bshort after staging its helper: no root, worker
or retirement existed. Full before/current inventories and first deployment metadata
are preserved locally; omit only the redundant remote hash inventory. Exact failed-
helper cleanup has fresh lease/UID/path/inode/SHA proof. OS240MiB/evidence12MiB and
128KiBmetadata reserve stay unchanged. Only committed canonical exact terminal
B626 bytes and the superseded d51f reference (all3aliases) are retired, under fresh
lease/path/UID/inode/link/SHA proof. Qualified R271/current images remain protected.
All original conditions and corrected staging receipts survive.
