# B1521 explicit-float CSV against CPython

Retained Lython `65d4207fb` and stock CPython 3.13.16 process the same
4,096-record, 139,299-byte CSV fixture. The comparison ran November 23,
2026 UTC on a quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.

| Phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm | 3.38746–3.58906 ms | 2.97522–3.03381 ms | 1.117–1.206× |
| Compile to result | 3.40395–3.42891 ms | 3.14679–3.20315 ms | 1.063–1.090× |
| Compilation only | 0.04479–0.04501 ms | 0.07204–0.07207 ms | 0.621–0.625× |

Ratios divide Lython elapsed by CPython elapsed and span the four
pairings of process medians. Warm worker spread is 5.95% for Lython and
1.97% for CPython. Compile-to-result spread is 0.73% and 1.79%.
[B1520](../2026-11-23-short-csv-utf8-slots/README.md) measures the
change against the matched Lython reference. B1521 is the external
diagnostic for this report.

Warm reuses the compiled script. Both execution phases include mediated
or native RAM-backed reads and writes, verification, and prior-result
disposal. First use adds compilation and code-owner disposal. Compilation
alone covers front-to-back compilation and disposal. Worker startup,
fixture reset and request framing are outside elapsed. Process CPU keeps
the wider request costs.

Both engines warm with two workers for eight seconds per worker and
produce seven complete rotated batches per worker per phase. All **84**
batches match the goldens. The complete frozen image and both quiet
gates verify. Workers join, fixtures are removed, and the raw archive
verifies under its lease. The local preparation escaping correction is
preserved with the procedures.

[Image identity](identity.json) · [Summary](summary.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
