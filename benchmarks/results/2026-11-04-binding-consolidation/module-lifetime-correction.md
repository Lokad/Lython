# B197 module binding correction

Initial source passes764/776 focused internal cases;12 existing OwnedFunctionLocals
cases fail because context.Variables no longer contains f after module frame cleanup.
The candidate incorrectly pre-interned ALL module bindings as transient locals.
Normal module definitions/imports previously lived directly in the durable namespace;
forcing new broad mirroring would add cost/lifetime changes rather than simplify.

Correct the model: consume complete lexical binding facts for FUNCTION locals only,
retaining their parameter prefix. Modules have dynamic namespace semantics; their
actual executable stores already intern the slots they need. Remove the duplicate
module prepass too, but do not pre-intern import/def/class namespace names. Earlier
module loads still use ordinary name lookup; later stores and globals bridges use
existing frame/name paths. No new module mode/collector, no widened mirror policy,
no changed tests or relaxed ownership assertions. Preserve initial patch/failures.
Recheck builtin fallback, namespace mutation/order, retained function context,
closures and generators before comparison. This is correction before qualification,
not a measured performance result.
