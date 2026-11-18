# Compact UTF-8 keyword layout delivery

Release CI run [44393944382](https://github.com/Lokad/Lython/actions/runs/44393944382)
passed at `604dc52db9155dfd265b5fc75dfcc330cb877bc5`.

Windows and Ubuntu each passed 4,889 white-box and 18,078 public ordinary
tests, plus the 14 white-box and 406 public original-scale stress tests,
per .NET 10 and .NET 11 target. Installed Release package-consumer
verification passed on both platforms.

Applying the archived compact-layout patch to the preceding qualified
revision reproduces the dispatched runtime, test and benchmark worker
sources. The measured source pair has separate frozen image hashes.

[B998 benchmark](../README.md) · [Source proof](source-proof.json) ·
[CI checks](qualification.json) · [Raw CI logs](run-logs.zip)
