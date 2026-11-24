# Static builtin resolution and singleton unpacking (B1615)

The reference core is `0bf516c79`, unchanged in the documentation
parent `b65d6bd96`. The [frozen patch and image identities](identity.json)
identify the candidate. It keeps an unknown binding for an actual builtin
name when a write, branch or invalidation removes the value's type.
Call diagnostics, return inference, mutation analysis and length
refinement use that binding information. Diagnostics also reuse the
call's resolved target. Singleton unpacking uses the usual element
binding. This removes the shortcut that assigned the iterable's type
to its target.

The separate Windows probe compares 22 scripts with isolated stock
CPython 3.13.2. The candidate matches all 22 in both execution modes.
The reference rejects 21, including function and import rebinding,
unknown callables, either branch of a join, parameters, deferred
module writes, closure locals, unpacking, conditional mutation,
context targets, match captures, assignment expressions and a
temporary exception binding. Normal module deletion still restores
the builtin diagnostic. Reading a deleted lexical local still reports
the unbound-local diagnostic.

Windows and Ubuntu each pass 265 focused public and 32 white-box .NET 11
Release checks. These include compile cancellation, terminal admission
failure, binding and diagnostic behavior, and allowance refunds.
Both exact 32-file worker images pass all four complete CPython goldens.

## Short developer timing comparison

The existing comparison worker runs on local Ubuntu under WSL.
Five rotated batches from each of two workers per source give 120
timed responses across six lanes. The windows reflect the four pairings
of per-worker medians. Negative values mean less time for the candidate.
Warmup is two seconds per worker and timed requests target 180 ms.
All workers join and RAM-backed guest fixtures are removed.

| Workload | Phase | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Configuration manifest | Compile | +1.78 to +5.47% | +1.75 to +6.03% |
| Configuration manifest | Compile-to-result | −0.37 to +9.17% | −4.32 to +14.22% |
| Sparse validator | Compile | −6.63 to −4.11% | −8.17 to −1.28% |
| Sparse validator | Compile-to-result | −9.67 to +1.66% | −11.43 to +0.84% |
| CSV summary helpers | Compile | −6.24 to +2.20% | −10.30 to +7.99% |
| JSON summary helpers | Compile | −0.14 to +1.54% | +3.95 to +4.56% |

Compilation includes disposal. Time to result also includes execution,
applicable RAM-backed I/O and checks. Wider CPU also covers fixture
reset and request framing. This block had no quiet-machine gate.
The manifest's reference time-to-result worker spread reaches 8.75%.
Keep the correctness changes, retain the adverse compile observations,
and use the quiet confirmation to interpret them. B1614 remains
the source-identified external comparison.

[Summary](summary.json) · [Images](identity.json) ·
[Raw responses, patch, goldens and test logs](observations.tar.xz) ·
[Quiet confirmation](quiet-confirmation/README.md) ·
[B1614](../2026-11-24-abstract-compiler-cpython/README.md).
