# Owned JSON-name set — B153, withdrawn

2026-11-03. Both variants are **WITHDRAWN**. The original execution-owned
Dictionary<ReadOnlyMemory<byte>,PyString> source is restored exactly. No HashSet,
alternate lookup request, comparer factory, new owner or PyString field survives.
There are no architecture-only tests to delete. Existing independent JSON tests stay.

The index duplicates byte-memory key plus owned string value. A HashSet of owned
strings halves stored-entry width and reuses the string's existing hash for normal
insertion/guest dictionary access. Initial alternate lookup carried a stack request;
refinement uses native byte spans and one execution-owned, funded comparer. Neither
changes guest names, records, hooks, parser, options, owners or global string layout.
This is a distinct approach from B139's larger stored keys, not their revival.

| Complete script / phase | Initial elapsed / control Δ | Refined elapsed / control Δ |
| --- | ---: | ---: |
| Report warm |+2.20 /−0.54%|+3.09 /−1.84%|
| Report compile-run |+6.51 /−2.55%|−2.16 /−3.37%|
| JSON-lines warm |+1.13 /−2.45%|+1.41 /−0.74%|
| Short unique names warm |−9.62 /+1.27%|−11.12 /−2.08%|
| Long unique names warm |−3.59 /+3.21%|−7.45 /−5.37%|
| Config warm |+3.45 /+5.83%|−0.34 /−2.04%|
| Config compile-run |+4.03 /+0.99%|−1.51 /−1.67%|
| Unchanged text warm |+6.60 /+6.86%|−3.07 /−6.45%|
| Unchanged CSV warm |−0.46 /−0.24%|−1.38 /−5.16%|

Lower is better; each compares its own same-source baseline. Short unique allocation
−9.84/−9.81%, long−3.73/−3.72%; ordinary report/JSON allocation is effectively unchanged.
[Admitted peaks](accounted-peaks.json) retain all images; these are not physical RSS.
Refined report warm external CPU+4.65%/control−1.84%; short unique−12.08/−1.07%.
Fresh/long CPU controls are unstable (including config CPU shifts around−30–38%);
no precise CPU speedup follows those rows. Every adverse/unchanged control remains.

**Decision:** smaller unique-name storage and useful unique-input gains are real,
but repeated-schema report remains adverse after the native-span refinement. The
extra lookup/comparer machinery does not justify this representative cost in the
current priority. This is not a percentage veto, permanent rejection or proof that
layout/controls are fully explained. No third variant or unchanged retiming selected.
B138's older unique-input debt remains OPEN; different-boundary independent medians
do not quantify remaining debt or establish a composite gain. Work moves to config
execution, whose current CPython gap is materially larger.

Baseline exactbd3f9dee3d0f372f6738c1973cb042bd12903e20, core
578496bb2cd47ba826729dca9fe2cbadf277597caafe518d745e36a2d2e2f92c.
Initial core54cd2a6684408c195ef78158ddae844e764aeb391570e53ccc907e638f259d06;
refinement85bc20eced41c1842111abcf525df739639ce1784e9a86104278e1d1210aa25a.
Exact patches/hashes and normalized unchanged helper/adapter bits are recorded per
variant. All local build/test handles joined before freezing. The recorded lookup
size correction (40-byte estimate versus32-byte fields) did not alter measurements.

Quiet Standard_F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Existing protocol2
workers, four-second warmups, nine rotated~200ms batches, nine complete warm/fresh
jobs plus four separate allocation jobs. Exact canonical and short/long unique
catalogues are reused; isolated CPython3.13.16 verifies both unique goldens. This
is a source pair, not a new CPython ratio. Execution excludes reset/staging/startup/
transport/final owners; includes mediated guest I/O, full checks and prior-result
cleanup. Fresh also compiles/disposes. External process CPU includes reset/runtime/
response and is not guest-only. Allocation reuses the unchanged B150 helper (legacy
CSV purpose label); actual IDs/catalogues/hashes define it, and its elapsed is only
diagnostic. Hash witness/image isolation/goldens/zero balances/fixture cleanup PASS.

Initial/refined services exit0 in229.202/230.326s; both before/after quiet gates PASS,
all workers joined, owned fixtures removed and lease free. Each variant172 internal/
803 public Release cases per actual.NET11/.NET10 PASS, including live aliases,
mutable borrowed input, callbacks/cancellation, terminal refusal and budget reuse.
After restoration, nonincremental build0 warnings and same172/803 cases per target
PASS. The unchanged retained runtime's full41600267923 qualification is cached in
../2026-11-03-bulk-text-operations/delivery-ci-status.json; no duplicate full run.
Publication stays postponed; whole-script performance remains open.
