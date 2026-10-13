# Pressure-point ownership reassessment

Assessed at `7f5fbf70` / production `dcec7539` on 2026-10-13. This is source
evidence and a stopped candidate, not a shipped collector or a measurement of
interpreter-frame overhead.

Keeping operands on the existing interpreter stack until a helper returns would
cover more call/subscript/operator inputs than today's early pops. Publishing a
pending result before its observation would cover another transfer boundary.
Those two changes alone do not establish a complete root rule:

- [Prepared dictionary expansion](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.PreparedOperations.cs)
  retains mapping methods, returned keys and a freshly governed `snapshot` while
  guest getters and iteration run. These are helper state, even if the original
  mapping remains on the operand stack.
- [Prepared calls](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.PreparedOperations.cs)
  pop the argument accumulator before invoking it. Keeping it published requires
  preserving its single disposal, failed-admission and exception-unwind behavior.
- [Native aggregation](../../../src/Lokad.Lython/Runtime/LythonRuntime.BuiltinIterables.Aggregates.cs)
  replaces `total` while guest operators and the next generator item execute.
  Input arguments do not describe the evolving accumulator. Deferring all refunds
  until native return breaks the already validated bounded native CSV consumer.
- A published interpreter still needs dynamic callers, suspended state and
  explicit child/resource edges. Complete traversal also needs funded workspace
  at exhaustion. An owned-byte snapshot supplies none of those relationships.

The candidate stops without adding a root registry, pending-result field or
partial refund policy to production. The required larger representation must
include evolving helper state and transfers, rather than relying on native stack
inspection, reflection or a namespace-only walk. The supported managed stack
admission policy remains independent and unchanged.

The [new pressure measurements](README.md) demonstrate why allocation-progress
suppression cannot stand in for that rule: each bound/exposed denial publishes
real exception storage and thereby authorizes another full collection. The
[separate recovery probe](recovery/drop-after-denial.py) demonstrates the converse:
reference withdrawal after a denial does not authorize relief, despite making
room logically. Both directions need consideration in one ownership invariant.

Any bounded eligibility correction must preserve unchanged pinned/no-op denial
suppression and retained aliases, and address actual withdrawal paths without
introducing scalar identity tracking or a second ledger. It cannot be reported
as GC-independent reclamation. The complete root/child/resource replacement,
Windows embedding impact and CPython milestone remain open.
