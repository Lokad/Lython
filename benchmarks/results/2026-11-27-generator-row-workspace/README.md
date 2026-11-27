# B1905 Reuse callback storage while draining generator rows

The trial leases the existing function workspace before advancing a
same-command generator used as a `starmap` row. Exact positional
callbacks use the slots already filled by the source. This removes the
general call packet and its copy into native callback storage. A short
row passes its prefetched values to the existing binder for live
defaults. On overflow, the first excess yield and the remaining source
are consumed in order. Either path can suspend. The drained workspace
returns only after its final user or exception cleanup.

The reference is production source at `afc29bef6`. Both comparisons use
the same reference image. Each pair has 32 verified files and differs
only in the core DLL. All five independent CPython 3.13.16 output and
file goldens verify on each image. The added regression checks defaults
after a short row, successful exact rows, full source effects before an
arity failure, the following row, alias identity and awaited file
cleanup in both APIs. Its output also matches CPython 3.13.2.

Both candidate versions pass 469 public and 109 white-box focused
Windows .NET 11 Release checks. The allocation witness returns the
borrowed allowance to its original balance on every run. The refinement
also passes 437 public and 91 white-box focused checks on each of Windows
.NET 10 and Ubuntu WSL .NET 11.

## Initial candidate

The iterable shipment saves 147,384 B sync and 147,306 B async per
run on Windows .NET 11, about 6.1%. Peak admission falls by 16 B.
The generator-reduction control allocates 32,776 B more in both APIs.
The first candidate adds a positional cursor field to every execution
context. The refinement reuses the field for staged lists because the
generator selects one of these modes per advance.

Ubuntu WSL runs on CPU 27 with .NET 11 RC3. Two workers per image warm
for at least five seconds and contribute seven rotated batches each.
Ranges span all worker-median pairs. Warm elapsed includes mediated
RAM-backed I/O, golden checks and previous-result cleanup. Process CPU
also includes fixture reset and framing. Compilation and process startup
are outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable-row shipment | `Run` | −9.51% to +11.09% | −17.46% to +7.64% |
| File-fed map control | `RunAsync` | −6.88% to +8.08% | −3.27% to +3.67% |
| Wide-list shipment control | `RunAsync` | −4.94% to −1.10% | −4.25% to +0.70% |
| Bound shipment control | `RunAsync` | −4.03% to −0.27% | −7.17% to +1.98% |
| Iterable-row shipment | `RunAsync` | −3.44% to −2.57% | −3.45% to −0.47% |

Reference workers differ by 27.18% in sync iterable shipment CPU.
Two controls improve in elapsed with CPU spanning zero. The initial
async CPU result warrants a bounded check of the refined implementation.

[Allocation](initial/allocation-summary.json) · [Timing](initial/summary.json) ·
[Images](initial/identity.json) · [Observations](initial/observations.tar.xz) ·
[Patch and validation](initial/procedures.tar.xz).

## Refined candidate

The refinement shares the frame field between the position in a
positional drain and the arity of a staged list. It restores the
generator-reduction control's CLR allocation in both APIs. Iterable
shipment saves 164,070 B in sync and 164,014 B in async, about 6.8%.
Its peak remains 16 B below the reference. Other allocation and peak
admission observations match within 56 CLR bytes.

The second WSL comparison uses the same worker schedule and includes the
generator-reduction control in place of wide-list timing.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable-row shipment | `Run` | −2.52% to +3.98% | −2.50% to +1.12% |
| File-fed map control | `RunAsync` | −10.14% to +12.38% | −14.06% to +11.80% |
| Generator-reduction control | `RunAsync` | −36.19% to −15.41% | −20.62% to −13.50% |
| Bound shipment control | `RunAsync` | −6.30% to +3.32% | −1.45% to +37.04% |
| Iterable-row shipment | `RunAsync` | −3.37% to +2.04% | −1.75% to −0.04% |

The reduction timing and bound candidate's 35.29% same-image CPU
spread leave CPU attribution uncertain. The allocation witness confirms
the removal of a transport stage while the timing requires a quiet
control. Both WSL captures verify all 280 batches and their persistent
archives.

[Allocation](refined/allocation-summary.json) · [Timing](refined/summary.json) ·
[Images](refined/identity.json) · [Observations](refined/observations.tar.xz) ·
[Patch and validation](refined/procedures.tar.xz).

## Quiet comparison

The same refined images run on Azure F4as_v7, Ubuntu 24.04.4 and
.NET 11 RC3. The lane order and worker placement reverse. The same
elapsed and process CPU boundaries apply. Both images again verify
all five independent CPython outputs and files.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable-row shipment | `RunAsync` | −1.54% to +0.17% | −1.16% to +0.39% |
| File-fed map control | `RunAsync` | −0.54% to +3.91% | −0.73% to +2.59% |
| Iterable-row shipment | `Run` | −3.18% to −0.67% | −3.57% to +0.21% |

Keep the single-workspace route. The allocation reduction holds and sync
elapsed is lower on the quiet VM. All quiet CPU ranges and async elapsed
span zero. The first WSL async CPU improvement remains in its record.

All 84 quiet batches verify. Both machine gates pass, every worker
joins, and the persistent archive verifies under the lease. Staging
reuses 62 immutable files and reclaims two VM DLL replicas whose complete
images and observation archives were verified locally. Available space
after staging is 17,670,144 bytes, above the existing floor.

[Timing](quiet/summary.json) · [Images](quiet/identity.json) ·
[Storage custody](quiet/stage-custody.json) · [Terminal status](quiet/terminal-status.json) ·
[Observations](quiet/observations.tar.xz) · [Validation and procedures](quiet/procedures.tar.xz).
