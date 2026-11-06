# F01 post-capture source decision

No production prototype. Source dd76df36 remains unchanged.

1. The exact-capacity idle workspace is not a primary-workload bottleneck:
   text/JSON/report native-return helpers borrow arguments and avoid its frame;
   CSV repeatedly binds the same 9-slot body. Do not change it on this hypothesis.
2. Removing DictReader's temporary row array alone is not yet a substantial
   redesign. The actual parser already batches field-payload admission and
   constructs independent final strings; copying is one pointer per column.
   The dictionary is already capacity-prepared. It does not repeatedly reconstruct
   the header list. B150's earlier bulk construction is retained, not pending.
3. Unifying parser record projection could remove a complete staging phase, but
   current FinishExhausted finalizes an EOF record before awaited source disposal
   and scratch release. Deferring materialization changes that order and funding.
   A parser-owned deferred view would also cross reentrant sources and mutable
   headers, and cannot survive ReleaseScratch. A callback/sink added solely to
   avoid a small array is unjustified before a coherent lifetime design.
4. CSV's per-call ExecutionContext and executable interpreter remain distinct
   allocations. Current samples (binding28/frame creation16/BeginPendingCall17)
   do not establish them as dominant, and unknown/TLS/write barriers cannot be
   assigned to them. B61/B69 retained or moved semantic contexts and added saved
   state/arena operations. Their measured slowdowns remain relevant.
5. Another concrete phase is ordinary function free-name lookup. Existing compiler
   infrastructure uses LoadGlobal only for explicit global directives, with local
   slots and known closure candidates handled separately; remaining names use
   general ResolveName. CSV resolves `int` this way. The current57 ResolveName
   samples include necessary work, not a removable fraction. Standalone class and
   generator compilation, type-parameter scopes and inlined comprehension binding
   are real consumers of dynamic lexical resolution. Do not globally replace it
   or add a per-load cache/guard before the compiler can prove binding facts.

Advance the roadmap's complete lexical/call/frame construction source design,
with these actual displaced phases and explicit retained obligations. A justified
next design may fuse invocation context/state or fully prepare lexical resolution,
but must remove the displaced storage/route. Avoid speculative pooling, saved-call
records around existing objects, another profile campaign or scalar fast paths
presented as whole-script redesign. Source-gate completion does not close the
performance objective. All adverse B307/B308/B309 observations remain visible.
