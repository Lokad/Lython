# B1984 Locate the remaining log-search work

Two warmed .NET 11 RC3 EventPipe captures use Lython `f231ce76a` with
the signed optional PythonRe 0.5.0 adapter on one logical CPU in
Ubuntu WSL. Each script scans the same 3,072-line, 86,954-byte log.
One calls a compiled pattern and the other calls the module entry.
Both use `Run` and match their independent CPython 3.13.16 output and
file goldens. All 62 sampled batches verify and the workers and tracers
join.

| Region | Compiled search | Module search |
| --- | ---: | ---: |
| Optional adapter, inclusive | 38.4% | 34.0% |
| Producer frames, inclusive | 35.2% | 32.0% |
| Subject window, inclusive | 3.3% | 1.9% |
| Match validation, inclusive | 0.5% | 0.2% |
| Pattern resolution and cache, inclusive | 0.1% | 0.9% |
| Instruction dispatcher, nearest Lython method | 13.9% | 16.2% |

Inclusive regions overlap and each script has a separate resolved
sample denominator. The producer uses its funded scalar path. The
module route also places 6.4% of samples nearest the general target
dispatcher. That route already borrows executable arguments through
the fixed binder. Review the complete call path before attributing
these samples to a removable copy or additional binding stage.

The profile supports keeping the existing cache and validation paths.
It identifies the producer and the general executable dispatch as
larger regions for investigation. The emitted calls already use
positional operands, with one argument for the compiled search and
two for the module search. Both modules run the executable dispatcher
through protected file scopes. Measure a proposed dispatch change with
matched images and the original scripts.

[Attribution](attribution.json) · [Worker identity](identity.json) ·
[Run summary](summary.json) · [Executable layouts](layout.json) ·
[Captures and procedures](profiles.tar.xz).
