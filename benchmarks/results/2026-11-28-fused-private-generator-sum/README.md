# B1972 Reduce the adjacent private generator and builtin sum together

The exact list, tuple or range generator is prepared and reduced at its
producer using the existing executable dispatcher. The runtime checks the
evaluated builtin and retains both the generator and call admission
boundaries. The temporary executable operand, its 256 B charge and the
intermediate argument binder are removed. Full frames still receive the
original instruction position, iterator and live storage when needed.

Against `6ae6f2fa8`, each 2,048-row tuple, list or range shipment
allocates 360,392–360,622 fewer CLR bytes in both APIs, about 352 KiB.
Execution peak falls by 256 B for those cases. The 128 retained iterator
aliases save 22,528–22,584 CLR bytes at the same peak. The explicit loop
allocates within 12 B of the reference. Compiled admission and growth
requests match, and all runs return the host allowance to baseline.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 411 public and
41 white-box focused Release checks. Both frozen 32-file worker images
pass five CPython 3.13.16 output and file goldens locally and on the VM.
The candidate is the archived patch over `3bc32973a`, whose source and
build settings match `6ae6f2fa8`. The worker images differ in the core
DLL alone.

## Warm async comparison

November 28, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds and take seven
rotated batches each. Elapsed includes mediated RAM-backed I/O, output
and file verification, and previous-result disposal. Process CPU also
includes request framing and fixture reset. Compilation and startup are
outside both timings. Ranges span pairings of worker medians.

| Workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Tuple generator shipment | −14.29% to −10.65% | −14.80% to −10.37% |
| List generator shipment | −12.49% to −10.76% | −13.34% to −8.99% |
| Explicit loop control | −1.28% to +3.72% | +0.28% to +3.73% |

The explicit loop's CPU increases in the first collection. A repeat with
the same images and reversed worker order spans −4.17% to +8.73% CPU and
−3.83% to +8.20% elapsed. The control effect remains inconclusive. Across
both collections, all 112 timed batches and quiet gates pass. The
allocation witnesses also cover sync execution and range sources, for
which these collections have no timing.

Two local warmed EventPipe captures of the candidate verify 97 sampled
batches. The private generator reduction accounts for 30.5% inclusive
of resolved Lython samples on the tuple workload. The prefix accounts
for 25.6%, preparation for 7.5%, and the shared instruction loop is
nearest for 11.0% within that private region, as a fraction of all
resolved samples. Short integer accumulation accounts for 0.3%.
On the explicit workload, the native instruction loop is nearest for
37.1%. Inclusive shares overlap and these captures have different
denominators. The quiet comparison above supplies elapsed and CPU.

[Timing summary](summary.json) · [Worker images](identity.json) ·
[Allocation](allocation-summary.json) · [Admission](admission-summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Control repeat](control-summary.json) · [Repeat observations](control-observations.tar.xz) · [Control checks](control-procedures.tar.xz) ·
[Managed attribution](profile-managed-attribution.json) · [Raw captures](profiles.tar.xz) ·
[Patch and checks](procedures.tar.xz).
