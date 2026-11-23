# B1445 Lend evaluated module operands to native factory calls

Private `b410301eb` reserves a small tail with the module's existing
value storage. A compatible positional call runs the same native body
using its evaluated operands as parameters. The loan ends before
the caller resumes. If the body needs to transfer, the existing
continuation takes independently funded live values before the loan ends.

Compared with `f892efa70` on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3 on November 23, 2026 UTC. The reference core
matches retained `30d956735`. Only the core DLL differs across the
verified 32-file images.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −1.55 to +3.31% | −1.45 to +4.32% | 4.60 / 0.31% |
| Fully supplied helper | Warm | +1.04 to +2.86% | +0.51 to +3.36% | 0.10 / 1.70% |
| Eager nested factories | Warm | −1.28 to +3.71% | −2.02 to +1.87% | 1.73 / 3.28% |
| Factories observing defaults | Warm | −4.39 to +1.69% | −3.57 to +3.85% | 2.82 / 3.44% |
| Quote-heavy invoice control | Warm | −0.67 to +0.39% | −1.49 to +1.52% | 0.38 / 0.69% |
| Postponed nested factories | Compile to result | −7.65 to −0.49% | −6.88 to −0.02% | 0.68 / 7.03% |
| Large positional control | Warm | +0.42 to +2.34% | +0.27 to +2.54% | 0.23 / 1.69% |
| Ordinary split control | Warm | −0.06 to +2.52% | −0.19 to +2.79% | 0.41 / 2.16% |

Two independent workers per source warm for eight seconds each and
supply seven rotated batches. Ranges span the four pairings of process
medians. Warm elapsed includes applicable mediated RAM-backed I/O,
output verification and prior-result disposal. Compile to result adds
compilation and code-owner disposal. Wider process CPU includes resets
and protocol. Startup and final teardown are separate.

The original module callers have two slots from the evaluated argument
position and both factory bodies need six in the existing layout. The
instrumented candidate selects the loan on all 4,096 factory activations
in each host mode and calls the admitted native body without copying the
positional argument. The ordinary split and quoted invoice keep their
calls. Module storage adds 240 allocated bytes and 360 admitted bytes
per invocation on the factory and fully supplied scripts. All allowances
return to baseline.

Keep the candidate private. Factory warm results span zero. The
postponed first-result range favors the candidate, with 7.03%
candidate-worker spread. Both supplied controls slow on elapsed and
CPU even though they have no borrowed calls. The pair does not
isolate the cost among added storage, branch layout and other work.

Each platform passes 627 focused white-box checks, 200 public checks
and 293 native-transfer and cancellation checks. Both images match
seven CPython goldens. The procedures keep the initial sparse-checkout
build failure and the four Windows failures that used the wrong
probe SDK, followed by passing runs with the matching probe.
All 224 quiet batches and both machine gates verify. Workers join,
guest fixtures are removed and the persistent archive verifies.

[Source and image hashes](source.json) · [Timing](summary.json) ·
[Route counts](route-shapes.json) · [Allocation](local-allocation.json) ·
[Observations](observations.tgz) · [Tests and procedures](procedures.tar.xz).
