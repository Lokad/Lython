# Lython development adapter: bounded native range matching

R507,2026-11-18. Explicit frozen producer7e683b2d source, current Lython60da2cf4
production/measured65fcf7d0. Independent development qualification, not a normal
adapter upgrade, release/package qualification or backend performance claim.
Core dependencies and the normal optional/nonpackable adapter0.4pin are unchanged.
The sibling working tree was not read or modified; build inputs are committed blobs.

New cursor modes support Search/Match/FullMatch with explicit original-subject
byte ends. The development adapter deletes detailed-match snapshots and their
separate text-capture copying helper, using its existing native range copy for
both single-shot and incremental operations. Completion-order lastindex comes
from cursor metadata. The producer's new caller capture slots on one-shot Match
do not expose that metadata, so cannot alone implement Lython's match contract.
Capture count now uses explicit producer metadata. Original bytes/windows,
coarse preflight ownership, current compile/match/advance tokens and cleanup stay.

Both actual.NET11and10 pass363public regex checks, including real backend work
interruption, refused admission/unwind, retained owners, windows, captures,
replacement/splitting/errors/effective flags and shared-budget reuse. Private
parent and subprocess core/adapter/producer hashes are identical and verified;
the earlier mismatch negative controls are not replayed. New source build uses
AllowUnsafeBlocks=false. Producer Core still contains Unsafe.SizeOf calls:
this development build is not policy certification or a production dependency
promotion. No unsafe C#, Unsafe API or native probing is added to Lython.

Remaining precise final-integration limitations: UTF-8 group-name enumeration,
creation/preparation token detached from later retained pulls, and separate
per-match result staging admission. Existing UTF-16-name/current-token/coarse
storage bridges remain adapter-local; no quota or core infrastructure is added.
New producer capabilities do not block independent Lython work. Final adapter
qualification against an immutable released package and its focused performance
comparison remain separate. Neither unchanged dev105qualification nor an elapsed
source comparison is repeated here; allocation/CPU gains are unmeasured.

[Source/capability facts](source-facts.json),[exact parent/child identities](image-identity.json),
[checks](checks.json),[adapter/procedures/logs](adapter-and-checks.tar.xz),
[frozen producer inputs](producer-source.tar.xz).
Publication remains postponed; the whole-script performance roadmap stays active.
