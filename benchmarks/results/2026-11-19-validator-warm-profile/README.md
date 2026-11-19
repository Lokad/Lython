# B1132 validator and manifest warm execution profile

This profile uses the retained Lython revision `a472eb2a4` on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC.
The run reuses the complete 32-file Lython image and representative scripts
from [B1130](../2026-11-19-postfix-cpython-checkpoint/README.md). The workers
verified both CPython goldens and all image files before and after sampling.
Samples cover warm execution, mediated file I/O, verification and cleanup.

| Samples or nearest runtime method | Validator | Manifest |
| --- | ---: | ---: |
| All samples | 988 | 1,013 |
| Samples with Lython runtime ancestry | 654 | 647 |
| Fixture samples outside those runtime stacks | 317 | 357 |
| Opened byte file acquisition | 96 | 238 |
| Integer text parsing | 86 | 0 |
| Native call dispatch | 62 | 6 |
| Host operation await and translation | 15 | 23 |

The acquisition frames can contain inlined benchmark host work, including
real path resolution and `FileStream` construction. They also contain the
runtime's acquisition wrapper, which awaits a completed `ValueTask` for
this host. An implementation comparison can test whether collapsing the
completed acquisition path helps whole scripts while preserving mediated
path resolution, terminal refusal, cancellation and uncancelled cleanup.
Integer parsing also warrants a separate validator investigation. Unmapped
frames limit attribution, and the counts estimate neither removable CPU
nor comparative performance.

The quiet gates passed and both workers joined. The VM archived and
verified both RAM traces before removing guest fixtures. Local retrieval
verified the persistent archive and its members.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
