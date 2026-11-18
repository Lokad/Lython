# B994 fixed unpacking from exact lists

Fixed executable unpacking of an exact list reads into already-admitted
operand slots. It checks arity and captures every value before any assignment
target can run. This removes the separate scratch list and its copy.
Starred unpacking and user iteration keep the snapshot path where work can
reenter. A sync and async regression checks identity and arity across a
setter that reads from the host and then changes the source list.

B994 compares retained `d63004a6f` (`dd0dc932`) with the archived patch
(`b62bfd52`) on 2026-11-18. It used .NET 11.0.0-rc.3.26522.148 on the
quiet Azure F4as_v7 VM with Ubuntu 24.04.4. Both images passed all file and
stdout goldens. The 96-second run passed both quiet endpoints. Workers
joined before RAM-fixture cleanup and archive verification.

| Warm workload | Reference elapsed | Candidate elapsed | Elapsed change | Process CPU change |
| --- | ---: | ---: | ---: | ---: |
| Expanded forwarding report | 3.272–3.292 ms | 3.165–3.179 ms | −3.85% to −2.85% | −5.73% to −1.27% |
| Positional call control | 2.083–2.152 ms | 2.086–2.118 ms | −3.07% to +1.67% | −2.92% to +2.54% |

Ranges use two independent workers per image. Report reference spread was
0.61%. Warm includes mediated RAM-backed I/O, golden checks and prior-result
cleanup. Setup, process startup and final teardown are outside elapsed.
Process CPU covers wider worker activity. Windows diagnostic allocation falls
from 5,678,654 to 5,391,926 B per report (−5.05%). Its accounted peak stays
5,896,862 B. On Windows, the report elapsed medians moved −6.57% and −0.72%
with a broad positional control. The local observations are archived
separately from the quiet pair.

Focused Release passed 161 public cases per target on Windows and WSL
(.NET 10 and 11). The quiet result supports the runtime change. CPython is
the output oracle for this source pair, with no elapsed measurement in B994.
The prior [B993 external checkpoint](../2026-11-18-retained-cpython-checkpoint/README.md)
reports its own measured revision.

[Summary](summary.json) · [Local screen](windows-screen.json) ·
[Observations](observations.tgz) · [Source patch and focused checks](checks-and-procedures.tar.xz)
