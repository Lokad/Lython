# B1053 independent capture-cell ownership

Retained `68a7fc465` extends live-module ownership to functions that use
independent executable cells. The compiler checks the body and descendants
for dynamic lexical namespace use. Eligible cells keep their live values
without a frame reference and acquire retained funding when they escape.
Nonlocal declarations, class-cell discovery, lowered patterns and other
dynamic namespace users keep their existing frame and funding.

The captured-rules workload creates and invokes rules over 2,048 records.
Its binding cells are shared between scopes, including a lambda returned
through a nested helper. Local allocation fell **6.22%**, from 5,271,176
to 4,943,496 bytes. Accounted peak fell **46.86%**, from 4,474,968 to
2,377,816 bytes. Other cases' allocation changed at most 0.03%. Allowances
returned to baseline in every verified local run.

| Workload | Boundary | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −2.84…−0.78% | −1.96…−0.72% | 0.65 / 1.45% |
| Captured rules | Compile-to-result | −2.69…−0.95% | −3.45…−1.96% | 0.70 / 1.08% |
| Annotated rules | Warm | +0.01…+0.18% | 0.00…+2.40% | 0.12 / 0.06% |
| Expanded forwarding | Compile-to-result | −1.52…−0.29% | −1.77…+1.09% | 0.50 / 0.75% |
| Positional calls | Warm | −1.18…−0.32% | −1.55…+0.83% | 0.41 / 0.46% |

The two 32-file images, `48309e4e3` and `6165aa771`, ran on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Their sources are `56d7e329d` and `68a7fc465` respectively. Both images
passed the seven-case CPython-golden catalog. Ranges cover the four pairings
of process medians. Negative changes favor the candidate. Annotated-rule
elapsed stays within 0.18%, with higher CPU possible. The other controls
move slightly in elapsed and span zero in CPU. Retain the independent
ownership and measured memory reduction.

The local Windows .NET 11 and WSL .NET 10 selection covered 3,869 cases
each. Both passed after supplying the CLI probe, Python command and one
workflow fixture for the isolated WSL build. The 320-definition regression
preserves a captured rebind through two factories and observes updated
module globals. It completes at 256 KiB on both APIs and leaves the
allowance at zero. The reference refuses that same regression. Existing
nonlocal, class-cell, match and import checks retain their coverage.

The five jobs took 229.47 seconds. All 100 measured batches passed their
output checks. Both quiet gates passed, workers joined, guest fixtures
were removed, and the archive verified under the lease. Warm includes
mediated I/O, golden verification and prior-result cleanup. Compile-to-result
adds compilation and compiled-owner cleanup. Startup, fixture reset, protocol
and final cache teardown fall outside elapsed. Process CPU has a wider
boundary. Full platform, stress and package qualification awaits the next
checkpoint.

[B1053 summary](summary.json) · [Raw observations](observations.tgz) ·
[Exact core](measured-core.dll.gz) · [Sources, checks and procedures](checks-and-procedures.tar.xz).
