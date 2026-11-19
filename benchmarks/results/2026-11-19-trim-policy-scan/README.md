# B1064 inline ASCII trim with a dedicated whitespace scan

R634 keeps default whitespace trimming on its dedicated scan. Explicit
`strip`, `lstrip` and `rstrip` share a directional scan and use an inline
ASCII membership mask. The admitted rune hash set holds non-ASCII members
only. An unchanged string keeps its identity. A changed string uses the
existing funded slice path.

The comparison measures `43c9f2c38` versus `ac175cc27`, whose runtime and
trim tests match retained `12fabffb1`. Both complete 32-file images passed
five CPython goldens. Windows .NET 11 and Ubuntu under WSL .NET 10 each
passed 524 focused Release cases, including Unicode scratch refusal,
cancellation, exact builtin and subclass behavior, and result ownership.
Local allocation fell 7.01% on expanded forwarding and 35.20% on mixed
Unicode trim. Accounted peaks were unchanged on forwarding and 128 B
lower on mixed Unicode. Both images returned the budget to baseline.

The same five-case catalog as B1063 ran on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC. The ranges cover all four
combinations of duplicate process medians. Negative change favors R634.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Expanded forwarding | Warm | −3.39…−3.06% | −4.37…−3.69% |
| Expanded forwarding | Compile-to-result | −3.45…−1.09% | −2.83…−2.17% |
| Mixed Unicode trim | Warm | −14.78…−14.07% | −14.71…−12.05% |
| Default-whitespace normalization | Warm | −1.80…−1.30% | −2.59…0.00% |
| Shipment JSON selector report | Warm | −1.21…+0.67% | −1.55…+1.96% |

The explicit-trim reductions recur and the earlier adverse controls do
not. Forwarding's warm result differs from B1063's inconclusive result.
These measurements compare two Lython revisions. CPython supplies output
validation for both.

Warm includes mediated I/O, output validation and cleanup of the previous
result. Compile-to-result adds compilation and disposal. Process CPU has
a wider boundary. Both quiet gates and 100 timed goldens passed in 231.43
seconds. Workers joined, fixtures were removed and archival verification
finished under the lease. The initial preflight stopped before launch due
to disk headroom. The rerun reserved 1,792 KiB for the measured archive,
with the same staging and archival floors. Both preflights are preserved.

[Initial B1063 comparison](../2026-11-19-inline-trim-membership/README.md) ·
[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
