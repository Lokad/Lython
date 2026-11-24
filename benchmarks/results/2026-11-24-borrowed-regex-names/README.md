# Named replacements without temporary UTF-8 strings

On November 24, 2026 UTC, the Release build at `ccf11aac8` resolved names
in the original UTF-8 template. It reuses the Python identifier check and
the native probe over immutable named-group metadata. A valid reference
requires no temporary Python string or name-scratch reservation. Invalid
and unknown names still get their Python diagnostics from admitted storage.

Both frozen images use the same optional PythonRe 0.4.0 adapter and the
same .NET 11 RC3 tooling. The runtime reference is `3e5826f7d`, also the
runtime source in the qualification parent `4af3c6e6e`. The four scripts
each expand one match 1,024 times with identical output verified against
stock CPython 3.13.16. The Windows allocation diagnostic reports medians of
six complete warm invocations. Each invocation restored its shared-budget
baseline after disposal.

| Replacement | CLR bytes before | CLR bytes after | Allocation reduction | Admitted peak before → after |
| --- | ---: | ---: | ---: | ---: |
| Short Unicode name | 401,632 | 344,288 | 14.28% | 143,629 → 143,545 B |
| Long Unicode name | 967,432 | 344,840 | 64.36% | 146,209 → 144,061 B |
| Numeric reference | 344,288 | 344,288 | 0.00% | 143,545 → 143,545 B |
| Literal replacement | 106,720 | 106,720 | 0.00% | 139,253 → 139,253 B |

These are an allocation comparison and a semantic check. CPU, elapsed and
performance of a newer adapter remain unmeasured. Windows and Ubuntu each
passed 708 public and 73 white-box focused Release checks, including
adapter semantics, provider cancellation, cache and ownership coverage.
The new check verifies the exact unknown Unicode name alongside malformed
and escaped references across chunk boundaries.

[Raw observations, scripts and patch](observations.tar.xz) ·
[Allocation summary](allocation-summary.json) · [Image hashes](identity.json).
