# B1265 compiler instruction scratch

Reference `b190476cd` and detached prototype `18ac5adee`.
The prototype replaces each provisional instruction list with a
range in private compiler arrays. An active block can extend at the
current array's end. Existing compaction and layout operate on those
ranges, then copy the final tape into independently owned storage.
The public API, interpreter instruction layout and coarse compiler
admission estimate are the same for both sources.

## Source attribution

The local Windows Release phase probe uses .NET 11 RC3 and an Intel
Core i7-14700KF. On the generated validator, executable construction
accounts for 32% of measured compilation allocation, parsing 28%
and abstract interpretation 14%. The instrumented pass attributes
338,336 bytes to provisional instruction backing growth across 485
blocks and 3,889 emitted instructions. The instrumented image measures
allocations only. The phase decomposition invokes internal stages
directly and excludes entry normalization, wrapper construction and
owner setup or disposal. Its local timings are diagnostic.

## Allocation and behavior

Windows Release public compilation with a borrowed allowance, including
compiled-owner disposal. Each invocation returns its allowance to
baseline. Positive changes indicate more CLR allocation.

| Complete script | Reference allocation | Prototype allocation | Change |
| --- | ---: | ---: | ---: |
| Postponed nested factories | 67,008 B | 67,496 B | +0.73% |
| Inherited dataclass configuration | 598,984 B | 598,416 B | -0.09% |
| Generated 96-field validator | 2,275,353 B | 2,159,600 B | -5.09% |

The local warm allocation and accounted execution peaks match on
all three scripts. Complete stdout and output-file goldens pass with
both 32-file images and isolated local CPython. All 57 inspected
code objects agree on instruction fields, source spans, operand
positions and exception regions. These comparisons cover the recorded
scripts. Windows .NET 11 and Ubuntu .NET 10 each pass 390 white-box
and 1,010 public Release checks, including compiler cancellation,
exception routing, stack admission and ownership. The first Windows
public run had eight probe setup failures. A probe built from the
candidate with the pinned runtime on PATH passes every check.


An additional CPython comparison exercises 600 consecutive assignments,
24 formatted expressions that continue through different branch blocks,
and tuple folding under a protected call. Both Lython run modes match.

## Quiet VM

Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Two separately warmed workers per source, with normal tiering and GC.
Each B1265 lane uses seven rotated batches after at least 16 seconds
of full requests per worker. Compilation includes the compiled-owner
disposal. Time to result adds one execution with mediated RAM-backed
I/O, golden observation and result cleanup. Startup and final teardown
sit outside the guest clocks. Process CPU also covers the enclosing
request and fixture work.

| Lane | B1265 elapsed change | B1265 process CPU change |
| --- | ---: | ---: |
| Validator compilation | +1.47 to +3.36% | +1.95 to +4.53% |
| Validator compile-to-result | −5.20 to −2.46% | −4.14 to −2.48% |
| Dataclass compilation | −2.10 to +1.67% | −2.81 to +1.30% |
| Postponed factory compilation | −2.07 to +1.61% | −2.74 to +1.69% |

Ranges cover all four pairings of the two process medians per source.
Positive percentages indicate slower execution. B1265 used 112 timed
batches in four lanes over 367 seconds. Both quiet gates passed.
Workers joined, guest fixtures were removed, and the persistent
observations were verified under the VM lease. Two staging attempts
were refused before any image retirement. The successful attempt
proved all paths, hashes and aliases for three reconstructible
historical core replicas before retiring them.


B1266 repeats both validator lanes with the exact same images, in
reverse lane and worker order and with 22 seconds of warmup per
worker. All 56 batches and the cleanup gates pass in 237 seconds.
Compilation is 2.56–4.76% slower with 2.43–5.16% more process CPU.
Time to result is 1.36–4.84% faster with 2.18–4.68% less process CPU.
The two boundaries reproduce different outcomes. Their measurements
do not isolate the cause of that difference.

Keep `18ac5adee` private. The reduction in managed allocation and
time to result makes a source refinement worthwhile. Emission currently
materializes a block span before checking the opcode. Only the comparison
jump fold reads that span. Move the lookup into that branch and compare
the two validator boundaries and an annotated control. Keep the current
production storage through this experiment.

[Phase samples](phases.json) · [Construction allocations](construction-phases.json)
· [Whole-script allocation](allocation-summary.json) · [Instruction inspection](inspection-summary.json)
· [B1265 timing](summary.json) · [B1266 timing](confirmation/summary.json)
· [Source and checks](checks-and-procedures.tar.xz) · [Additional checks](additional-checks.tar.xz).
