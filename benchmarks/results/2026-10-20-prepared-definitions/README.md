# Prepared definitions share executable execution

`eed93783` removes the obsolete `PushObserved` forwarding helper and 17 unused
span arguments. Five suspending producers await before accessing the mutable
operand stack. Actual operand replacement, storage admission and unwind remain.
Existing focused Release selections pass 66 white-box and 70 public checks.

`37ea6fb4` makes functions whose definition inputs cross a guest `yield` reuse the
existing compiled binding and ordinary definition factory. It removes their
lowered-body dispatch, duplicate lowered body, whole-namespace resynchronization
and the obsolete compiler rejection in that binding builder. Frontend diagnostics
for invalid suspension remain. Class bodies retain their real lowered route;
the rejected class-method prototype and callback/stack policies are not restored.

Four source-level controls receive a lowered `PyFunction` before this change and
an executable function after it. They also verify aliased defaults, nonlocal cells,
docstrings and generic metadata after generator close. This is execution-route
consolidation, not a claim that the previous Python result was wrong.

Preparation admits its captured-expression/default-map scratch before construction
and holds it through awaited metadata and decorators. Published values/cells keep
their existing execution/result owner. Code uses the existing conservative
compilation owner. Extra scratch can refuse earlier; frame counts and ownership
policies are unchanged. There is no new allowance, tracing, transfer or quota.

Focused Release validation at the final source passes:

- 42 white-box prepared/generic/lambda/async-function checks.
- 79 public definition, metadata, retention, ordering and host-I/O checks.
- 20 isolated post-I/O stack controls, including the two new prepared-function
  recursion/copy-hook cases, cleanup and recovery at the unchanged 2 MiB allowance.
- Both execution APIs match CPython for the [private-name/class-cell probe](class-cell.py).

The public and stack selections briefly overlapped locally. The four new host
boundaries and both new fatal-stack boundaries were repeated sequentially and
pass. The result-retention fixture returns collection data: a scalar-only result
can legitimately release the execution owner immediately. No budget, timeout or
containment assertion was relaxed. [Receipts, outputs and counts](checks.json),
[original source-route failures](red-prepared-white.txt) and [scope](scope.json)
preserve the evidence.

No timing capture or speedup claim. The maintained core catalog has no yielding
definition-input case; ordinary generator kernels would not measure this change.
No new benchmark framework or full lane was added. Performance against CPython
remains described by the explicitly versioned snapshots in [BENCHMARKS.md](../../../BENCHMARKS.md).

Delivery **37ea6fb4** is fully qualified by
[CI 39340904174](https://github.com/Lokad/Lython/actions/runs/39340904174): Windows
and Ubuntu Release builds, complete ordinary suites, original Stress and both
explicit package consumers pass. Every required step was inspected; [terminal
gate proof](delivery.json) is cached permanently. Four status queries were spaced
at least three minutes apart. Later documentation/evidence changes leave runtime,
tests, build and CI inputs unchanged. No publication or further backend campaign.

Follow-up source assessment found two `dict.setdefault` differences on this
revision, under both APIs: keywords are incorrectly accepted, and a guest key is
hashed twice on insertion and zero times on a hit, versus once each in CPython.
Relevant member/storage sources are unchanged by this batch. The independent
[keyword](follow-up/setdefault-control.py) and [hash](follow-up/setdefault-hash-control.py)
probes, [receipts](follow-up/probes.json) and [reference identity](reference.json)
preserve the findings. They remain open correctness tasks before the proposed
ordinary-call optimization; a green delivery does not establish full compatibility.
