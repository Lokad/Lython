# Primitive counting increment — withdrawn source gate

2026-11-12, prototype over82ed92d4, not retained. Two production files pass a
bounded constant as long through the existing dictionary accumulation service,
removing repeated BigInteger struct transport/width/cast. The compiler already
proves this constant's range; the ordinary caller validates dynamic increments.
Existing values, promotion and effects remain unchanged in focused checks.

90internal/197public Release tests pass per actual .NET11/10 (574total). Three
complete text/report scripts, both APIs,64warmups/5rotated rounds8jobs preserve
full stdout/files, allowance and cleanup. Allocation flat. Ordinary text elapsed
-9.55..-9.07%Run(control0.52%),-4.12..-2.94%Async(control1.23%) is diagnostic.
Captured Run controls10.36%, capturedAsync+12.18..+25.47%with10.59%control;
reportRun+5.13..+14.75%with9.15%control, reportAsync-2.90..+12.03%with13.33%control.
No quiet VM collection, CPU claim, full qualification or publication.

Withdraw for the contract tradeoff: a previously checked internal integer argument
becomes a plain long with an implicit signed-seven-byte precondition. Enforcing it
inside the service recreates the removed check; a new wrapper type would add
machinery. This tiny change does not constitute the intended coherent redesign.
The original constant was already unboxed before the loop; remaining work is struct
transport/width/conversion, not a per-word heap box. No allocation saving is claimed.
Timing uncertainty is not a failed design or permanent ban; retain all observations.

Exact82ed production is restored from saved physical source, tests from Git;
nonincremental solution rebuild passes23.35s/zero warnings/errors; the coupled
identity regression passes per actual11/10 after restoration. No dormant new
signature or range contract remains. Continue with the complete ordinary helper
binding/workstate/pending-call lifecycle, whose guarded-script CPython gap remains.
