# Native format-field allocation diagnostic

Measured 2026-10-30 in Release .NET 11 RC3 on Windows x64 / Intel Core i7-14700KF.
Control is the exact net11 DLL from the locally qualified unpublished core package
at `d7f7a00c`; candidate is `afb701c9`. [Revisions, package/assembly identities and
raw counts](measurements.json) are frozen with the [driver](Format.cs).

| Case | Managed bytes / 100 formats before | After |
| --- | ---: | ---: |
| Plain positional field | 51,200 | 54,400 |
| Small nested spec | 92,800 | 72,000 |
| 4,096-byte nested spec | 1,732,000 | 1,302,400 |
| Unicode names/suffix and nested spec | 103,200 | 81,600 |

The plain-field increase remains visible: funding private metadata adds a small
reservation object. Nested fields remove whole-field text parsing, spec re-encoding
and an intermediate owned UTF-8 copy. Those private copies also stop accumulating
coarse batch charges: the 4 KiB case's reservation delta drops from 435,400 to
13,000 bytes per hundred calls, including the final retained result estimates.
This is accounting, not CLR live-heap measurement. All owners return to zero after
actual users end, without GC. Neither elapsed improvement nor a CPython ratio is
claimed, and the primary comparison table is unchanged.

The driver reuses one prepared template, inputs and callbacks; callback values and
complete final output are checked before/after sampling. All callbacks complete
synchronously. It warms twenty formats and samples one hundred with the current-
thread allocation counter. It exercises native parsing, metadata, nested working
storage and final output copies, excluding frontend/startup/public projection.
Both sides use the same supplied allowance for input and working storage; the
separate public admission regression verifies execution/compilation-owner isolation.

Reproduce as a Release net11.0 console named Lokad.Lython.WhiteBox.Tests, referencing
the recorded core DLL and unchanged Lokad.Parsing dependency. Exclude other core
DLLs from default project globs, rebuild and run each side separately, and verify
the emitted core hash. Define GOVERNED_FORMATTER for the candidate's explicit
working-owner signature. This conditional adapts the internal signature only;
templates, callbacks, allocation counter and sample boundary are identical.
The frozen driver targets the recorded internal APIs, not future HEAD.
