# Current text and validated-CSV allocation attribution

R134, 2026-11-09, committed9db554b3/core36fd. Existing D36 allocation-tick
collector and comparison worker/catalog/protocol reused unchanged. Two original
complete scripts,256warmup jobs then512checked capture jobs each, normal GC/tiering,
Windows x64/.NET11RC3. This is not elapsed or CPU evidence and uses no forced
collection, rundown, heap snapshot or guest-object inventory.

| Marked-window allocation-tick type | Text helpers | Validated CSV |
| --- | ---: | ---: |
| PyString |35.13% |26.56% |
| Boxed BigInteger |24.63% |23.66% |
| Byte[] |13.56% |2.82% |
| PyList |12.05% |unobserved |
| TemporaryMemoryReservation |8.81% |10.52% |
| Object[] |5.82% |9.36% |
| PyDict |unobserved |11.62% |
| Boolean |unobserved |9.05% |
| PyTuple |unobserved |3.92% |
| PendingJump |unobserved |2.45% |

Each tick assigns an allocation interval to its threshold-crossing type. These
associations are statistical, not exact per-type bytes or object counts. All
threads and raw ticks remain. The principal allocation thread has7168/7170text
and18175/18178CSV ticks and allocates guest-specific PyString values; that does
not establish CPU dominance or an independently observed thread name. Boolean
samples are24B boxes on CSV's same principal allocation thread. Caller/reset/
protocol buffers remain part of the complete request window. Compile, warmup and
final teardown are outside; mediated I/O, validation and prior-result cleanup
are inside. One CSV tick lies outside the marked capture window and stays raw.

7170text/18178CSV ticks, zero lost. Before/after verification, full output/file
goldens, cooperative collector stop, worker Closed/exit0, joined processes and
removed private fixtures pass under a120s outer ownership bound. No new production
change for this diagnostic, no VM timing or qualification replay.

The next selected R135 source gate concerns native Boolean result construction:
source returns repeatedly box bool into object, while Python Boolean values have
singleton identity. Inspect actual source/IL and preserve arbitrary rich protocol
results before replacing that complete result boundary with immutable boxes.
Integer representation and text ownership remain unchanged; neither their
allocation shares nor older CPU profiles justify a universal tagged-value/VM/JIT
or another shared-window prototype. Native Boolean runtime work has separate
correctness, allocation and controlled performance qualification.

[All marked-window types](assessment.json), [all thread associations/object sizes](thread-assessment.json),
[selection before capture](selection.md), [original ticks/worker identities/goldens/collector source/binary/commands](observations.tar.xz).
Source36fd and its unchanged auxiliary worker are also preserved in the
[prior source comparison](../2026-11-09-static-call-shape/observations.tgz).
