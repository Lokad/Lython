# Native regex cache lookup allocation diagnostic

Measured 2026-10-30 on Windows x64, Intel Core i7-14700KF, .NET 11 RC3.
[Raw observations and source/assembly identities](measurements.json) distinguish
revision 31772bdd from the native-cache candidate identified by production hashes.

| Pattern (4,096 UTF-8 bytes) | 1,000 warmed hits before | After |
| --- | ---: | ---: |
| ASCII | 8,264,000 managed bytes | 48,000 managed bytes |
| Supplementary scalars | 4,168,000 managed bytes | 48,000 managed bytes |

The existing 48-byte per-call overhead remains. The change removes the decoded
pattern copy; this is not a timing comparison, a CPython comparison or a statement
about total script allocations. The main performance table remains unchanged.

The frozen [driver](CacheLookup.cs) calls the actual internal CreatePattern in a
single warm context, with a reused argument array and native string. It performs
one compile through a metadata-only cooperating provider, warms 200 hits, then
counts allocations during 1,000 hits. It checks every returned pattern's identity,
exactly one provider compilation, and complete scope cleanup. It does not invoke
an actual backend, matching or public projection.

To reproduce, compile the driver as a Release net11.0 console with assembly name
Lokad.Lython.WhiteBox.Tests, referencing the chosen built Lokad.Lython.dll and its
Lokad.Parsing dependency. Run each assembly in a separate process. Rebuild against
each exact DLL, verify its emitted SHA-256, and exclude other candidate core DLLs
from project item globs: identical assembly versions alone do not identify the
measured binary. No runtime/provider code is substituted by the driver.
