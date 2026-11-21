# B1254 and B1255 typed dictionary enumeration

Both runs compare the same `ea04028b5` reference image on the quiet
Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. The first prototype `7a3476198` put the CLR enumerator's
`Current` field in the shared cursor, widening the JSON encoder frame.
The retained refinement `6bf2947e8` keeps that field in the typed
enumerator and leaves the encoder frame layout intact. It is committed
at `92bf942ec`.

Typed dictionary iteration uses a value enumerator over the existing
versioned scan and drops the compiler-generated iterator object.
Interface callers still box an enumerator. The bridge preserves
namespace-backed traversal, structural mutation errors and live value
replacement. Ordinary sorted JSON, rendering and public projection
reuse the typed path.

| Campaign and warm workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| B1254 sorted JSON normalization | −3.47 to +0.89% | −3.09 to +1.67% |
| B1254 unsorted JSON control | −1.04 to +3.76% | −1.14 to +4.42% |
| B1255 sorted JSON normalization | −4.09 to −1.16% | −4.24 to −1.94% |
| B1255 unsorted JSON control | −0.89 to +3.29% | −1.44 to +2.11% |

Each range compares all pairings of two reference and two candidate
worker medians. Each worker contributes seven batches after a 16-second
warm-up. The confirmation reverses worker order and narrows the
production change while keeping the reference image and all inputs.
The sorted workload reads and writes 3,072 records and 189,441 bytes in
each direction. Its refinement improves both clocks across all pairings.
The unsorted control spans zero in both runs, and possible regression
remains. The initial candidate's unsorted process spread was 4.57%.

Local sorted CLR allocation falls 6.21% initially and 6.19% on the
retained refinement. The unsorted and validator diagnostics stay
within 0.04%. Accounted peaks match and the borrowed allowance returns
to baseline after cleanup. All three CPython goldens pass in both
32-file images for each source pair. Each candidate passes 1,942 public
and 443 white-box focused Release checks per platform.

Warm elapsed includes mediated RAM-backed I/O, stdout and output-file
validation, and disposal of the prior result. It excludes compilation,
process startup and final teardown. Both collections pass the quiet
checks and verify their archives under the lease after workers join.
The two runs provide Lython-to-Lython timing with CPython output goldens.

[First timings](summary.json) · [First observations](observations.tgz) ·
[Refined timings](narrow-frame/summary.json) ·
[Refined observations](narrow-frame/observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz) ·
[Refined source and checks](narrow-frame/checks-and-procedures.tar.xz).
