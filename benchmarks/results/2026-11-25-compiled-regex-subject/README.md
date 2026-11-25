# B1740 Reuse the constant regex subject's UTF-8 backing

The first constant match borrows the subject buffer under the normal scratch
admission. After the provider disposes its prepared subject, the validated
bytes can back the emitted literal under the existing compilation owner.
Repeated analysis of that same literal borrows the backing. Executable and
lowered literal constructors share its byte storage while preserving their
own Python wrappers. Short strings retain their inline representation.

| Workload | Subject bytes | Reference compile allocation | Candidate compile allocation | Retained admission, both |
| --- | ---: | ---: | ---: | ---: |
| Short Unicode match | 8 | 62,112 B | 62,112 B | 107,459 B |
| Unicode match | 264 | 63,168 B | 62,560 B | 119,747 B |
| Long Unicode match | 8,202 | 94,936 B | 78,440 B | 500,771 B |
| Long Unicode miss | 8,202 | 89,104 B | 72,608 B | 497,891 B |
| File-backed configuration manifest | | 72,328 B | 72,328 B | 82,304 B |
| Generated ordinary definitions | | 1,437,832 B | 1,437,832 B | 883,617 B |
| Shipment callback report | | 149,992 B | 149,992 B | 164,576 B |

This is a local Windows .NET 11 RC3 allocation diagnostic on November 25,
2026 UTC. It compares `d661e0d27` with the archived patch. Each source has
six warmup compilations and eleven current-thread allocation observations.
The measured operation includes compile and compiled-owner disposal under a
cancellable compile token. All allowance returns to baseline after disposal.

The separate provider trace records two analysis preparations and one runtime
preparation per regex case. For the three longer subjects, all three calls
see one backing in the candidate and three in the reference. The 8 B case
has three observations with distinct backings on both sources. Original text
and scalar/byte bounds match. This establishes byte-buffer reuse independently
of CPU or elapsed time.

Seven independent CPython output/file goldens pass for both 32-file images.
Windows .NET 11 and Ubuntu .NET 10 each pass 693 focused Release checks,
including typed capture positions, provider cancellation and refusal,
import cleanup, and both compiled literal consumers. The existing adapter
supplies the goldens. Adapter release qualification is tracked separately.

[Identity](identity.json) · [Allocation summary](allocation-summary.json) ·
[Provider trace](subject-trace.json) · [Procedures and raw evidence](procedures.tar.xz).
