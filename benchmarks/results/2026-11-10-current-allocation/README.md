# Qualified-source allocation refresh — R206

2026-11-10, runtime `dba779a5` / evidence HEAD `976fdbfc`, source-equivalent
forced-build core `101ba9d3`. All 568 normalized production sources match the
qualified runtime. The exact R203 freeze and unchanged worker are reused; no
source restore, rebuild, new prototype or repeated platform qualification.

Two existing complete scripts, unchanged D36 allocation-tick collector and R134
protocol: 256 warmups then 512 checked capture invocations each. Windows x64,
.NET 11 RC3, normal GC/tiering. No forced collection, heap dump, rundown, tracing
of guest ownership or runtime instrumentation. This is not CPU or elapsed evidence.

| Threshold-crossing allocation type | Text helpers | Validated CSV |
| --- | ---: | ---: |
| PyString | 39.06% | 25.96% |
| Boxed BigInteger | 28.04% | 25.73% |
| Private split cursor | 15.45% | unobserved |
| TemporaryMemoryReservation | 6.13% | 12.93% |
| Byte[] | 9.10% | 1.84% |
| PyDict | unobserved | 14.01% |
| Object[] | unobserved | 10.23% |
| PyTuple | unobserved | 5.60% |
| PendingJump | unobserved | 2.68% |

Text has 6,480 selected ticks over 4.046 seconds; CSV 15,945 of 15,946 ticks over
5.067 seconds. One CSV tick outside the marked window remains raw. Zero events
lost. A tick associates an allocation interval with its threshold-crossing type;
these are **not exact per-type byte shares or object counts**. All threads and
object-size associations remain visible. No dominance or speedup follows from
these associations. The prior Boolean-box and eager split-list profile is historical;
this refresh reflects the retained Boolean and split-iteration work.

The full request includes mediated file I/O, fixture reset, output/file checks,
protocol and prior-result cleanup. Compilation, startup, warmup and final teardown
are excluded. Both scripts pass before/after verification, all source/fixture/full
output identities, normal runtime checks, cooperative collector stop, worker Closed,
process joins, empty stderr and removal of only owned fixtures, within a 120-second
owner. No VM or CPython timing, embedding tail-latency claim or new backend test.

Source assessment: CSV row adoption, shared header keys and exact capacity already
remove the proposed second values copy. Ordinary builtin metadata and sparse module
namespaces already remove the presumed repeated inventory; connected mutable types
remain per execution. The [R205 source gate](invocation-source-gate.json) reuses
existing config attribution rather than another profile. R176's dual scalar
representation and R188's local sidecars saved allocation without establishing a
simpler/faster engine; R204's fewer operand loads alone did not establish benefit.

This refresh completes a bounded diagnostic, not the whole-script performance
objective. Remaining selection must identify actual removable construction work
and account for its whole path, including escaping values and generic operations.
Do not infer a new cache, numeric representation or ownership subsystem from a
sampled allocation share. CPU and allocation remain distinct acceptance evidence.

[Associations](assessment.json), [all threads/object sizes](thread-assessment.json),
[source/freeze proof](source-proof.json), [selection before capture](selection.md),
[raw ticks, identities, scripts, protocol, goldens and cleanup](observations.tar.xz).
The collector source/binary is unchanged from the
[prior allocation artifact](../2026-11-09-runtime-allocation/observations.tar.xz),
with its SHA checked in this capture's receipt. Original adverse source comparisons
remain at their own revisions. Publication and tagging stay postponed.
