# B1371 Evaluate complete integer returns from supplied operands

Compared the frozen `7a8df051a` core from delivery
`880c6d958` with `b3909536d` on
November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3. The rest of the 32-file image is identical, including
the benchmark worker and optional adapter.

A closed arithmetic body can read the caller's supplied positional span.
The compiler uses the existing integer continuations and verifies that
the body returns the chain's result. The runtime still enters through
both managed checks and leaves directly with the existing slot result.
This eliminates the inline frame buffer, argument copy and native
activation dispatch on that edge. Unsupported types and signed
overflow return to the ordinary compiled body before any guest effect.
The base code and both paired images emit the same 23 inspected tapes.
The candidate identifies five eligible bodies without changing the tape.

Warm allocations are flat locally, and all eight admitted peaks
match exactly. Every borrowed allowance returns to baseline. Both
actual images match local CPython 3.13.2 on all eight workloads and
six additional checks covering promotion, signed division, exception
context, reflected protocols, mutable globals and nested calls.

## Quiet comparison

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread old/new |
| --- | --- | ---: | ---: | ---: |
| Function-scoped helper | warm | -20.84 to -18.42% | -21.47 to -19.23% | 0.96 / 2.06% |
| Module-scoped helper | warm | -12.31 to -10.65% | -12.09 to -10.69% | 1.13 / 0.75% |
| Nested-helper CSV invoice | warm | -3.78 to +0.63% | -3.60 to +0.00% | 1.04 / 3.51% |
| Quoted policy CSV invoice | warm | -3.73 to +2.71% | -3.08 to +3.21% | 3.49 / 3.10% |
| JSON Lines shipment report | warm | -4.00 to +1.47% | -4.91 to +2.58% | 5.30 / 0.38% |
| Mixed factory | compile to result | +3.30 to +4.58% | +2.33 to +5.66% | 0.92 / 0.31% |

Negative changes favor the candidate. Ranges span all pairings of two
process medians per image. The sample has seven rotating batches per
worker after at least eight seconds of warmup. Warm clocks include the
run, applicable mediated RAM-backed I/O, output verification and
prior-result cleanup. Compile to result adds compilation and disposal
of the compiled owner. Wider CPU also includes the worker's reset and
request handling. Startup remains separate.

All 168 timed batches and both quiet gates passed in
329 seconds. Workers joined and the raw
archive was checked under the lease before cleanup. The exact source
pair passes 1,129 focused Release checks per platform. The checks
cover genuine async operation transfer, in-flight cancellation and
funded fallback, along with integer promotion and call semantics.
The staged catalog was already verified byte-for-byte in both images.
The quiet lease retires only six aliases of an older core, with
the exact binary and all records preserved in their pushed archives.

## Decision

Both helper placements measure lower warm elapsed and CPU, without a
measured allocation change. The affected invoice elapsed spans zero
and the quoted invoice and JSON controls are inconclusive. The mixed
factory consistently takes 3.30–4.58% longer to reach the result and
uses 2.33–5.66% more wider CPU. The factory has no eligible arithmetic
return, so it takes the ordinary workspace entry through the new wrapper.
Keep this candidate private. The next source puts the workspace back
in its original entry method and preserves the direct arithmetic return.
That changes the common entry shape. The measured differences do not
isolate its cause.

[Summary](summary.json) · [Local allocation](allocation-summary.json) ·
[Executable shape](shape-summary.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Paired attribution and baseline](../2026-11-22-retained-call-cpu-profile/README.md).
