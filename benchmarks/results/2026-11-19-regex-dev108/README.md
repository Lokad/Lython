# Development validation with Utf8Regex 0.5.0-dev.108

The optional development adapter passed nine pattern and subject cases
against CPython 3.13 using Lython `0d16f8307`. Each case passed on Windows
.NET 10 and .NET 11 through both execution APIs. Cases cover original
subject windows, captured lookbehind, adjacent empty and nonempty results,
repeated multibyte captures, split, callback replacement and matches used
after cache purge. Each run uses a borrowed 4 MiB allowance.

The Core and PythonRe package bytes and target assemblies match the
supplier's immutable Release receipt at `e0097a3fa`. The complete probe
images were checked before and after use. The supplier's separate
development adapter run passed 405 Lython cases per target, including
in-flight cancellation during compilation, matching and cursor advance.
Its TRX and package digests were verified. The probe and supplier checks
cover distinct integration boundaries.

This development package still uses `Unsafe.SizeOf` in Core construction
and matching code. Its cursor accepts per-pull tokens and can fund retained
ranges, but creation disallows a starting byte beyond the end byte.
Python's reversed anchored window remains an adapter capability gap.
The released adapter pin remains [PythonRe 0.4.0](https://www.nuget.org/packages/Lokad.Utf8Regex.PythonRe).
Final installed-provider qualification and backend performance measurements
remain open.

[Inputs and checks](inputs-and-checks.json) ·
[Exact cases, test records and receipts](checks-and-procedures.tar.xz).
