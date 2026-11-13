# Complete config-edit CPU source gate — R297 / B653

2026-11-13, committed31d65e01, frozen coree34cacaf, fixedfec79 worker/adapter0.4,
actual.NET11RC3 on quiet Azure F4as_v7/Ubuntu24.04.4. The existing181B config-edit
fixture and full source/stdout/file goldens come unchanged from the historical
UTF16/config checkpoint. This profiles the current runtime, not its old timing.

Twelve-second warmup/eight-second CPU-clock capture at199Hz, normal GC/tiering with
PerfMap diagnostic only. **737 selected samples**,3.87guest CPU seconds /
5.721814s execution clocks. **466 unknown leaves(63.23%)**, including
428 unresolved kernel leaves;0 ambiguous leaves/0 malformed map lines. Full raw
native/managed stacks, all thread CPU and exact IP intervals survive. This is not
an elapsed, allocation or source-pair speed comparison.

| Nearest owning frame (includes useful/inlined/runtime work) | Samples / share |
| --- | --- |
| Comparison host Resolve | 195 /26.46% |
| Comparison host OpenFileAsync | 142 /19.27% |
| Worker Take | 47 /6.38% |
| Worker Run | 43 /5.83% |
| HostOperation AwaitAsync | 37 /5.02% |
| Comparison byte-file WriteAsync | 35 /4.75% |
| CreateBuiltinVariables | 23 /3.12% |
| ExecutionContext constructor | 8 /1.09% |

## Decision and uncertainty

Invocation setup is not established as the dominant cost. The high unresolved
kernel share limits negative claims too:3.12% is not proof that setup is free,
nor a predicted removable share. Host containment resolution/open and scheduling
are prominent; do not bypass capability/path checks or call their costs core
interpreter work. This bounded gate ends without selecting a shared-type prototype.
Specific later evidence can justify refined measurement; no permanent design veto.

Shared plain functions and immutable signatures already exist. Remaining builtin
objects retain per-run type hierarchy, descriptor and namespace caches. In
particular PyType's namespace bridge captures its memory owner. Blind sharing
would retain refunded state; a future coherent migration must remove repeated
immutable construction while retaining actual execution ownership, rather than
introduce a lazy registry/controller. The current evidence does not justify it.

Source review also demonstrates a separate Python bug: aliasing object.__init__
into a guest class overwrites the original wrapper's __objclass__. A public probe
returns True/False/False versus CPythonTrue/True/True. R298 handles that bounded
definition-owner correction; no performance gain or shared metadata follows.

Original staging tried to link an old catalog over the already-staged config-only
catalog and failed before any mount/worker. A fresh idle lease verified every
staged input, then resumed without replayed observations. The offline analyzer also
kept its former report directory; corrected only the path/source label. Original
failed sources/logs survive. The actual capture passes full goldens/balances,
normal Closed/exit0, joined users, quiet gates and archive/SHA verification before
RAM unmount. Last free267776000B; no active VM users/guest mounts. OS240MiB and
evidence12MiB floors remain. No RSS/embedding-tail or new CPython timing claim.

[Attribution](attribution.json), [raw](observations.tgz),
[collection/analysis/failure sources](collection-sources.tar.xz),
[source gate](source-gate.json), [terminal](terminal-status.json),
[archive custody](archive-receipt.json).
