# JSON encoder delivery

Corrected run 44332004310 passed at `c3d21ab45`. Windows and Ubuntu each
passed ordinary Release tests on .NET 11 and .NET 10, the original stress
scales on both targets, and the installed Release package consumer. Each
ordinary target passed 4,871 white-box and 18,054 public tests. Stress
passed 14 white-box and 406 public tests per target.

The bundle verifies 16 test summaries and 2,393 committed inputs. All 588
core production source files match the source used to freeze encoder image
`33025cee`, allowing for line endings. The archived image identifies the
measured binary. This run qualifies the combined JSON options and encoder
changes and the corrected immutable-repetition test. The later frontend
prototype has a separate check boundary.

[Green run](https://github.com/Lokad/Lython/actions/runs/44332004310) ·
[Qualification](qualification.json) · [Logs](run-logs.zip) ·
[Original Ubuntu failure and focused correction](../delivery-attempt/README.md) ·
[Encoder measurements](../comparison/README.md)
