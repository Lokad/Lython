# Initial owned CSV range prototype and incomplete comparison

2026-11-14, R355/frozen510a3f86, two uncommitted production inputs changed from
qualifiedf5817a15/frozen9cfb. One parser uses ranges into owned raw input instead
of compacting ordinary field bytes before copying final independent values.
Borrowed strings first copy in bounded cancellable chunks; native files already
fill the admitted buffer. Only escapes rewrite within a field. No new owner,
backend, policy or raw-pointer borrowing. Raw syntax stays admitted until record
completion, which can require more scratch/earlier denial for escaped/multiline
or malformed borrowed input. No promotion or full delivery claim here.

1,116focused actual11/10 checks pass (119internal/421public plus18new each target),
including16native/borrowed CPython3.13.2 goldens and escaped multiline fields
across actual I/O/buffer reuse. Existing refusal checks preserve actual prefix/
capacity/funding rather than assuming a decoded private layout. Whole invoice,
fulfillment and unchanged text allocation are effectively flat (at most a few B).

B727is TERMINAL FAILED in copied measurement code: Worker routed diagnostic
IDs through undefined control_catalog, even though every case belongs in the one
supplied catalog. Two normal phases completed; later controls/final quiet gate
are missing. These values are diagnostic only, regardless of direction:

| Completed diagnostic phase | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
|scripts.csv-fulfillment.large /warm|-3.18..-2.03%|-2.34..-2.18%|
|scripts.csv-fulfillment.large /compile-run|-3.78..+0.67%|-4.50..-0.12%|

All original bits/source/checks, failed condition, raw partial observations and
adverse points are retained. All workers joined and the empty RAM fixture was
archived/verified and unmounted. Failed raw SHA256: `288b1fe049564df3727d1afea42b6ba5936e159d95781dacdf57329c40d39e2f`.
This is measurement uncertainty, not evidence that the runtime design failed.

Earlier setup corrections also survive: the first proposed list-digit control
was not borrowed CSV input and was replaced before timing; real borrowed rows and
escaped-SKU data were independently verified by isolated CPython and both actual
frozen workers. A wrong local canonical-archive member lookup stopped before VM
work; a local omitted-jobs metadata error occurred after first launch and was
repaired without restarting it. Historical72c9 aliases were retired only after
canonical Git/path/UID/full-alias/byte proof; current9cfb/510a and all auxiliary
images/catalogs/toolchains/floors remain.

A bounded confirmation may remove only stale collector routing and run diagnostic
cases first, reusing exactly510a/9cfb/data/settings under a new owner. It must
preserve this failed collection and independently obtain all final gates before
deciding CPU/scratch/complexity tradeoffs. No automatic restart, broad campaign,
external-baseline update or performance-objective completion. Publication held.

[Partial timings](partial-summary.json),[failed closure](failed-closure.json),
[source/core/tools/checks/failed raw archive](sources-checks-and-failed-observations.tar.xz).
