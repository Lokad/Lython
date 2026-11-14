# Complete invoice and search stage attribution

2026-11-14, R350/B721, exact committed `e235b18b` / frozen `71ba421c`.
Existing quiet F4as_v7 Ubuntu24.04.4/.NET11RC3 worker/optional0.4adapter,
two5s199Hz cpu-clock/fp captures after8guest-second warmups. Invoice batches64,
search512 avoid consuming a capture in one oversized invoice request. Perf maps
and JIT disassembly are explicit diagnostic overrides. This is CPU attribution,
not a normal timing, allocation-stack or CPython comparison.

| Complete script | Guest CPU | Leaf samples | Unknown leaves | Ambiguous |
| --- | ---: | ---: | ---: | ---: |
| Invoice,4,096records /127,031B | 5.30s | 1,053 | 96(9.12%) | 0 |
| Search,2,049lines /42,010B, stop after1,025 | 3.79s | 742 | 142(19.14%) | 0 |

Search also uses1.02s on a thread-pool worker; all thread deltas and raw stacks
remain available. Guest-only selection does not describe all embedding CPU.
Ordinary dispatch, native entry/body, admission and useful record/scalar work are
material owning costs; the families include inlined work and are not removable
shares. Unknown kernel/runtime leaves limit attribution.

Every native-body leaf maps to one unique same-process Tier1 range/disassembly:
117invoice leaves,11,568bytes/387groups;42search leaves,5,405bytes/142groups.
Invoice's largest13-leaf group mixes instruction metadata and cancellation; other
groups include real lookup/hash/arithmetic. Search has19leaves in a managed
prologue (2.56%of guest samples). Neither establishes a large redundant activation
or operand packet. Preserve earlier slower arena/control lessons, without making
them permanent design bans.

Source inspection also rules out attractive but stale removals: native calls
already bind final slots; private DictReader rows already bypass guest lists and
adopt funded arrays/shared keys. CSV record construction already combines major
field/row admission; fixed-size scalar arithmetic avoids allowance transactions.
Native eligibility includes live default storage. Tuple copying and CSV decoding
have useful retained-value semantics; their hot methods alone do not establish an
unnecessary complete stage.

Close this bounded source gate without a speculative local patch. Continue
dispatch/operand construction analysis from these owning facts before selecting
one coherent representation removal, preserving identity, mutable defaults,
promotion, genuine suspension, cancellation, terminal refusal and retained owners.
No new engine, memory policy, once-cached mutable-default proof or universal value
rewrite follows automatically. The wider performance objective remains active.

All source/image/auxiliary/golden/balance/quiet/join/archive gates pass. No workers
or RAM fixtures remain; each RAM byte is independently archived/verified before
unmount under120s ownership. Only three duplicate working raw archives were
retired after exact canonical Git/SHA/path/UID/terminal proof, with all current
images/catalogs/toolchains and254/252MiBfloors preserved. Canonical evidence
remains immutable. Raw SHA256:
`4bb297596bb7906fe188561c84ecfcf7fa2d3db745ebc36d7d389aa0a7eed7f4`.
R349 full delivery43571864663 is independent and pending at capture time.
Publication/tagging remain held.

[Decision](decision.json),[invoice](attribution-invoice.json),[search](attribution-search.json),
[raw](observations.tgz),[collection/analysis and source-gate history](collection-and-analysis.tar.xz).
