# B1036 and B1037 native keyword copy

Prototype `98c4f2954` checks trailing keyword mappings in Python
order and then uses the existing native dictionary copy routines to
produce the callee's new dictionary. It removes entry-by-entry
insertion on the validated path, preserving key and value identity
and later independent dictionary mutation.

The prototype stays private. Mutable bound-method forwarding ran
1.52–2.92% faster warm with less CPU in B1036. Its compile-to-result
timing was mixed in both runs. Expanded forwarding used 1.10–1.35%
more CPU in B1037 while elapsed spanned zero. The modest warm gain
and unchanged allocation do not justify that tradeoff on the primary
expanded report.

| Collection and workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1036 Expanded forwarding, warm | −6.56…+0.48% | −6.96…+0.88% | 6.34 / 1.13% |
| B1036 Expanded forwarding, compile-to-result | −1.56…+1.08% | −1.38…+0.35% | 1.08 / 1.59% |
| B1036 Mutable bound-method forwarding, warm | −2.92…−1.52% | −3.56…−1.80% | 0.87 / 0.57% |
| B1036 Mutable bound-method forwarding, compile-to-result | −0.85…+3.98% | +0.37…+3.60% | 0.95 / 3.88% |
| B1036 Explicit dictionary copy, warm | −1.12…+0.17% | +0.62…+1.54% | 0.70 / 0.61% |
| B1036 Positional control, warm | −1.33…−0.21% | −2.81…−1.23% | 0.42 / 0.71% |
| B1037 Mutable bound-method forwarding, compile-to-result | −1.22…+2.15% | −1.06…+3.02% | 0.13 / 3.27% |
| B1037 Expanded forwarding, warm | −1.01…+1.86% | +1.10…+1.35% | 0.45 / 2.44% |
| B1037 Explicit dictionary copy, warm | −2.84…−0.14% | −3.14…+1.08% | 2.05 / 0.71% |

Both runs compared `e93356adc` and `98c4f2954` on the quiet Ubuntu
24.04.4 Azure F4as_v7 with .NET 11 RC3 on 2026-11-19 UTC. Frozen core
hashes start with `b21ca2e4` and `9fe26c48`.
Ranges cover all four pairings of process medians. Negative values
indicate less time. Reference elapsed spread reached 6.34% on the
original expanded warm run. The repeat used identical case rows and
both frozen images with a changed worker order. The explicit
dictionary-copy control uses the same report input and goldens,
passing a fresh `dict(fields)` as an ordinary positional argument.

Local allocated bytes and accounted peaks were effectively unchanged,
and each local comparison returned the allowance to baseline.
Windows .NET 11 and WSL .NET 10 each passed 74 public and 33 white-box
focused checks. A test-only LF normalization fixed the initial raw
string assertion failures. The measured runtime was identical before
and after that correction. The three native dictionary shape assertions
agree with CPython 3.13.

Both 32-file images verified before staging. All 180 timed batches
and both quiet gates passed over 276.97 and
140.38 seconds. Workers joined and raw archives
verified under the lease. Warm includes mediated RAM-backed I/O,
goldens and prior-result cleanup. Compile-to-result includes
compilation and disposal of the compiled owner. Startup, fixture reset,
request framing and final cache teardown are outside guest elapsed.
Process CPU covers the wider request.

[B1036 summary](summary.json) · [B1036 raw](observations.tgz) ·
[B1037 summary](confirmation/summary.json) · [B1037 raw](confirmation/observations.tgz) ·
[Prototype image](measured-core.dll.gz) · [Sources, checks and procedures](checks-and-procedures.tar.xz).
