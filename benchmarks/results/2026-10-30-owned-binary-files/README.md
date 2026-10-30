# Owned binary-file allocation diagnostic

Measured 2026-10-30 in Release .NET 11 RC3 on Windows x64 / Intel Core i7-14700KF.
Control is the exact core DLL from the previously qualified unpublished package at
`d7f7a00c`; candidate is `acc0a8b9`. [Identities and raw counts](measurements.json)
are frozen with the [driver](Files.cs).

| Native workload | Managed bytes / 100 iterations before | After |
| --- | ---: | ---: |
| Read 64 KiB in 4 KiB guest chunks | 23,555,448 | 16,780,248 |
| Stage, publish and close 64 KiB | 21,018,368 | 14,394,576 |

The owned file fills admitted read buffers directly and borrows stable writer
storage until actual completion. It removes the old range payload and publication
copies while adding explicit handle ownership. The diagnostic checks every byte
and complete publication, and verifies the shared root returns to zero after each
iteration without GC. Host input is pre-existing host-owned storage; the write
host consumes its borrow without keeping a second file copy.

This measures synchronous native file handles, parameter binding, guest byte
storage and ownership cleanup. It includes fresh native execution contexts and
owners, warms twenty invocations and samples one hundred using the current-thread
allocation counter. It excludes compilation, instruction dispatch, public
projection, actual disk I/O and asynchronous suspension. It supplies neither an
elapsed speedup nor a CPython ratio, and does not update the primary comparison
table. Text/whole-file transport migration remains separate.

Reproduce as a Release net11.0 console named Lokad.Lython.WhiteBox.Tests, referencing
the recorded core DLL and unchanged Lokad.Parsing dependency. Disable default
project globs, rebuild each side separately and verify the emitted core hash.
Define OWNED_FILES for the candidate's host contract. Both versions use identical
guest buffers/content, native calls, sample boundaries and balance assertions;
the conditional selects the real host transport appropriate to each revision.
The frozen driver targets those recorded internal APIs, not future HEAD.
