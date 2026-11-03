# B139: native UTF-8 hash handoff, withdrawn

2026-11-03. No production change or prototype-specific test survives this trial.
The existing B138 runtime at802fb318 is restored and fully qualified. Publication
remains postponed. All results, including adverse storage and CPU data, are retained.

Source showed three byte-hash passes on a new JSON name: execution-cache lookup,
insertion and first guest-dictionary use. The prototype replaced the separate ROM
comparer with PyString.Utf8Key, carrying one content hash into an independently
admitted string and the index. It reused existing capacity admission and ownership,
without a new parser, pool or policy. This removes real work, but grows keys from
16 to24 bytes on64-bit and index entries from32 to40 bytes before buckets.

| Complete job | Candidate vs802 elapsed | Unchanged802 control | Allocation vs802 |
| --- | ---: | ---: | ---: |
| Report warm | +0.946% | +0.687% | +0.00059% |
| JSON-lines warm | -0.343% | +1.893% | +0.00130% |
| Short unique-name warm | -0.899% | +0.043% | +4.855% |
| Long unique-name warm | +1.035% | +0.578% | +1.862% |
| Text warm control | +0.841% | -0.428% | not measured |
| Report compile-to-result | -1.309% | -1.292% | not measured |

The same run also includes pre-B138fd7bc7e4 and its unchanged control. Short unique
candidate elapsed is +43.591% versusfd7, control -0.570%; the existing802 interner
shows a similarly large cost in that run. Do not substitute earlier independent
medians to infer a source gain. Report remains faster than pre-B138; this patch
adds no established report elapsed gain. Short unique weighted process CPU changes
-6.578% versus802/control -1.985%; long -3.571%/control0%. These diagnostic changes
do not establish a useful elapsed improvement or resolve the separate index cost.
Fresh report CPU +11.207%/control +4.373% is adverse and retained.

**Withdrawn:** the extra key/factory/hash API and increased index allocation do not
justify essentially flat complete-script elapsed results. This is an evidence-based
tradeoff, not a fixed percentage veto. No further timing attempt was selected.
Hash work alone does not account for B138's remaining unique-input regression.
The old comparer and factory are restored; there is no dormant alternate key type.

The long-name control preserves the short case's1,024 four-field records and sum,
adding64 Greek-character suffix pairs per key. Source is unchanged; UTF-8 input
and golden identities are recorded. Its isolated CPython3.13.2 golden agrees.
It is a diagnostic control, not a new external CPython performance claim.

Quiet Azure Standard_F4as_v7/Ubuntu24.04.4/.NET11RC3. Existing workers/protocol,
four-second warmup per image, nine interleaved approximately200ms batches, six jobs,
five images: fd7/candidate/fd7-control/802/802-control. Warm excludes compilation
and startup; fresh includes per-invocation compile/dispose. Timed work includes
fixture reset/staging, efficient mediated I/O, full stdout/file validation and
prior-result cleanup; final result/program/process/fixture joins occur outside
that batch timer. Process CPU is Linux/proc TGID user+system through response,
including runtime/JIT/response, not guest-only CPU. Before/after quiet gates pass;
this remains diagnostic rather than long sampling qualification.

Separate allocation uses the existing complete-Run driver with802/new/802-control
sharing CoreLib/hash seeds,192 warmups and nine-by32 jobs per case. Its elapsed
numbers are not the timing verdict. The full owned service exited0 in247.925s;
all workers/fixtures joined/removed, unit inactive, shared lease free.

Core SHA256: fd7 baseline547825f9dfe52ddf22e24e76b8bc54ae8975428dae5b5e22052dd223624d2c42;
802 previous380b78d49359b22ba73fe337ef400f4d1949485f6ef6e9945bfae49d86a9d108;
candidatebc8fac46f779aa544f444bf1329f668a4423c93d97b68112caf19ac42686bdbd.
Frozen after all checks joined; auxiliary worker bits unchanged. Source/test patches
are compressed evidence, not retained implementation.

Focused prototype323 internal/1535 public per actual.NET11/10 PASS. Four internal
handoff cases checked ordinary dictionary/hash compatibility, empty/default/sliced/
Unicode/long keys, independent bytes and budget cleanup. Those tests solely
exercise the discarded type and were removed with it; B138's public semantics,
reentrancy, alias, cancellation/refusal and useful ownership checks remain.
After restoration: fresh nonincremental solution build, zero warnings/errors;
319 internal/1535 public per actual target PASS. The cached full delivery
41554275552 at exact802fb318 covers Windows/Ubuntu ordinary.NET11/10, original
Stress both and installed package consumers. It is terminal/success; do not query
it again or repeat unchanged runtime qualification.
