# B1009 direct function annotation construction

Measured reference `0547e5489` (`62665014`) and prototype `6f699fcd3`
(`91b7ea2d`) on the quiet Ubuntu Azure F4as_v7 with .NET 11 RC3 on
2026-11-18 UTC. Both 32-file images and the unchanged B1007 workload
catalog passed verification. Five jobs took 229.51 seconds. Each used
two processes per source, eight seconds of warmup per process and five
rotated rounds. Both quiet gates and all 100 measured goldens passed.
All workers joined before archived evidence was verified and fixtures removed.

| Workload and boundary | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Annotated rules, warm | −31.00% to −28.32% | −31.88% to −26.44% |
| Annotated rules, compile-to-result | −31.55% to −27.52% | −32.13% to −27.32% |
| Annotated rules, compilation only | +3.68% to +4.21% | +2.81% to +4.43% |
| Unannotated rules, warm | +0.05% to +2.40% | −1.96% to +1.83% |
| Positional calls, warm | +0.40% to +1.42% | +1.23% to +2.88% |

The controlled whole-script improvement supports retaining the change.
Extra compiler work costs time even though complete one-shot execution
improves. The unannotated control has same-source elapsed differences
up to 1.84% and mixed CPU. The adverse positional control is small
and has supporting CPU. Carry both observations into the next substantive
function and call comparison. These are Lython revision comparisons.

Ordinary eager operands now use the defining executable code and share
the existing yielding-annotation route. Generic and incomplete lexical
scopes retain their annotation evaluator. Function metadata uses an
independent, pre-sized dictionary with prepared UTF-8 keys. Module and
class annotations retain custom-mapping subscript behavior. A failed
attachment refunds only unpublished dictionary capacity. The Windows
allocation screen reports 4,929,256 to 4,093,720 bytes per annotated
invocation (−16.95%) and an accounted peak of 6,388,845 to 5,295,285 bytes
(−17.12%). The controls' allocation and accounted peaks are unchanged.
The allowance returned to baseline in both images.

Focused Release checks passed 363 public and 55 white-box cases each
on Windows .NET 11 and WSL .NET 10. Both public execution APIs matched
CPython 3.13 for evaluation order, defining scopes, Unicode names, mutable
annotation dictionaries and value aliases. Checks also covered suspended
cancellation, refusal, exception unwind and budget reuse. Full qualification
belongs to the next delivery checkpoint.

Warm includes execution, output verification and prior-result cleanup.
Compile-to-result includes compilation and disposal of its owner. Startup,
fixture reset, transport and final teardown are outside guest elapsed.
Process CPU covers the wider request.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
