# D62 before first build: remove duplicated local-target metadata

After writing the first source prototype, the adjacent store already contains its
local target. Keep no duplicate target property in the producer. Complement the
existing original live extent as a direct-result marker; decoding recovers that
same extent. ResultSlot stays physical. The store retains both target and span.
No added fields/limits/phase state. Original selected design contract is preserved;
this refinement happens before any build or timing and removes redundant data.

Retire original inputs above the consumed result slot. If cleanup is pending,
use the existing funded operand and adjacent store after the original driver
awaits disposal, preserving cleanup-error routing. Otherwise forget consumed
input without disposing it (same as overwrite), check the original store span,
commit using StoreLocalValue and skip that store. No new async publication stage.
