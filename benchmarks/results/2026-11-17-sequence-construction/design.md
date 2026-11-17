# R460 native sequence construction

Private base82e45812 plus archived R458 guarded8dd7eda1, not production. Reuse
PyList private capacity and rollback for tuple construction; retain exact native
integer tuples and direct object-array/Span construction. Transfer exact-sized
owned arrays; otherwise admit a trim copy with full replacement overlap. Preserve
large-scalar aggregate estimates before publication, removing per-item scalar
reservation growth. A temporary metadata reservation funds the private builder
beside the surviving tuple shell. No new owner, policy, arena or representation.

Share low-level sync/async append loops across list constructors and extension,
retaining their distinct source preparation/self-snapshot semantics. Existing
engine bulk borrowing supports only the two native storage types (List<object>
and PyList); check actual live PyList capacity after guest mutation, and clear the
borrow before the final ordinary Add can admit growth. No Python consumer callback
is batched. Preserve current tokens, pending disposal, reentry and exception state.

Contract tradeoff: conservative per-capacity scalar headroom may deny text tuples
earlier; no tiny-budget equivalence. Complete input consumption precedes final
aggregate retention admission; public inputs/operations retain their existing
owners through construction. Preserve major scalar payload estimates, terminal
refusal, rollback and retained aliases. No per-object ledger or exact reclamation.

Focused semantics/cancellation/ownership first, then one complete affected
constructor/normalization warm/fresh source pair with numeric/CSV controls. No
unchanged retiming or full platform/package repetition before a retained coherent
milestone. If no storage/code is removed or wider ownership machinery is needed,
stop/refine explicitly; do not automatically broaden the redesign. Publication held.
