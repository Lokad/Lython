# One completed-instruction runner

R495/B915, 2026-11-17 UTC. **Private prototype; not promoted. Representation/cost investigation continues.** Fully
qualified R492/33bd7efd, frozen5e7058a3, versus39c11156 on22d5a5f3.
The prior R494 foundation and all its adverse conditions remain
[separate](../2026-11-17-native-generator-loop/README.md).

One existing synchronous completed-instruction loop now serves admitted frames,
generators and stack-borrowed helper prefixes. A managed ephemeral ref/span view
shares the authoritative work PC/live extent and final slots. It ends before
suspension. Existing owned continuations retain pending users; partial helpers
transfer once without replay. Remove duplicate NativeExecution.RunCore,
call/format/publication/integer dispatch, the generator-only bridge and whole-code
native opcode eligibility scanning. Preserve the call ABI, native helper returns,
simple loop exits, scalar comparisons and existing async lifecycle. No third
dispatcher, JIT, arena, owner, resource policy or public option.

An existing cancellation/loan regression exposed an incomplete compiler cleanup
bound: directly consumed/forwarded calls can return private range loans. Include
their result slots in the existing bound; this replaces the native final full
scan while retaining the test's cancellation/refund/alias assertions. The old test
constructor required adaptation, not deletion. Failed port builds and the initial
cleanup-bound failure remain in the procedures archive.

Nonincremental Release rebuild passes. Actual Windows .NET11/10 each pass1,006
internal/4,217 public focused checks, including functions, stack admission, loops,
exceptions, closures, genuine I/O, held cleanup, terminal refusal, cancellation,
retained aliases and shared-budget reuse. Five actual copied32-file worker goldens
pass; allocations are effectivelyflat, accounted peaks unchanged and all budget
baselines restored. These are focused checks, not full delivery qualification.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference/candidate process spread |
| --- | --- | ---: | ---: | ---: |
| scripts.numeric-squared-error.floating.large | warm | +12.38..+14.65% | +13.41..+13.82% | 0.97/1.04% |
| scripts.numeric-squared-error.floating.large | compile-run | +5.90..+22.25% | +5.93..+20.63% | 6.76/8.13% |
| scripts.text-normalization.large | warm | +1.83..+2.36% | +0.02..+1.44% | 0.25/0.27% |
| scripts.json-summary-helpers.large | warm | +9.12..+19.05% | +7.38..+20.86% | 2.88/6.04% |
| calls.positional.large | warm | +29.63..+36.61% | +32.41..+39.95% | 1.97/3.34% |

192.64seconds/five jobs; quiet AzureF4as_v7,
Ubuntu24.04.4/.NET11RC3, six-second rotated full-request warmups/five rounds/four
processes, natural GC/tiering. Ranges compare all candidate/reference process
medians; negative is lower. Controls and within-process distributions are retained.
Allocation reduction, simplification and CPU improvement are separate claims.
This source pair does not refresh CPython performance ratios.

Warm clocks include complete Run, mediated RAM I/O, full stdout/file goldens and
previous-result cleanup; fresh adds compilation/compiled-owner disposal. Startup,
fixture reset/protocol and final cache teardown excluded. External CPU is wider.

Two staging attempts refused the unchanged254MiB floor before mutation or
measurement. Original configs/logs remain; add only committed, hash-verified
replicas for fresh lease/path/UID/link/all-alias retirement, preserving qualified
5e7/current catalog/toolchains. A read-only inventory timed out; its exact lease
owner was verified and joined. The bounded replacement inventory completed.
All actual-image/golden/quiet/Closed/exit/join gates pass; RAM traces are
SHA-archived persistently under lease before unmount. Terminal cached.
Publication held; broader performance objective active.

[Summary](summary.json),[allocation](allocation-summary.json),[source](candidate-sources.tar.xz),
[prototype](prototype.patch.gz),[identity](identity.json),[checks](checks.json),
[raw](observations.tgz),[procedures](procedures-and-checks.tar.xz).

The net source reduction is useful, but this footprint is substantially slower.
Clear floating warm/text/call adversity needs no unchanged confirmation. Fresh
and helper process spreads limit magnitude; original adverse observations remain.
Do not present this as an improvement or copy it into production.

One local Windows11 diagnostic call-control capture (not Linux VM attribution)
finds optimized StartInline code1,099→1,763bytes and stack216→440bytes. Its new
persistent work/storage initialization is a concrete hypothesis; ref/span operand
transport can also cost each instruction. Neither code size nor boxing alone
explains the measured CPU. The first reused diagnostic expected a file hash on
a file-free control; adapt only the optional-key assertion, preserve its original
failure, and join both workers. No timer/GC/tiering/semantic assertion relaxed.

Next gate: attribute activation/operand costs, then identify a smaller shared
representation that deletes owned-state initialization for borrowed prefixes and
unnecessary hot transport. Preserve one authoritative PC/live extent and one
opcode loop. If that requires a general storage/compiler redesign, name the
blocker and withdraw the private foundation while retaining independent external
regressions. A coherent changed footprint needs both-target checks and one bounded
affected-script/control pair, with regression debt decided before promotion.
This is an active completion milestone, not permanent rejection or an automatic
percentage veto. [Decision/debt](decision.json),[diagnostic code generation](codegen.tar.xz).
