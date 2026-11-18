# Direct construction of Python function defaults

B1001 compared `0cf1156c8` (frozen `334f106b`) with direct default
construction (frozen `6c4cb50a`) on the quiet Azure F4as_v7 VM,
2026-11-18 UTC. The definition records the slots that have defaults.
Runtime evaluation fills the final positional tuple backing and keyword
dictionary, removing the temporary CLR name map. The lambda path uses
the same layout for already-evaluated arguments. Generated dataclass
plans keep their name-supplied entry point.

| Workload and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Rule-closure report, warm | −25.49% to −22.84% | −27.35% to −22.22% |
| Rule-closure report, compile-to-result | −22.52% to −21.47% | −22.66% to −21.38% |
| Rule-closure report, compile | −1.35% to −0.28% | −1.38% to +0.84% |
| Calls reusing one function's defaults, warm | +0.12% to +1.65% | +0.17% to +1.20% |
| Positional-call control, warm | −0.33% to +1.60% | 0.00% to +0.82% |

The affected report creates one function and one lambda for each of
2,048 rule inputs, exercising positional-only and keyword-only defaults.
The local screen allocates 32.66% less, from approximately 7.926 MB to
5.337 MB. Accounted peak falls 2.53% and host allowance returns to
baseline. Control allocations are effectively flat. Retain the direct
construction and track the small adverse reused-default call result.

Warm includes mediated I/O, golden validation and prior-result cleanup.
Compile-to-result also includes compilation and compiled-owner disposal.
Startup, fixture reset and final cache teardown are outside elapsed.
Process CPU covers the wider request. Both 32-file .NET 11 RC3 worker
images verified. The five jobs passed the quiet gates, joined every
worker and archived their output under the lease.

Focused Release checks passed on .NET 10 and 11. The final ownership
regression compares state after both definition operations unwind.
It verifies that private defaults can be refunded on annotation failure
and a function exposed by a failing decorator retains its defaults and
their funding. The prior source fails the post-unwind allowance
difference in both execution modes, holding 94,405 bytes for private
failure and 94,901 bytes for escaping failure. An earlier version of
the test mixed the old map's scratch cleanup into the measurement.
Both reference results are archived.

Combined CI `44399326106` failed `GenericMethodDefinitionsRetainDefaultsTypeScopesAndRealClassCells`
in both modes on Windows and Ubuntu .NET 11. The test required the removed
128-byte temporary defaults map to stay reserved through decoration. Both
public suites passed all 18,081 cases. The corrected test verifies live
callback frames, the original default alias, governed tuple storage and the
function seen by decorators. All 26 method-body cases pass on Windows and
WSL .NET 10 and 11. The correction changes test code only.
Full Windows and Ubuntu Release qualification passed after this correction.
[Delivery](delivery/README.md).

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, tests and procedures](checks-and-procedures.tar.xz) ·
[Original CI failures and focused rechecks](method-boundary-checks.tar.xz)
