# B992 borrowed expanded arguments

Executable expanded calls to user functions now borrow the accumulator's
live prefix through the existing callable guards and binder. The accumulator
stays funded through invocation and cleanup. Callables on the existing
array path still receive an exact packet. Growth admits both backing arrays
before allocation, copies in cancellable chunks, then retires the overlap.
The new shared-budget regression fails on the reference in both reservation
modes because that build admitted only the capacity increase during growth.

B992 compares `045eb7f44` (`97851ccf`) with the archived source patch
(`dd0dc932`). The 1,024-row report expands seven values per helper call
from a four-slot initial accumulator. Both workers verified stdout and file
goldens against CPython before measurement. The 96-second quiet run used
Ubuntu 24.04.4 on the four-vCPU Azure F4as_v7 with .NET
11.0.0-rc.3.26522.148. Both quiet endpoints passed. Workers joined and
fixtures were removed before archive verification.

| Warm workload | Elapsed change | Process CPU change | Reference / candidate same-source elapsed spread |
| --- | ---: | ---: | ---: |
| Expanded forwarding report | −2.24% to +2.06% | −3.28% to +2.00% | 2.76% / 1.60% |
| Positional call control | −1.65% to +7.54% | −0.41% to +5.75% | 1.69% / 7.53% |

Ranges pair the two reference and two candidate worker medians. Warm elapsed
includes mediated RAM-backed I/O, goldens and prior-result cleanup. Process
CPU has the wider worker boundary. Startup and fixture resets are outside
elapsed. Diagnostic Windows allocation falls from 5,899,836 to 5,678,646 B
per report (−3.75%). The accounted peak remains 5,896,862 B. The positional
control's median allocation and accounted peak match. The timing range spans
zero and the positional control is unstable. Retention is based on correct
growth admission and removal of the exact-length handoff copy.

The local Windows elapsed screen is also mixed. One compile-to-result
attempt was interrupted by its sampling deadline. Its partial observation
and the reverse-order sample are preserved. The first staging preflight found
271,503,242 available bytes after proposed recovery against 272,720,105
required. It performed no remote mutation. The second preflight verified
additional exact archived replicas and reclaimed only the required paths.

On each of .NET 10 and .NET 11, Windows Release passed 36 white-box and
226 public cases, and WSL passed 36 white-box cases. Coverage includes
terminal refusal, alias identity, suspension, generator definitions and
keyword collision ordering.

[Quiet summary](summary.json) · [Windows screen](windows-screen.json) ·
[Raw observations](observations.tgz) · [Checks, source patch and procedure](checks-and-procedures.tar.xz)
