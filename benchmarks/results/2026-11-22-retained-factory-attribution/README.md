# B1310 retained factory and helper CPU attribution

Profiled `4337b7765` on November 21–22, 2026 UTC on the dedicated
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The 32-file
image is the exact B1307 worker. Each workload ran in a separately
warmed process. Samples cover complete batches including captured
output, request handling and prior-result disposal. Compilation,
startup and teardown are outside the sampled batches.

| Nearest resolved runtime method or sample class | Eager factories | Postponed factories | Ordinary helper |
| --- | ---: | ---: | ---: |
| All process samples | 1,501 | 1,475 | 1,519 |
| Unresolved leaf | 87 | 100 | 102 |
| General instruction prefix | 197 | 186 | 389 |
| Memory admission | 185 | 173 | 0 |
| Native definition operation | 165 | 125 | 0 |
| Eager annotation reads | 114 | 0 | 0 |
| General binder, inclusive | 16 | 42 | 0 |
| Scope promotion, inclusive | 0 | 0 | 0 |

The two factory scripts each create 4,096 callables and make one
positional and one keyword-only default for 2,048 of them. The nearest
resolved admission callers include callable and annotations, default
storage and lexical capture. The ordinary helper defines its function
once and supplies every argument. Admission and binding sample counts
do not estimate recoverable CPU. Samples and inclusive call groups
include useful operations, and inlining limits attribution.

Select a deferred single positional default that holds its existing
identity directly in the admitted callable slot. The candidate should
eliminate both the positional tuple and its slot array until Python
observes the tuple. It should keep wide defaults, ordinary tuples and
the supplied-argument path intact. Preserve first refusal, alias
funding through replacement, suspended default evaluation and exact
tuple identity. Include the observer and fully supplied controls.

All three CPython goldens match before staging and each worker
verifies output before and after capture. Both machine gates pass.
Workers join and the persistent traces verify before RAM fixture
removal. These profiles provide phase attribution.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Image verification and procedures](procedures.tar.xz).
