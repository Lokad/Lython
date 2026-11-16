# R458 refinement

Original B861/B862 source99e6554d shows repeat normalization gains and supported
ordinary-generator adversity with moving controls. Restore original single-item
Advance/AdvanceAsync; buffer only at existing instruction-loop yield boundaries.
This removes per-yield engine exits as well as repeated guarded entry for private
drains. The frame borrows already-admitted List capacity and clears the borrow
before user observation/growth/cleanup. Leave one final value for the ordinary
Advance result, preserving exhaustion/return semantics. Pending disposal takes
the ordinary yielded path before another instruction. No second generator engine,
new owner, quotas, cache, compiler recipe or configuration. Revalidate focused
semantics/ownership/current tokens, then one changed-source whole-script pair
with controls. Initial and confirmation evidence remain intact.
