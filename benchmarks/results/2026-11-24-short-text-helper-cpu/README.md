# B1556 attribute the short report's helper and word preparation

The three traces use the same 3,072-line input and produce the same
output. The shipped source updates the maximum line width through a
nonlocal binding. The second script moves that update to the caller
and keeps a helper that returns the words. The third does both in the
caller. The frozen Lython image is `eabbf02ed`, whose runtime and tests
match `000ab0632`. Each script passes its CPython golden check.

| Nearest resolved runtime group | Nonlocal helper | Width in caller | Fully inline |
| --- | ---: | ---: | ---: |
| Entry and operand movement | 93 | 83 | 1 |
| Native instruction dispatch | 280 | 204 | 149 |
| Word decoding and casing | 151 | 163 | 170 |
| Lookup and accumulation | 163 | 148 | 187 |
| Admission or retirement | 91 | 105 | 103 |
| Host I/O | 72 | 74 | 83 |
| Other runtime | 279 | 370 | 403 |
| Total with a resolved runtime ancestor | 1,129 | 1,147 | 1,096 |

Moving the width update shifts instruction dispatch into the caller.
Entry and operand movement still accounts for 83 nearest runtime
samples. `PrepareCasedWhitespaceIteration` alone accounts for 115, 120
and 131 samples. One third of the input lines contain no words.
Check whether proving that case before renting word scratch can
remove an ownership step shared by all three scripts.

The collector samples at 199 Hz for six seconds after eight guest
seconds of warm-up on the dedicated Azure F4as_v7 running Ubuntu
24.04.4 and .NET 11 RC3. The active threads produce 1,211, 1,229 and
1,200 samples, including 157, 157 and 192 unmapped leaves. The scripts
complete 4,736, 4,864 and 6,144 invocations in their respective
windows. Those counts and the unequal sample resolution limit the
comparison to attribution. The controlled phase measurements are
recorded separately.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Golden checks](verification.json) · [Raw traces](observations.tar.xz) ·
[Collector, analysis and code tapes](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json) ·
[Current CPython phases](../2026-11-24-json-text-cpython/README.md).
