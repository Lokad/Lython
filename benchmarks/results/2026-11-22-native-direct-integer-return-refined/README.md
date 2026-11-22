# B1372 Keep direct arithmetic returns in the existing call entry

Compared the frozen `7a8df051a` core from delivery
`880c6d958` with `7d323d40f` on
November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3. The rest of the 32-file image is identical, including
the benchmark worker and optional adapter.

A closed arithmetic body can read the caller's supplied positional span.
The compiler uses the existing integer continuations and verifies that
the body returns the chain's result. The runtime still enters through
both managed checks and leaves directly with the existing slot result.
The ordinary workspace remains in the same entry method. Eligible
calls skip its explicit buffer initialization, argument copy and native
activation dispatch. Unsupported types and signed overflow return to
the ordinary compiled body before any guest effect.

In the local tier-disabled Windows x64 disassembly, the initial
selector had no standalone listing. Its workspace routine retained the
original 549 bytes and 440-byte reservation. The refined entry emits
737 bytes and reserves 456 bytes before selection. It skips explicit
workspace initialization and copying on a direct return. These code
generation observations do not attribute the normal-tier quiet timings.
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
| Function-scoped helper | warm | -21.21 to -18.84% | -20.71 to -19.66% | 1.78 / 1.21% |
| Mixed factory | compile to result | -0.22 to +6.41% | +1.06 to +4.66% | 1.67 / 4.89% |
| Module-scoped helper | warm | -12.16 to -10.24% | -11.42 to -10.47% | 0.79 / 1.39% |
| Nested-helper CSV invoice | warm | -3.10 to -2.06% | -3.64 to -2.19% | 0.19 / 0.89% |
| Quoted policy CSV invoice | warm | -1.49 to +1.65% | -2.22 to +1.54% | 1.73 / 1.44% |

Negative changes favor the candidate. Ranges span all pairings of two
process medians per image. The sample has seven rotating batches per
worker after at least eight seconds of warmup. Warm clocks include the
run, applicable mediated RAM-backed I/O, output verification and
prior-result cleanup. Compile to result adds compilation and disposal
of the compiled owner. Wider CPU also includes the worker's reset and
request handling. Startup remains separate.

All 140 timed batches and both quiet gates passed in
275 seconds. Workers joined and the raw
archive was checked under the lease before cleanup. The exact source
pair passes 1,129 focused Release checks per platform. The checks
cover genuine async operation transfer, in-flight cancellation and
funded fallback, along with integer promotion and call semantics.
The staged catalog was already verified byte-for-byte in both images.
The quiet lease retires only the staging copy of the first candidate, with
the exact binary and all records preserved in their pushed archives.

## Decision

Retain the refined source with explicit mixed first-use debt. The
function-scoped helper uses 18.84–21.21% less warm elapsed time and
19.66–20.71% less wider CPU. The module helper uses 10.24–12.16%
less elapsed time. The nested invoice uses 2.06–3.10% less elapsed
time and 2.19–3.64% less wider CPU. The quoted invoice spans zero.

Mixed time to result ranges from −0.22 to +6.41% with 4.89%
spread between candidate workers. Wider CPU is consistently
1.06–4.66% higher. The first source also shows a mixed slowdown.
Preserve both records and investigate the cost before release.
The verified helper and invoice gains justify retention with that debt.
The JSON control in the first comparison spans zero.

[Summary](summary.json) · [Local allocation](allocation-summary.json) ·
[Executable shape](shape-summary.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[First candidate](../2026-11-22-native-direct-integer-return/README.md).
