# Maintained JSON-report CPU attribution

R150/B505,2026-11-09. Committedafb11d97/core6417B97A, reusing the verified fresh
nonincremental R149 freeze with every production source byte unchanged. Common
worker/optional adapter, unchanged maintained json-summary-helpers script/input/full
stdout/file goldens. The R146 warm1.37–1.38× CPython gap describes65ed, not this capture.

One six-second existing-tool capture after execution warmup:6.083s recorded execution,
6.03 guest CPU seconds,1,201 guest/29 caller/17 tiering samples;5.66% guest leaves unknown.
Normal GC/tiering, only perf-map diagnostics. Innermost observed phases:

| Phase | Guest samples |
| --- | --- |
| VM prefix | 40.47% |
| JSON processing | 19.90% |
| Dictionary | 7.83% |
| Memory admission | 6.91% |
| Native call preparation | 6.74% |
| Text kernels | 6.41% |
| Managed cancellation/stack | 4.33% |

VM prefix includes useful inlined work and parent assignment for unknown/truncated
leaves. Shares identify observed work, not removable overhead. Largest leaves include
ExecuteInstructionPrefix188,ReadJsonString59,ReadJsonContainerRemainder49,TLSlookup49,
ExecuteOrdinaryCall43,MonitorExit34/Enter33,CompleteLocalResult31,ReadJsonValueExact31
and TryLoadDirectCallReceiver31. Caller reset19/host6/protocol3 are separate.

Source disproves presumed duplicate stages: instructions are already compact,
immutable and read by readonly reference; plain JSON strings bypass backend reparsing
and decoding scratch; record keys and completed-value loops already share work;
default JSON calls avoid the general binder. Early class binding facts deliberately
precede final class-cell demand, while module facts are retained at preparation's end.
No new phase deletion selected from inclusive counts; no extra capture, dispatch
framework, arena, cache or permanent design veto. Broader performance remains open.

All identity/quiet/golden/Closed/join/maps/RAM/archive/localSHA gates pass. Finite120s
service; persistent raw outside RAM, no remaining users/fixtures, no space recovery
or floor change. No elapsed/source-pair/CPython speedup claim or production change.
The first offline classifier omitted several JSON method names; retain its original
output and corrected source-owner classification using the same unmodified raw stacks.

[Summary/source gate](summary.json), [full attribution](attribution.json),
[measured source](core-source.json), [terminal receipt](terminal-status.json),
[raw capture/maps/code](observations.tgz), [collector/analysis/original output](preparation.tar.xz).
