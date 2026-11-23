# Short word report source boundary check

Three source shapes use the same frozen Lython runtime at `0112138c1`,
the same 3,072-line Unicode fixture and the same expected report.
The first is the representative script. The second moves the width
update from the nonlocal helper to the caller. The third also inlines
the existing `lower().split()` expression into the caller's loop.
All three, a mixed-width histogram and the quoted invoice match
CPython stdout and file goldens.

| Source shape | Warm CLR bytes per invocation | Admitted peak |
| --- | ---: | ---: |
| Representative report | 380,488 | 735,891 |
| Width update in the caller | 380,432 | 735,795 |
| No per-line helper | 380,272 | 735,211 |

The representative helper's tape includes the nonlocal store and a
direct return. All three shapes keep the native dictionary loop. These
Windows .NET 11 RC3 diagnostics isolate no substantial CLR allocation
for the per-line helper or its nonlocal store. The variants also change
instruction placement. They establish no elapsed or CPU speedup.
Compare CPU before selecting an entry or cased-split redesign.
Every tested shared allowance returns to baseline.

[Quiet-VM CPU comparison](cpu.md) ·
[Catalog, opcode tapes, allocations and verification](diagnostics.tar.xz) ·
[Frozen image](../2026-11-23-owned-class-module-slots/identity.json).
