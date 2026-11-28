# B1964 Remove eager execution builtin bindings

The frontend and runtime share a builtin name index. The runtime keeps
immutable definitions separately and creates run-local constructor and
exception values on first lookup. `IOError`, `EnvironmentError` and
`OSError` resolve to the same run-local object. Importing `builtins`
uses shared name snapshots with the current execution's bindings.

Compared with `0b50e6fda`, each first-line search or configuration edit
allocates 7,656 fewer CLR bytes in both APIs. This is 19.2–19.3% for
the search and 22.6–22.8% for the configuration edit. The 2,048-row
shipment allocates 6,608 fewer bytes in both APIs. Compiled and peak
execution admission match across all six observations. Cleanup returns
the allowance to baseline.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 343 public and
60 white-box focused Release checks, including concurrent isolation,
shadowing, exception identity and terminal admission refusal. Both
complete worker images pass three CPython 3.13.16 output and file goldens
in each collection. Only the core DLL differs.

## Local matched comparison

November 28, 2026 on Ubuntu WSL and .NET 11 RC3. Each collection uses
two workers per image, at least five seconds of warming per worker and
seven rotated batches. Ranges span pairings of worker medians. The
repeat reverses worker and lane order and uses logical CPU 25 in place
of 27. Warm elapsed includes applicable RAM-backed I/O, verification
and previous-result disposal. Process CPU also includes fixture reset
and request framing. Compile to result includes compilation and
compiled-owner cleanup. The measurements use prewarmed processes.

| Workload | Boundary | Initial elapsed | Initial CPU | Repeat elapsed | Repeat CPU |
| --- | --- | ---: | ---: | ---: | ---: |
| File search, match at line 1 | Warm sync | −1.72% to +3.43% | −2.11% to −0.54% | −5.87% to −3.67% | −3.05% to −2.20% |
| Configuration edit | Sync compile to result | −7.97% to −6.56% | −8.19% to −2.89% | −2.74% to −0.81% | −5.80% to +1.33% |
| Ordinary tuple shipment control | Warm async | +1.04% to +4.08% | −0.28% to +4.42% | −0.48% to +1.10% | −2.19% to +1.61% |

Both runs show lower file-search CPU and lower configuration elapsed.
Configuration CPU spans zero on repeat at 4.72% same-image candidate
spread. The initial shipment elapsed increase spans zero on repeat.
Keep the removed per-run storage and all 168 observations. Both runs
joined all workers, removed their RAM-backed fixtures and verified the
persistent observations archives.

[Initial timings](initial-summary.json) · [Repeat timings](repeat-summary.json) ·
[Allocation and admission](allocation-summary.json) ·
[Initial identity](initial-identity.json) · [Repeat identity](repeat-identity.json) ·
[Initial observations](initial-observations.tar.xz) ·
[Repeat observations](repeat-observations.tar.xz) · [Procedures](procedures.tar.xz).
