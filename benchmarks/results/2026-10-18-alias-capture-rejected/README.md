# Type-alias fallback shortcut rejected, 2026-10-18 UTC

The diagnostic prototype was rejected and all three production files were
restored byte for byte. The existing runtime is correct for these examples.
Six public regressions now protect lazy alias captures; all match isolated
CPython 3.13.2. No timing was collected and no optimization is shipped here.

| Pure program | Baseline | Prototype | Restored |
| --- | --- | --- | --- |
| Escaped nested factory | Pass | Missing outer value | Pass |
| Transitive nested factory | Pass | Missing outer value | Pass |
| Generic alias with lazy bound | Pass | Missing outer value | Pass |
| Alias created before local rebinding | Pass | Pass | Pass |
| Module late binding | Pass | Pass | Pass |
| Nonlocal editor and alias factory | Pass | Pass | Pass |

The [programs and owner checks](../../../tests/Lokad.Lython.PublicApi.Tests/LazyTypeAliasCaptureTests.cs)
run through both APIs on the baseline and restored runtime. Each fixture tries
synchronous execution first: the three prototype failures reject there, so
this experiment does not infer async results for those failed fixtures. The
three passing prototype controls also reach async execution.

## What the prototype tested

Baseline: immutable **6912bd8f40016f01453a568dcdffd329c8f5c6e6**. A
[three-file patch](rejected-candidate.patch) emitted an existing prepared-
definition operation for `type` aliases, calling the unchanged alias helper
and local synchronization. That removed the fallback-table entry and its
propagated mirroring requirement. It added no capture machinery, parser,
storage policy or execution mode. The [initial scope and strict rejection gate](scope.json)
were frozen before editing.

The alias helper retains live namespaces when it executes. That is too late
for an escaped factory whose outer function has already returned: the still-
lowered lazy value needs that earlier environment. Simply moving the declaration
out of the fallback table loses it. The observed failures say the free variable
`value` is no longer associated with a value in its enclosing scope.

Future alias lowering must preserve transitive captures before parent frames
end, using the existing executable/cell machinery or retaining the necessary
namespace path. Lazy values, bounds/defaults, rebinding and asynchronous
evaluation must stay correct together. This experiment starts no broader
capture redesign. The conservative choice is to keep the working fallback and
mirroring until their lazy consumers have a complete replacement.

## Validation and provenance

The baseline passes all six examples through both APIs. The prototype builds,
then passes three fixtures and fails three. It is restored immediately, without
timing, retrying the failing route or weakening assertions. The restored Release
build passes **129 focused typing/annotation cases**, including the new six.
Each new execution returns its shared budget to the compiled baseline; disposing
the script returns it to zero.

The restored public test assembly is byte-identical to the pre-prototype test
assembly. The restored core library matches the qualified explicit Release
package at `6912bd8f`, whose Windows/Ubuntu build/tests/package
[CI39110567807](https://github.com/Lokad/Lython/actions/runs/39110567807) is cached
green. No local full-suite, Stress or package repetition was needed for the
resulting coverage-only change.

[Validation](validation.json), [rejection](rejection.json),
[CPython goldens](python-goldens.json), [baseline](baseline-captures.trx),
[prototype](prototype-captures.trx) and [restored](restored-captures.trx) receipts
retain source hashes, case counts, owned-process limits and failure messages.
CPython was launched with `-I -S -X utf8`; these are semantic checks, separate
from the primary external performance baseline. Regex code/dependencies, runtime
ownership and managed stack protection are unchanged.
