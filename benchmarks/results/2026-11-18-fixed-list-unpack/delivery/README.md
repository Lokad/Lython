# Borrowed expanded calls and direct fixed-list unpack

Release CI run [44382234351](https://github.com/Lokad/Lython/actions/runs/44382234351)
passed at `98da93f7dfe67a65920d0cb91435f7d23d313b0f`. This includes the
borrowed expanded-call packet and reservation-overlap correction measured
in B992 and direct fixed-list unpack measured in B994.

Windows and Ubuntu each passed 4,883 white-box and 18,074 public ordinary
tests per .NET 10 and .NET 11 target. Each also passed the original-scale
14 white-box and 406 public stress tests per target, and the installed
Release package-consumer checks.

Applying the archived source patches to the previous qualified revision
reproduces the dispatched code, tests and benchmark worker sources.
The quiet performance comparisons keep their separate image identities,
controls and uncertainty.

[Borrowed expanded calls](../../2026-11-18-borrowed-expanded-call/README.md) ·
[Fixed-list unpack](../README.md) · [Source proof](source-proof.json) ·
[CI checks](qualification.json) · [Raw CI logs](run-logs.zip)
