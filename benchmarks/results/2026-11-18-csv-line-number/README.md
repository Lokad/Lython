# CSV dictionary reader line numbers

B977 compared `12643cec` with commit `e507b4667` / `4eb89711` on the
quiet Azure F4as_v7 VM on 2026-11-18. Only the core differed between the
verified 32-file worker images. All three scripts matched the existing
CPython output and file goldens. Runtime was .NET 11 RC3 with normal GC
and tiering. The decoded fixtures contain 4,096 invoice records (127,031 B)
and 5,120 validated records (68,625 B). The catalog nominal size is 1,024
for each. The larger field-validation fixture measured against CPython is
a separate script and input.

The dictionary reader keeps its physical line number in an existing scalar
slot and publishes a stable Python object when the attribute is read. Its
ordinary header check uses the integer value directly. Guest-assigned objects
keep their identity, equality and truth protocols. Header, blank-record and
error paths publish the count at the same points as before.

| Script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Invoice, warm | −2.39 to −1.72% | −2.04 to −1.41% | 0.42 / 0.26% |
| Invoice, compile-to-result | −2.12 to −1.67% | −1.41 to +0.56% | 0.16 / 0.30% |
| Validated records, warm | −6.34 to −3.63% | −8.77 to −4.20% | 1.18 / 1.69% |
| Validated records, compile-to-result | −5.50 to −5.07% | −7.08 to −6.03% | 0.20 / 0.25% |
| Positional calls, warm control | −1.92 to −0.42% | −1.96 to −0.41% | 0.08 / 1.45% |

Ranges compare all pairings of two process medians per source. Negative
favors the candidate. Workers used eight-second rotated warmups and five
samples. Warm elapsed includes mediated RAM I/O, output checks and previous
result cleanup. Compile-to-result also includes compilation and compiled
owner disposal. Process CPU includes reset and protocol. Startup is separate.
The five jobs took 232.21 seconds. Both endpoints were quiet, workers joined,
guest fixtures were removed and the persistent archive hash matched.

Local validation measured 9.37% lower invoice allocation and 16.53% lower
validated-record allocation. The positional control allocation was identical.
Accounted peaks matched and allowance balances returned to baseline for all
three. Windows passed 118 white-box and 391 public CSV checks per actual
target. Another 93 focused checks per target passed on WSL, including the
CPython-derived assigned-value and observed-identity regression, suspended
reads, errors, blanks and ownership cleanup.

Retain the change for lower allocation and the validated-record improvement.
Invoice timing overlaps movement in the positional control and its fresh CPU
is mixed. The initial staging proof stopped before any VM mutation because
one old compiled replica appeared in a historical reference folder. The
corrected proof verified both aliases of the superseded, Git-archived binary
and preserved the newer candidate images.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and validation](checks-and-procedures.tar.xz)
