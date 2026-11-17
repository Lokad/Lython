# Current complete helper workload CPU attribution

R468/B874, 2026-11-17 UTC, qualified ccf20ba3 / frozen 7a599347. The existing
sampling procedure captures complete helper-list text normalization and report
rendering after R464 removed continuation allocations. No source or elapsed
comparison, no new benchmark framework or prediction of removable CPU share.

Text: 943 selected active-thread samples, 120 unknown leaves. Report: 962 samples,
128 unknown leaves. Instruction dispatch, native helper calls and text processing
all contribute; inclusive groups overlap useful/inlined work. Text native admission
and managed stack checks remain visible. Report has 88 sampled reservation stacks:
32 string views, 19 JSON containers, 20 formatted operations and 17 native format
operations. Those observations do not justify removing containment or assigning
all admission overhead to JSON. Full per-thread CPU, unknowns and raw stacks stay
in the evidence; this capture establishes no CPU improvement or regression.

Source gates reject already-removed stages: multi-part f-strings write into one
final UTF-8 builder, split construction batches shells/shared backing, and small
helpers avoid continuation arrays. Do not reopen shared-call kernels/native blocks
that measured slower, or remove managed stack checks based on these samples.

One remaining complete-operation candidate is JSON string token scanning:
ordinary UTF-8 bytes still pass through a per-byte delimiter/control loop. The
next bounded prototype replaces that loop with bounded bulk searches in the same
decoder, preserving escape processing, UTF-8 defense, exact errors, existing
preflights and ownership. Report and JSON-lines scripts are affected; helper text
is the unchanged control. Allocation is expected flat; CPU benefit remains a
hypothesis. No new parser, memory credits/ledger, policy, cache or option. The
[source assessment](source-assessment.json) records acceptance and a single short
warm/fresh source-pair decision. Whole-script objective remains active.

Each process warms at least 8 guest-clock seconds, followed by about 5 seconds
of sampled full requests (4.88 / 5.06 guest-clock seconds). Existing quiet Azure
F4as_v7, Ubuntu 24.04.4, .NET 11 RC3, normal tiering/natural workstation GC;
DOTNET_PerfMapEnabled=3 is a diagnostic override. perf cpu-clock 199 Hz / frame
pointers samples wider work including reset, protocol and validation. It is not
an exclusive guest-time decomposition. Final cache cleanup is outside capture.

All 32 image files/catalog identities, complete output/file goldens, quiet gates,
Closed/exit/join and persistent archive checks pass. Both workers/profilers joined;
all RAM trace bytes archived and SHA-verified before fixture unmount. One exact
committed B873 raw JSON replica (409,284 B) retired after fresh owner/path/hash,
link-count and all-inode-alias proof. Canonical archives, current images and
toolchains retained; 254/252 MiB staging/final floors preserved. Terminal cached;
no requery or unchanged profiling/qualification replay. Publication held.

[Attribution](attribution.json), [reservation callers](reservation-callers.json),
[raw samples/maps/records](observations.tgz), [procedures](procedures.tar.xz),
[identity](identity.json), [custody](archive-receipt.json), [terminal](terminal-status.json).
