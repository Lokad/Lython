# Builtin-root identity correction — R36

The invocation-setup source gate found an unrelated correctness defect: user classes
named object/type inherited name-based root immutability/instance decisions. Original
42101cd5/frozen6ab7367f neutral-handler probes demonstrate it; CPython allows writes.
Real builtin mutation was already correctly rejected. Replace display-name tests
with the existing self-metatype/original-object-base relation, consolidated on PyType.
No new root registry, flags, shared graph, owner, public API or performance claim.

Eight independent CPython3.13.2 goldens cover both names, instance dictionaries,
errors, descriptors, original aliases and a supported subclass of type. Both public
APIs preserve original builtin rejection and restore the shared budget after cleanup.
Focused Release986internal/2,765public pass on actual.NET11 and.NET10, including
class/attribute/constructor, cancellation, refusal, ownership and native-call cases.
Build0warnings/errors. Full platform/Stress/package qualification remains separate;
the prior42101cd5 delivery does not qualify this source.

The initial fixture used explicitly unsupported metaclass= syntax. Only that new
case failed (986internal/2,764public passed on.NET11). Preserve its source/log and
replace it with a supported type subclass; do not expand Python support or relax
the assertion to conceal the test mistake. A separate initial shadowed-type probe
handler masked its first error; retain it and the corrected neutral-handler probe.

[Focused checks](focused-initial.json), [Python goldens](python-goldens.json),
[all conditions, sources, commands and logs](development.tgz).
Graph sharing remains unselected without descriptor/owner/isolation proof and
current cost evidence; existing fixed functions/signatures are already shared.
