# B1902 Share assignment targets throughout the frontend

Ordinary and unpacking assignments share the same name, attribute,
subscript, slice and group records. The parser no longer constructs
temporary display items for bare comma targets. Binding diagnostics
visit assignment targets directly, removing the conversion to loop
targets. Both execution paths use the same lowering switch and keep the
existing unpacking layout. The starred marker adds a field to the shared
target. Function-free generated configuration allocates about 2 KiB more
per compile, an observed tradeoff.

The reference is `a16b4c188`, with production source identical to
`9d048800a`. Both frozen images have 32 verified files and differ only
in the core DLL. The nested record transform includes a starred inner
unpack, indexed stores, a chained assignment and a context-manager
target. Its output matches CPython 3.13.2 in both Windows hosting modes.
Both images also verify all six catalog goldens against CPython 3.13.16.

The compiler witness matches 19 emitted tapes, 143 admission levels,
22 ordered diagnostic specimens and cancellation cleanup. The diagnostic
cases cover duplicate starred items, indexed reads before and after
sibling bindings, a starred group and a late scope directive. Windows
.NET 11 Release passes 1,474 public and 468 white-box focused tests,
including genuine asynchronous target operations. Windows .NET 10
passes 249 public and 104 white-box focused tests. Ubuntu WSL .NET 11
passes the same 249 public tests.

| Workload | Windows compilation allocation change | Peak admitted |
| --- | ---: | ---: |
| Nested assignment record transform | −2,800 B | 107,967 B |
| Ordinary definitions | −608 B | 889,975 B |
| Escaping definitions | −600 B | 973,783 B |
| Guarded report | −2,296 B | 2,410,080 B |
| Function-free configuration | +2,056 B | 1,461,312 B |
| Nested JSON document | −40 B | 69,840 B |

Peak admission is identical between both images. Allocation values are
medians of 17 warmed compilations on the same Windows .NET 11 SDK.

## Timing on November 27, 2026 UTC

Ubuntu WSL on CPU 27 ran .NET 11 RC3. Two workers per image ran seven
rotated batches each after at least five seconds of warmup. The ranges
compare every pairing of worker medians. Compile elapsed includes
previous-code disposal. Warm and compile-to-result elapsed also include
the script, applicable host I/O and result cleanup. Process CPU includes
fixture reset and framing. Startup is outside both clocks.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested assignment record transform | Compile | −1.19% to +0.14% | −3.58% to +1.99% |
| Ordinary definitions | Compile | −3.05% to +3.19% | −3.65% to +2.89% |
| Nested assignment record transform | Compile to result | −2.94% to +0.84% | −3.12% to +0.62% |
| Nested JSON control | Warm | −1.52% to +0.11% | −2.97% to +0.12% |

All 112 timed batches verify. Worker joins, removal of guest fixtures and
archive verification complete. Retain the simpler frontend and the
measured allocation changes. The elapsed and CPU ranges leave the
whole-script timing effect unresolved.

[Compiler parity](compiler-parity.json) · [Timing summary](summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch, checks and procedures](procedures.tar.xz).
