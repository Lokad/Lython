# B1353 Factory CPU attribution after direct keyword defaults

Profiled production `285628a43` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The frozen
32-file image matches the production runtime source. Linux perf
collects all-thread samples after independent warmup for each phase.
Warm execution includes output verification and prior-result disposal.
Time to result adds compilation and code-owner disposal. The scripts
use no guest files. Startup and final teardown are separate.

| Resolved sample group | Mixed warm | Mixed time to result | Mixed compilation | Fully supplied warm |
| --- | ---: | ---: | ---: | ---: |
| All process samples | 1,156 | 1,099 | 1,108 | 1,206 |
| Unmapped leaf | 71 | 82 | 196 | 87 |
| Compiler ancestry | 0 | 27 | 1,082 | 0 |
| Closest runtime method is reserve | 119 | 111 | 8 | 2 |
| Capture construction reserve caller | 60 | 56 | 0 | 0 |
| Callable construction reserve caller | 56 | 48 | 0 | 0 |
| General binder ancestry | 39 | 29 | 0 | 0 |
| Scope promotion ancestry | 0 | 0 | 0 | 0 |

Capture and callable storage remain major uses of admission on mixed
definitions. The next source pair should construct the common first
local cell and its function together. That design can displace the
general capture walker for a prepared singleton and the separate cell
admission. Preserve cell sharing, first refusal, and eager annotation
preflight. B1331 previously combined admission with general capture
discovery still present and its time to result increased. The current
samples guide the design. They do not quantify removable work or
establish a time comparison against B1340.

Both CPython goldens pass against the actual worker image. All four
phases complete, the machine checks pass before and after, and every
user joins. Raw traces were copied to persistent storage and verified
under the lease before the fixture mount was removed. Staging retires
only duplicate working DLLs with the exact bytes saved in the B1348
archive and a complete local image.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Earlier profile](../2026-11-22-current-first-use-factory/README.md).
