# B1012 direct operands for a trailing keyword mapping

Measured the `2215ec040` runtime (`d1b9f71b`) against prototype
`9c60eed7f` (`df78258f`) on the Ubuntu Azure F4as_v7 with .NET 11 RC3
on 2026-11-18 UTC. Both complete 32-file images and the B1005 workload
catalog verified. Five jobs took 231.80 seconds. Each used two processes
per source, eight seconds of warmup and five rotated rounds. Both quiet
gates and all 100 measured golden responses passed. All workers joined
before verified archive and fixture cleanup.

| Workload and phase | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Expanded file forwarding, warm | −6.27% to −5.61% | −6.61% to −4.93% |
| Expanded file forwarding, compile-to-result | −5.64% to −2.80% | −7.41% to −3.09% |
| Expanded file forwarding, compilation only | −1.72% to −0.50% | −3.05% to −0.01% |
| Mutable bound report, warm | −6.87% to −3.22% | −5.79% to −5.29% |
| Positional calls, warm | −2.52% to +0.22% | −0.88% to +0.68% |

A sole trailing mapping and the preceding positional operands stay in
existing executable slots. Eligible guest functions receive direct
slot copies and the existing admitted kwargs dictionary. That removes
the general accumulator, positional argument wrappers and their array
from this route. Other targets and mappings build the existing packet
after evaluating the operands. A caller-owned operand window survives
suspension and admitted callee storage owns values through completion.
The exact mapping route keeps the source version check after admission,
including reentry before the copy.

The Windows allocation diagnostic shows 3,507,424 to 3,269,856 bytes
(−6.77%) for forwarding and 1,695,768 to 1,458,200 bytes (−14.01%) for
the mutable report. Accounted peaks are equal and the allowance returns
to baseline. All checks support retention. The mutable warm elapsed IQR
reaches 7.83%, although process CPU agrees in direction. Compilation-only
movement is small and positional measurement crosses zero. The positional
result has a different reference from B1009.

Windows .NET 11 and WSL .NET 10 each passed 429 public and 65
white-box Release cases. Both execution APIs matched CPython for
callback order, aliasing and error categories. One preceding diagnostic
uses Lython's shorter bound-function label and punctuation. The probe
archive includes the unchanged wording difference. Coverage includes
native admission reentry, refusal, suspended custom mapping callbacks,
tagged positional alias identity and cancellation while kwargs are live.

The measured images precede the independent module annotation mirror
correction `6a82ab810`. Combining it with the measured change passes
all 4,897 Windows .NET 11 white-box cases and 151 focused public tests.
[Full platform, stress and package qualification](delivery/README.md)
passed for that combination.

Warm includes mediated file I/O on forwarding, verification and
prior-result cleanup. Compile-to-result adds compilation and disposal of
its owner. Startup, fixture reset, transport and final teardown are
outside guest elapsed. Process CPU covers the wider request.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
