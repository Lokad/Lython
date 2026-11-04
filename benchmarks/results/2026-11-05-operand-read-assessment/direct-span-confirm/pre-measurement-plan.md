# Direct operand spans: one representation simplification before decision

2026-11-05. Preserve B231initial218s/adverse text+4% and B232getter-only28s:
unchanged initial controls differ2.45%, refinement+1.43/−0.99%; no demonstrated
getter benefit or CPU cause. Do not launch more native getter/JIT hint variants.

Reconsider the pre-code direct-span shape on simplicity, not a fixed memory-percent
veto. Replace compact tagged origins with two optional original-span references in
the immutable instruction. Delete negative span-tag encoding/decoding, constant-pool
growth/copy, tag-aware native getters and related invariants. Restore original raw
native links. Ordinary consumers read their span directly; forwarding still removes
the same14/9/11/6/7module instructions and initialized-local inventory. Retain all
pending protocol packets, receiver evaluation, iterator ownership and read-order
fixes. No new abstraction, memory subsystem, mode or execution route.

Tradeoff: instruction40→56B; predicted module instruction storage+21–28%, about
0.7–1KiB for these scripts, before any smaller/larger compilation allocations.
Borrowed compilation allowance/metadata headroom and conservative execution operand
capacity remain. No claim of lower admitted peak or retained heap. The runtime
source is simpler than the packed variant; CPU benefit still requires measurement.

Focused Release checks on actual11/10, same independent public error goldens.
One existing two-job complete textwarm/CSVwarm source pair versus qualifiedf6,
two unchanged baseline roles/6s warmup/9rotated200ms samples/<=90s owned wall.
If useful, require final eight-case warm/async/fresh/compile qualification before
retention; otherwise withdraw D18 and retain semantic regressions/evidence, rather
than a new local shape/hint series. No permanent design ban; any later attempt needs
a distinct cost/lifetime proof. Preserve every original failed/adverse condition.
Whole-script objective continues beyond this bounded design assessment.
