# B1159 factory, forwarding and formatting attribution

The quiet Azure F4as_v7 ran the `f1593aaf5` core with .NET 11 RC3 on
Ubuntu 24.04.4 on November 20, 2026 UTC. The whole 32-file image and
four independent CPython goldens verified before staging. Each worker
ran at least eight seconds of warmup and verified its output and files
before and after about five seconds of sampling.

| Runtime stacks | Eager rules | Nested factories | Expanded forwarding | Shipment selectors |
| --- | ---: | ---: | ---: | ---: |
| Lython runtime ancestry | 1,058 | 1,030 | 961 | 949 |
| Nearest frame is the instruction prefix | 228 | 205 | 116 | 89 |
| Nearest frame is reservation | 102 | 94 | 54 | 52 |
| Default storage or annotation attachment in ancestry | 159 | 106 | 0 | 12 |
| Generic binder in ancestry | 16 | 19 | 0 | 0 |
| Scope promotion in ancestry | 0 | 0 | 0 | 0 |
| Formatting in ancestry | 0 | 0 | 72 | 290 |
| Split sizing or replay in ancestry | 0 | 0 | 59 | 0 |
| Keyword mapping ownership in ancestry | 0 | 0 | 63 | 0 |

Sampled ancestry can overlap and miss inlined work. The counts describe where
the worker spent CPU in whole warm requests, including mediated I/O, fixture
handling, goldens and prior-result cleanup. A timed source pair will establish
the performance effect of any change.

Both reports still pass ordinary `str.format` calls through generic argument
packaging. Source inspection found that the original evaluated operand slots
already have the values and immutable call-site keyword names. A prototype
can borrow them through formatting and remove the intermediate packet,
including its owned allocation for wide calls. Keep the existing renderer,
expanded-call ownership and the actual callback suspension path. Compare
both reports and named formats with an unaffected control before retention.

The factory samples continue to show definition storage, annotation work and
instruction dispatch. Frame promotion is outside the observed hot stacks.
The prior measured call arenas and call-control designs remain private.

The first image check caught a different benchmark worker binary after a
nonincremental rebuild. The recorded image uses that rebuild's current core
and the prior checkpoint's exact worker, adapter and dependency files.
Tracked worker and adapter sources are identical between the two revisions.
The final image and catalog verified using the actual worker combination.

Both quiet gates passed. Workers joined, guest fixtures were removed and
the persistent archive verified under the lease.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Lease receipt](archive-receipt.json).
