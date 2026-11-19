# B1148 invoice and annotated-rule profiles

Sampled the frozen B1146 reference `c76c59159` and live keyword-default
prototype `a7b43ff7c` on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 on November 19, 2026 UTC. Both full images were verified
against the same six CPython goldens. The selected scripts ran in fresh
warm workers with at least eight seconds of warmup and about five seconds
of CPU sampling each. Output and output-file checks passed around each
capture. The VM verified the persistent archive before removing the guest
fixtures.

| Script and image | Runtime ancestry | Native prefix and unpack | General binder | Keyword-default lookup | DictReader iteration |
| --- | ---: | ---: | ---: | ---: | ---: |
| Invoice reference | 999 | 370 | 0 | 0 | 195 |
| Invoice candidate | 1,013 | 322 | 0 | 0 | 201 |
| Annotated rules reference | 1,112 | 75 | 20 | 0 | 0 |
| Annotated rules candidate | 1,116 | 70 | 20 | 0 | 0 |

Ancestry groups overlap. The invoice calls two helpers with all positional
arguments supplied. Source and profiles agree with ordinary direct
positional binding and no sampled general binder. Names may be absent
from a sampled stack because of inlining or sample coverage, so the table
does not establish every executed branch. The annotated scripts exercise
general binding and have 150–183 samples with definition execution in
their ancestry. These captures identify work to investigate. They do not
quantify the effect of removing any stage.

The invoice slowdown in B1146/B1147 remains unexplained. The profile gives
no evidence that the invoice repeatedly executes the modified default
lookup and does not establish a JIT layout cause. Retain that elapsed and
CPU regression as debt when judging future whole-script changes.

[Timing evidence](../2026-11-19-keyword-default-record/README.md) ·
[Attribution](attribution.json) · [Raw traces](observations.tgz) ·
[Collection and verification](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
