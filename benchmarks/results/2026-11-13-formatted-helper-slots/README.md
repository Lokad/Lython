# Formatted helper final slots — R296

2026-11-13; candidate **e34cacaf** over fully qualified91064b5d/**ed398473**.
The actual report's three dictionary fields already used cached native reads, but
f-string opcodes excluded its whole helper from final-slot execution. One shared
formatting kernel now serves ordinary and native storage, avoiding per-row frame/
workspace setup and argument transport. Existing pooling explains flat allocation.
No new controller, formatter backend, policy, dependency or public API is added.

Actual.NET11/10 focused checks pass:1,329internal/550public per target, plus the
exact report/no-frame case. Seven unchanged complete scripts pass sync, async,
compilation and compile-to-result, full stdout/file goldens, final zero balances
and fixture cleanup. Nested/dynamic fields, conversion order, single-field string
identity, reentrant admission and pending custom-format I/O/cancellation retain
their semantics. The live builder/input transfers once before a real protocol;
all users finish before funding returns.

## Controlled whole-script comparison

Quiet Azure F4as_v7/Ubuntu24.04.4, stock.NET11RC3, fixedfec79 worker/catalog,
adapter0.4. Four independent roles (reference/candidate plus unchanged copies),
5/8/12s warmups and seven rotated500ms rounds. **351.629s**, all gates/goldens,
normal Closed/exit0, joined users, archive/SHA verification and RAM unmount pass.

Percentages span all four candidate/reference median combinations. Wider process
CPU includes reset/protocol; it supports direction, not an isolated operation cost.

| Complete workload / phase | Elapsed change | Wider CPU change | Reference / candidate control spread |
| --- | --- | --- | --- |
| UTF8 report / warm | −9.05..−5.79% | −9.74..−4.13% | 0.96% /2.54% |
| UTF16 report / warm | −7.97..−6.53% | −7.41..−5.75% | 1.50% /0.07% |
| Report / compilation | −0.48..+3.05% | −1.98..+3.45% | 2.05% /1.46% |
| Report / compile-to-result | −8.27..−4.88% | −8.81..−6.32% | 1.43% /2.21% |
| CSV / warm control | −0.08..+0.69% | −0.34..0.00% | 0.29% /0.48% |
| Captured text / warm control | −0.65..+4.56% | −2.93..+2.51% | 1.56% /3.47% |
| Native search / warm control | −2.70..+1.18% | −2.40..+1.04% | 3.09% /0.76% |

Retain the lifecycle change: both report encodings improve with supporting CPU,
and compile-to-result improves. Compilation and text/search remain uncertain,
including the adverse combinations; no equivalence or blanket speed claim.
Allocation is flat. Existing native-search regression debt from earlier changes
is not erased. Full delivery qualification and affected CPython refresh are pending
at this recording. Publication/tagging remain held; the broader roadmap continues.

Desktop sync/async/compile/fresh clocks have moving controls and remain diagnostic.
An initial new fixture read unmirrored module locals incorrectly; construct its
input directly. Failed logs survive without a production correction. The offline
summarizer initially kept its previous six-job assertion; corrected to seven,
without rerunning any valid VM observation. No failed condition is discarded.

Superseded VM cores were preserved in Git before exact owned inode aliases retired.
The qualified replacement reference was installed/verified before retiring the
previous selected reference; OS240MiB/evidence12MiB floors remain. Last free
269717504B; no active VM users or guest mounts at the cached B651 terminal state.

[Local exact source/checks/all observations](local/source-checks.tar.xz),
[local validation](local/validation.json), [all VM rows](b651/summary.json),
[raw observations](b651/observations.tgz), [collection/analysis sources](b651/collection-sources.tar.xz),
[cached terminal](b651/terminal-status.json), [archive custody](b651/archive-receipt.json),
[historical core copies](superseded-vm-cores.tgz), [identities](superseded-cores.json).
