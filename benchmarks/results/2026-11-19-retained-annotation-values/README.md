# B1131 deferred annotation dictionary publication

Compared `a472eb2a4` and private `33d798fb1` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. The complete
32-file images matched four CPython workload goldens. Two workers per
source and five rotated rounds produced 80 verified timed responses.
Both quiet gates passed and workers joined before archive verification.

| Workload, warm execution | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Annotated rules, final dictionary read | −3.40% to −1.07% | −2.90% to 0.00% |
| Annotated rules, each dictionary read | +2.96% to +4.48% | +3.25% to +4.94% |
| Ordinary rules | −2.08% to +2.89% | −1.96% to +3.91% |
| Captured rules | −1.66% to +0.19% | −1.46% to +1.04% |

Ranges cover all four cross-source process-median pairings. The control
that reads every dictionary also observes a 6.13% local allocation
increase. Reading only the final dictionary allocates 6.38% less. Each
annotated workload has 131,072 extra accounted bytes at peak from funding
the private payload and future dictionary shell together. Both ordinary
rules' and captured rules' allocation and accounting are unchanged.
Ordinary candidate elapsed spread reaches 4.18%.

The prototype evaluates annotations eagerly and retains only their
funded values and shared compiled keys. A dictionary is created on first
observation by adopting those values. Read-every-time scripts pay for the
private object and the resulting dictionary. The gain when metadata goes
unread leaves a larger CPU and allocation cost when scripts inspect it.
The prototype stays private. A follow-up would have to remove that
extra retained object or establish a useful boundary that avoids it.

Windows .NET 11 and Ubuntu .NET 10 each passed 432 focused Release cases.
Coverage includes eager evaluation, decorators, aliases, deletion and
replacement, cancellation, refusal, and reading the annotation dictionary
at a fully occupied allowance. Local workloads returned allowance to
baseline. A stale local setup reference was caught before staging. The
procedures archive preserves that preflight and both source identities.

Warm includes mediated I/O, golden checks and previous-result disposal.
Process CPU uses a wider request boundary. This compares two Lython
revisions with fixed CPython goldens.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source, checks and procedures](checks-and-procedures.tar.xz).
