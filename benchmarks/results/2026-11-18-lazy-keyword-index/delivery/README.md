# Deferred keyword index and lowered call ownership

Release CI run [44387260966](https://github.com/Lokad/Lython/actions/runs/44387260966)
passed at `0f48fcbbf164a0f94f20b5edb22cf0f41bee5a28`.

Windows and Ubuntu each passed 4,889 white-box and 18,076 public ordinary
tests, and 14 white-box and 406 public original-scale stress tests, per
.NET 10 and .NET 11 target. Installed Release package-consumer checks passed
on both platforms. The exact dispatch sources and test patches were
compared against the previous qualified revision.

The defer-index speed and allocation measurements belong to B996. The
lowered-call correction has focused ownership and refusal regressions.

[B996 comparison](../README.md) · [Source proof](source-proof.json) ·
[CI checks](qualification.json) · [Raw CI logs](run-logs.zip)
