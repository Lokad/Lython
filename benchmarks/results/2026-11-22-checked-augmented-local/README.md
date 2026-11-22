# Keep private augmented operands in their local slots

Compared `eb227dcac` with `638fc1138` on November 22, 2026 UTC.
The same optional adapter and worker run against each core on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both copies of the
32-file image and their catalog verify before staging. The only binary
difference is the core library.

A same-block call checks that a private lexical local is bound at the
original read. The augmented operation consumes that same slot after the
right-hand side. Captured, mirrored and module bindings and explicit
right-hand-side writes retain their ordinary snapshots. The supplied helper
uses seven slots instead of eight. The emitted tapes of the five initial
controls match. No new memory owner or outstanding borrow is needed.

## Timing

| Collection and complete script | Boundary | Elapsed change | Wider CPU change | Worker spread old/new |
| --- | --- | ---: | ---: | ---: |
| Initial · Function-scoped helper | warm | -8.60 to -7.29% | -8.52 to -7.37% | 0.76 / 0.67% |
| Initial · Module-scoped helper | warm | -0.11 to +2.02% | +0.54 to +3.04% | 0.26 / 1.87% |
| Initial · Integer range loop | warm | -3.75 to +0.38% | -4.02 to +0.00% | 4.26 / 0.02% |
| Initial · Mixed factory | compile to result | +2.05 to +2.66% | +1.97 to +2.64% | 0.11 / 0.49% |
| Initial · Ordinary CSV invoice | warm | -2.78 to -1.01% | -3.39 to -0.94% | 1.67 / 0.15% |
| Initial · Typed ASCII CSV meter | warm | -1.53 to +4.16% | -0.90 to +4.02% | 2.00 / 3.71% |
| Initial · Function-scoped helper | compile | -0.38 to +0.52% | -1.18 to -0.01% | 0.23 / 0.67% |
| Follow-up · Mixed factory | compile to result | -17.17 to -2.72% | -17.45 to -3.19% | 14.35 / 2.70% |
| Follow-up · Helper-driven CSV invoice | warm | -4.34 to +0.61% | -4.55 to +1.15% | 4.42 / 0.72% |
| Follow-up · Mixed factory | compile | -0.79 to +1.41% | -1.76 to +1.23% | 0.65 / 1.56% |

Negative changes favor the candidate. Intervals span process-median pairs.
Each lane has two workers per image, at least eight seconds of warmup
per worker and seven rotated batches. Warm clocks cover guest work,
applicable mediated RAM-backed I/O, output verification and prior-result
disposal. Compilation includes code-owner disposal. Compile to result
adds one run and cleanup. Startup stays separate. Wider process CPU
also covers fixture reset and request handling.

## Allocation and correctness

The original function-scoped script allocates 65,504 fewer bytes per
warm execution on Windows, from 142,728 to 77,224. The 4,096-record
helper-driven CSV invoice falls from 6,229,470 to 6,098,430 bytes.
Admitted peaks match and every shared allowance returns to baseline.
Both platforms pass 666 focused Release checks and a final nine-case
rerun. Both images match all six initial script pairs, eight focused
semantic probes and the complete CSV invoice against CPython 3.13.2.
The tests cover alias identity and reflected operations, rebinding,
early unbound errors, generator send, actual file suspension,
cancellation and terminal refusal.

The Windows diagnostic with tiering disabled grows the ordinary
instruction prefix from 11,696 to 11,896 bytes and the native loop from
4,869 to 4,979 bytes. This records a shared code-generation change.
It does not attribute Linux timing differences to particular instructions.

280 timed batches verify in 551 seconds across the quiet
collections. All workers join, fixtures are removed and raw evidence is
archived and verified under the VM lease. The initial retrieval guard
rejects an early local attempt before contacting the machine.

## Decision

Retain the original-read check and direct slot consumption. The function
helper uses 7.37–8.52% less warm CPU and takes 7.29–8.60% less warm
elapsed time with stable workers. The complete helper-driven invoice
allocates 2.10% less. Its warm CPU and elapsed intervals span zero.

The initial mixed factory takes 2.05–2.66% longer from compilation to
result. The follow-up does not reproduce that direction, with 14.35%
reference worker spread. Compilation-only clocks span zero. Keep
mixed first-use timing open. The module-helper elapsed range spans
zero and its initial CPU is 0.54–3.04% higher. Reassess these controls
at the next coherent call or frame change. Keep the scope limit until
a design preserves module rebindings without adding a second store.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Executable shapes](shape-summary.json) · [Semantic coverage](semantics-summary.json) ·
[Initial observations](observations.tgz) ·
[Follow-up observations](invoice-observations.tgz) ·
[Verification and procedures](procedures.tar.xz).
