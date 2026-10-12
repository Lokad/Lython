# Complete-script failure and reclamation gates

Eight public cases run the unchanged CSV/text/JSON/file scripts synchronously
and through a genuinely delayed host. Bad CSV amount conversion raises ValueError;
malformed JSON raises JSONDecodeError. Missing text input and missing input
directory raise the documented generic-host RuntimeError. No report file or
stdout is published. Async completion is checked before output-existence calls,
so those checks cannot supply the apparent yield.

The independent CPython verifier now checks 16 jobs: 12 existing positive/empty
fixture jobs and one negative variant per script. Its concrete filesystem reports
FileNotFoundError for missing inputs. This is recorded separately from Lython's
mediated-host failure contract; the gate does not assert equal host error types.

The first delivery's Windows CI failed an existing discarded-integer test while
the workload itself succeeded: peak 4,516,344 bytes with an 8 MiB cap exceeded an
arbitrary 4 MiB peak assertion. That shape did not reliably force reclamation and
depended on ambient CLR collection timing. The corrected gate tightens the cap
to 1 MiB for cumulative payload above 4 MiB. Dropped loads must finish; a matched
retained-load control must reject under the same cap. Both stay within the cap.
The earlier failure is preserved, not replaced by a CI rerun.

All 55 affected checks pass in Debug and Release. Evidence records the committed
producer, exact binaries, test receipts and independent Python outcomes. The prior
10,458-test Debug qualification remains evidence for the unchanged production
source; these additional test/driver changes do not claim a new full Debug run.

No production source, memory contract, forced-GC policy, stack/thread machinery
or host exception translation changes. No performance or latency improvement is
claimed. Owned-resource closure, broader cancellation/alias controls, host-impact
qualification and forced-GC independence remain explicit milestone gates.
