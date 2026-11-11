# R231 — synchronous call-contract source gate

2026-11-11, runtime fully qualified4aa, evidence HEADbe08b541. **No runtime edit or
working performance prototype retained.** Exact178 original physical source/test
files are restored. Nonincremental solution build23.96s/zero warnings/errors and
44public call/lifetime/default/named-binding checks per actual.NET11/.NET10 pass.
Prior full runtime/platform/stress/package qualification stays cached; no replay.

The existing ICallable has array Invoke plus borrowed InvokeBorrowed. General sync
borrowed calls materialize/clear a private packet. Known functions/bound methods/
binders consume spans directly. The attempted canonical-span sync signature removes
the default copy and forwarding shims, reaching descriptors, constructors, generated
methods, modules and protocol callbacks. [Initial scope](before.json).

The first compiler pass identifies three signature/duplicate-forwarder corrections;
the second exposes82 real consumer mismatches. Many are shared async core helpers
and array-typed delegates, not dormant compatibility code. Their inputs can live
through callbacks, constructors or cache/descriptor work. Merely wrapping them in
new ToArray/clearing adapters relocates the phase rather than completing the desired
removal. A coupled migration must consume inputs into actual prepared state before
suspension and preserve its retained ownership. No new owned-callable hierarchy or
generic dispatch/arena framework is selected to hide this work.

This source gate does not establish a failed performance design: there is no valid
candidate, semantic qualification, allocation observation or timing. Compiler errors
show scope/ownership constraints, not a speed result or permanent architectural veto.
Current text call-entry4.29% samples alone do not justify the repo-wide rewrite.
[Decision and guarded source identities](decision.json).

Continue with the existing constructor CSV source. Its ordinary __init__ stores an
instance field, requiring context promotion and first attribute-capacity growth.
Assess that actual per-record construction boundary while preserving the canonical
constructor/initializer selection and real dynamic protocols. No repeat profile or
new benchmark framework follows from this signature-only gate.

All original/partial source, exact patch, compiler errors/corrections, restoration
build, commands and focused outputs survive in [preparation](preparation.tar.xz),
verified by [archive identity](archive-identity.json). The partial source is private
evidence, not an alternate backend or build option. Publication postponed.
