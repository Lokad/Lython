# B1751 Remove the wide JSON integer string copy

For validated default JSON integers outside the signed-64-bit path, shorter
tokens now use at most 128 stack characters on both targets. .NET 11 passes
longer tokens directly to the BCL's UTF-8 parser. .NET 10 keeps the CLR-string
path for longer tokens. These routes share the same exact decimal parser and
the existing preflights for magnitude and opaque CLR scratch. The CLR call
runs synchronously to completion between cancellation checks. Numeric hooks
keep the original token text. [BigInteger.Parse reference](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.biginteger.parse?view=net-11.0).

| Complete JSON report | Input | Local CLR bytes before → after | Peak admitted bytes before → after |
| --- | ---: | ---: | ---: |
| 4,096 values of 40 digits | 270,336 B | 2,997,940 → 2,571,814 | 4,341,968 → 4,341,968 |
| 16 values of 4,096 digits | 65,952 B | 401,708 → 270,176 | 211,381 → 211,381 |
| Ordinary 1,024-record report | 110,592 B | 1,599,888 → 1,600,062 | 3,822,014 → 3,822,014 |

The Windows .NET 11 diagnostic compares `0a0e32c16` with the archived patch.
Allocation falls 14.21% and 32.74% on the affected reports. The three matched
admission peaks and restored balances preserve the existing funding contract.
Both complete frozen images match independent CPython file and output goldens.

The first byte-span-only prototype uses 98,298 B more on the 40-digit report.
An isolated parser check reproduces the fixed allocation difference and shows
that a small stack bridge avoids it. The first implementation also saves
allocation on the 4,096-digit fixture. The broader radix-chunk trial remains
separate. [Earlier radix-chunk trial](../2026-11-07-json-integer-source-gate/README.md).

| Complete report | Warm `RunAsync` elapsed change | Process CPU change |
| --- | ---: | ---: |
| 40-digit values | −3.39 to −1.41% | −3.53 to −1.19% |
| 4,096-digit values | −7.37 to +3.66% | −6.79 to +5.20% |
| Ordinary JSON control | −1.03 to +4.74% | −2.75 to +5.95% |

The November 26, 2026 UTC quiet block uses the Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Two workers per source warm for at least seven seconds
and supply seven rotated batches. Ranges cover four pairings of process
medians. The warm boundary includes RAM-backed mediated I/O, verified output
and files, and result disposal. Process CPU includes framing and fixture
reset. All 84 batches, both quiet checks, joins and archive verification pass.

The desktop observation of 6.49–9.75% higher long-report async elapsed remains
in the evidence. The quiet result spans zero with 11.07% elapsed spread between
candidate workers. The ordinary JSON control also moves. These observations
support a CPU improvement on the 40-digit report and leave timing for the
4,096-digit report uncertain. Retain direct UTF-8 for longer .NET 11 inputs
and the stack bridge for shorter wide values, with the original admission
contract.

Windows .NET 11 and Ubuntu .NET 10 each passed 920 public and 223 white-box
focused runs. A final focused replay adds signed token widths around the stack
boundary and verifies exact prefix offsets with a multibyte preceding character.

[Allocation](allocation-summary.json) · [Quiet timing](summary.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Raw diagnostics and procedures](procedures.tar.xz).
