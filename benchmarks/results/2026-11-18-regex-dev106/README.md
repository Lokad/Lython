# Development verification of Utf8Regex 0.5.0-dev.106

R543 used the immutable Core and PythonRe packages produced from clean
fdbb1a2bb2dd0144ad9d4fd66fb8241f8b42abc8 source. Both package hashes and all
four target assembly hashes match the producer receipt. The private optional
adapter reuses the R528 native capture-name and cursor bridges with the retained
Lython core at bdab88c54. Neither the regular adapter pin nor the core dependency
graph changed.

The exact copied parent and subprocess images passed 363 regex tests each on
.NET 11 and .NET 10. Four independent sync and async probes matched CPython
3.13.2 for text capture names, retained aliases, delimiter-heavy and decimal
patterns, and original-subject windows with Unicode character positions. An
initial probe also supplied a bytes pattern. Lython's existing text-only
compiler rejected it with TypeError. The original response is kept alongside
the successful text probes.

The producer's changes update matching paths, leaving the cursor ownership
boundary in place. Creation cancellation is still authoritative during later
advances. Retained ranges extend the existing working allowance. The producer
still uses Unsafe.SizeOf. These checks qualify development semantics for an
optional test image. Release package qualification, dependency-policy
compliance and backend performance remain open.

[Inputs](inputs.json) · [Tests](checks.json) · [Probes and qualification](qualification-and-probes.tar.xz) ·
[Verified binaries](qualified-libraries.tar.xz)
