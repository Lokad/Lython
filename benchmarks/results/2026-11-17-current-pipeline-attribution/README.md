# Current normalization and protected-search CPU attribution

R462/B869,2026-11-17UTC,production46343804 / frozenaaef443f. Two bounded captures
after sequence construction; not a timing comparison or source-of-regression proof.

| Complete script | Guest clock | Active samples / unknown leaves | Selected exclusive mapped leaves |
| --- | ---: | ---: | --- |
| Normalization | 4.812s | 963 / 145 | Instruction prefix113; rstrip47; tab expansion41; rune width36; split28 |
| Protected file search | 4.196s | 926 / 323 | Instruction prefix79; native call entry50; native prefix43; native call35; iteration32 |

No ambiguous mapped leaves. Inclusive groups overlap useful/inlined operations
and are not removable shares or speedup forecasts. Selected active threads also
include worker reset/host work; search contains123nearest Reset frames and279
unknown kernel leaves. Guest clocks exclude reset/protocol while the CPU capture
is wider. Do not attribute all these samples to the guest or discard unknowns.

Current source confirms that call preparation packets/resume cycles were already
removed and small native helpers avoid heap arrays/frames. Larger calls use final
bound slots. Repeating the slower shared span kernel or generated native blocks
without a different displaced stage is unjustified. No new call-frame prototype
is selected from this capture.

A concrete remaining bulk-text stage is tab expansion's complete scalar/column
measurement pass, repeated by the writer. Next bounded source-backed design:
measure a cheap byte-level capacity bound, then do column/Unicode processing once
into final storage. Preserve exact representation-limit decisions (use exact
sizing before admission when a conservative bound exceeds Array.MaxLength),
Unicode columns/newline resets,negative widths,alias ownership,current tokens,
terminal refusal and full capacity funding. Conservative spare capacity can deny
earlier under a supplied allowance; quantify it. Retain only after semantic/
ownership checks and one complete normalization warm/fresh source pair with
unchanged call/CSV controls. No CPU gain inferred from41leaf samples. This is
one bounded pass-removal hypothesis,not completion of the performance objective.

Existing six-case catalog/worker/perf protocol,256invocation batches,8guest seconds
warmup per script,199Hz CPU-clock/frame-pointer perf and diagnostic perf-map
override. Two roughly5s capture windows,total9.009s guest clock below12s guard.
Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 natural tiering/workstation GC. No CPython,
compile,startup,RSS,pending-I/O or embedding-tail comparison. All identity/golden/
quiet/Closed/exit/join/archive gates pass. Every RAM trace byte SHA-verified
persistently under lease before unmount; no users/fixtures remain. No core copies
or retirement;254/252MiB staging/final floors maintained. Terminal cached forever.

[Attribution and unknowns](attribution.json),[image](identity.json),
[raw captures](observations.tgz),[procedures/custody](procedures.tar.xz).
