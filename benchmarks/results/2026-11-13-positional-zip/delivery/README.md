# Positional ZIP delivery

Runtime976f9439/frozen97bbeff3 with corrected overlap tests307f6f41 passes
Windows/Ubuntu actual.NET11/10 ordinary suites, original stress scales and
installed Release consumers in [43465190359](https://github.com/Lokad/Lython/actions/runs/43465190359).
All575frozen production inputs match the exact qualified commit, with explicit
checkout newline normalization. Full logs and16individual-step summaries are
cached here. CI binary byte identity is not inferred from matching sources.

The [original failure](../overlap-correction/README.md) remains visible: a stale
duplicate-header assertion disagreed with CPython's physical overlap handling.
The corrected assertion preserves append rejection and unchanged destination
bytes; no runtime change was needed. The [comparison](../comparison/README.md)
retains the small-case2.2–2.5µs elapsed regression, variable large-case timing,
original budget-guard partial collection and image-preflight failure. Full
qualification does not erase those observations or predict other workloads.

New JSON byte-input development is outside this frozen qualification.
Publication and tagging remain postponed.
