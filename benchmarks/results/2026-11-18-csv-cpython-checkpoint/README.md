# CSV workloads against CPython

B978 measured commit `c14c7c92b`, frozen Lython core `12643cec`, on
2026-11-18 UTC. The comparison uses .NET 11 RC3 and stock CPython 3.13.16
with PGO/LTO, GIL enabled and no JIT. The quiet Azure F4as_v7 VM runs
Ubuntu 24.04.4 with four EPYC 9V45 vCPUs and 16 GiB RAM. Both engines
matched the output and file goldens.

The invoice has 4,096 data records and 127,031 input bytes. Its catalog's
nominal size is 1,024. The historical field-validation input has 4,128
data records and 128,183 bytes. Both include blank records. The text
control has 4,096 lines and 52,224 bytes. These actual sizes were verified
from the frozen fixture bytes. [Fixture sizes](fixture-sizes.json)

| Workload | Phase | Lython ms | CPython ms | Lython / CPython | Control spread L / C | Max IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| CSV invoice | Compile | 0.0983–0.0994 | 0.0926 | 1.06–1.07× | 1.17% / 0.09% | 1.10% |
| CSV field validation | Compile | 0.0250–0.0251 | 0.0265–0.0267 | 0.93–0.94× | 0.29% / 0.72% | 0.66% |
| CSV invoice | Warm | 7.0899–7.1164 | 6.3993–6.4997 | 1.09–1.11× | 0.37% / 1.57% | 1.62% |
| CSV field validation | Warm | 2.8205–2.8601 | 4.3128–4.3480 | 0.65–0.66× | 1.41% / 0.82% | 0.59% |
| CSV invoice | Compile-to-result | 7.0705–7.0892 | 6.2654–6.2784 | 1.13× | 0.26% / 0.21% | 0.78% |
| CSV field validation | Compile-to-result | 3.1456–3.1472 | 4.6632–4.6688 | 0.67× | 0.05% / 0.12% | 1.11% |
| Text normalization control | Warm | 0.7938–0.8038 | 0.5075–0.5106 | 1.55–1.58× | 1.25% / 0.61% | 1.62% |

Ratios use elapsed time and ranges cover all pairings of two process medians
per engine. Compilation includes compiled-owner disposal. Warm includes
mediated RAM-backed file I/O, output validation and previous-result cleanup.
Compile-to-result also includes compilation and disposal of the new compiled
owner. Separate process/phase medians need not add up. Startup, fixture reset
and request framing stay outside the guest clock. Wider process CPU includes
reset and protocol. The seven jobs finished in 319.57 seconds and the archived
results were verified after all workers joined and guest fixtures closed.

The same text control measured 0.725–0.737 ms under CPython in B973. It
measured 0.508–0.511 ms here, close to the earlier 0.501–0.503 ms observation.
The cross-collection movement remains unexplained. Treat all ratios as
diagnostic for this source and machine. The earlier validation fixture
measurements and source-pair CSV improvements remain independent.
[Earlier text observation](../2026-11-18-json-options-cpython/README.md) ·
[Earlier field validation](../2026-11-18-helper-validation-cpython/README.md) ·
[Lython source pair](../2026-11-18-csv-line-number/README.md)

The first staging check compared the text checkout bytes directly with the
Git blob. The check stopped on Windows line endings, before preflight or
remote changes. The corrected setup verifies line-normalized equality and
reads the committed catalog for the measured input.

[Timings and controls](summary.json) · [Frozen image](identity.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz)
