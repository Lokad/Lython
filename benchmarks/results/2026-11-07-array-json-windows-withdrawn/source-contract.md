# R60 source boundary and limits

Only plain JSON tokens in a string owned by the same execution governor share
their immutable backing. Before admission/callbacks, source backing becomes batch
storage. Shell refunds return shell/cache charges; the full array/header stays
funded until the governor's execution scope ends. Views/nested views have their
own admitted metadata. Other-owner/unowned input and rewritten escapes copy.
No root inventory, reachability walk, identity ledger, new owner or forced GC.

The single storage-start field distinguishes original ownership, batch-retained
original backing and view position. Its logical byte start never changes after
construction; an original source changes from owned start zero to retained start
zero. Managed spans/memory keep checked bounds. All direct array consumers were
changed to use that logical window; only constructors, accessors and backing
accounting retain raw-array access.

Internal FromOwnedUtf8 adopts its array. Mutating that array afterward is not an
allowed ownership use. Independent unowned/other-owner input tests remain;
public host values/projected results keep their existing independence. Small views
can retain whole large inputs until batch end, with conservative earlier denial.
Plain-token denial now tests actual admitted output metadata, preserving refusal
before publication and cleanup, rather than an obsolete per-token copied size.

One atomic cached hash replaces separate value/ready publication. Zero remains
zero but can recompute, without caller-token retention. Managed construction
allocation is unchanged on the measured x64 runtime. No CPU claim follows from it.
The first complete script run shows stable adverse CSV; the explicit owned
zero-offset accessor refinement is a hypothesis under separate measurement.
