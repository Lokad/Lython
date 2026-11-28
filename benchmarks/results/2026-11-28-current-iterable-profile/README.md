# B1979 Locate iterable CSV reduction costs

Two warmed .NET 11 RC3 EventPipe captures use the exact B1978 Lython
`e78a95b1b` image on Ubuntu WSL, pinned to one logical CPU. The scripts
run asynchronously over the same 2,048 CSV rows. The ordinary form
uses `map` with a helper and a tuple generator. The iterable form
uses `starmap` and a generator function that yields four arguments.
Both match their independent CPython 3.13.16 output and file goldens.
All 98 sampled batches verify and both workers and tracers join.

| Managed region | Share of resolved samples |
| --- | ---: |
| Ordinary tuple private reduction, inclusive | 28.6% |
| Iterable generator advancement, inclusive | 72.6% |
| Iterable staged generator dispatch after source pull, inclusive | 50.1% |
| Iterable generator construction, inclusive | 8.8% |
| Iterable `CreateBoundGenerator`, nearest Lython frame | 6.5% |

The two scripts have separate denominators. Inclusive regions overlap.
The staged dispatch region includes draining the argument generator
and starting the destination callback. Its nearest Lython frame
accounts for 3.5% of the iterable samples. These are managed sample
shares. The elapsed and process CPU ratios come from the separate
[quiet whole-script comparison](../2026-11-28-current-mixed-cpython/README.md).

The existing private handoff already avoids publishing a generator
object. `CreateBoundGenerator` still admits and creates an execution
context for every row's argument generator. Inspect whether eligible
rows can complete in the existing managed operand storage and create
that frame only at the first operation that requires it. Preserve the
call's admission point, Python arity and error order, and the original
live state through a suspension or an excess yield.

[Attribution](attribution.json) · [Worker identity](identity.json) ·
[Run summary](summary.json) · [Captures and procedures](profiles.tar.xz).
