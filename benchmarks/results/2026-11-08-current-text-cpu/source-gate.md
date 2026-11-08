R116 bounded source audit, before CPU interpretation:

- Native dictionary value replacement already uses funded capacity and a plain
  array assignment (PyDict.Storage.cs StoreNativeValue). No scalar identity,
  per-update budget-resize or reclamation mechanism remains to remove there.
- Supplied ordinary positional calls already propagate `nativeAttempted` from
  LythonRuntime.Executable.Calls.cs:213 into BeginBoundCall:280. Their failed direct
  native proof is not repeated by that bound-call path. Other PyFunctionBase and
  class/nested entry points are distinct; seeing several method definitions does
  not prove the measured Quantity workload probes three times. Earlier R114
  repeated-probe discussion was an unverified hypothesis, not attribution.
- A failed native attempt does enter/leave managed interpreter admission before
  ordinary dispatch. That protects the recursive bounded proof; preserve it. Its
  CPU contribution is unknown. Do not invent a call-control carrier or weaken
  managed stack safety to remove speculative cost.
- R108 captured report, not the current ordinary text workload. R116 closes that
  actual measurement gap. Select any next complete phase/storage deletion from
  guest CPU stacks, with unknown/inlining limits and current semantics, rather
  than interpreting boxing or inclusive frames as dominant overhead.

No runtime change, new counters, tracing, caches or policies were introduced by
this audit. R115 custom async debt remains explicit and unresolved.
