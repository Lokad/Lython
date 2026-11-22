# B1357 Factory CPU attribution after combined capture construction

Profiled production-equivalent `2f9f818b9` on November 22, 2026 UTC on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The frozen
32-file image was built at `9cbcb2437` and matches production runtime
and test source. Linux perf collects all-thread samples at 199 Hz.
Each phase warms independently. Warm includes output verification
and prior-result disposal. Time to result adds compilation and
code-owner disposal. The scripts use no guest files.

| Sample group | Mixed warm | Mixed time to result | Fully supplied warm |
| --- | ---: | ---: | ---: |
| All process samples | 1,140 | 1,115 | 1,201 |
| Unmapped leaf | 80 | 85 | 71 |
| Frontend ancestry | 0 | 22 | 0 |
| Closest runtime method is reserve | 65 | 72 | 0 |
| Callable construction reserve caller | 62 | 70 | 0 |
| General binder ancestry | 33 | 26 | 0 |
| Scope promotion ancestry | 0 | 0 | 0 |
| Closest runtime method is instruction prefix | 143 | 180 | 320 |
| Closest runtime method is builtin arithmetic | 59 | 53 | 240 |
| Closest runtime method is result consumption | 55 | 45 | 35 |

The separately resolved capture reservation is absent. Most remaining
explicit admission samples in the mixed factory reach callable
construction, which now funds the cell or prepared defaults when
eligible. This profile supplies no evidence for widening shared frame
storage or removing the remaining live callable reservation. In the
fully supplied script, instruction prefix and builtin arithmetic
account for 560 of 1,201 closest-runtime samples. Operand
classification and transport warrant inspection before another call
layout. The groups can overlap, and the samples do not estimate a
speedup or measure a performance difference from B1353.

Both CPython goldens pass against the actual worker image. All three
phases complete, both quiet gates pass and every worker joins. Raw
traces were copied to persistent storage and verified under the lease
before the fixture mount was removed. Staging retires only a duplicate
working DLL with exact bytes saved in the B1355 archive and a complete
local image.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Previous profile](../2026-11-22-post-default-first-use-profile/README.md).
