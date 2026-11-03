# Reader-owned UTF-8 windows (B183)

Retained storage simplification, measured2026-11-03. Baseline is B178 casing source,
fully qualified as737a67e9, frozen core
`1daa28e48982052e006ce3a6b00ceebdc153b2cc874be72d4dbd0ea70d52d2cb`.
Candidate is3eee2a0e plus source.patch, frozen core
`0f143a6c68393a6149202e6238e8e6e4d62e61e775b545bd2868bc4d38aa47a9`.
These are the actual PRECOMMIT builds. SourceLink revision changes can change later
binary hashes; no claim of identical CI and measured binaries. No external CPython
ratio or general CPU improvement is established here.

## Removed work and ownership

The text reader no longer creates a per-window immutable string/copy for unchanged
valid UTF-8 or uses its lazily allocated rune-offset table for bounded reads. One
ReadOnlyMemory window borrows the admitted raw prefix; transformed codecs retain
their existing owned output charge. All encodings compact raw carry only after
window retirement. Returned strings always admit/copy into independent immutable
storage. Raw storage and decoded capacity remain funded through actual users.

Shared UTF-8 identity validation removes the reader-only copyInput option, redundant
strict validation/exception-rescan helper and unused strict decoder instance. Invalid
input/newline/error-handler work stays in the shared managed decoder. A final
cancellation check refunds only the unpublished decode result, preserving its input
alias. No new owner/pool/cache/ledger, allowance, resource policy or background work.
The scalar scan inspects at most the requested prefix, avoiding repeated scans of
the remaining ASCII window during read(1).

## Complete-job results and uncertainty

Existing protocol2, three independent Lython workers/case, two old roles with the
SAME frozen bits. Eight cases through both APIs; normal GC/tiering, quiet VM lease,
Standard_F4as_v7 Ubuntu24.04.4/.NET11 RC3. This bounded round uses1.5s adaptive
warmup, seven rotating150ms target batches and0.5s settle. Do not pool these medians
with older3s/nine200ms collections.

Warm clocks include invocation, actual mediated tmpfs file I/O, complete output/
file checks and previous-result cleanup. Compilation/startup/fixture reset and
transport/final cached-result/program/fixture teardown excluded; all cleanup joined.
Full 3,072-scalar read controls assert every character against CPython3.13.2 goldens:
UTF-8 read(1), UTF-8 read(17), UTF-16 read(17), preserving CRLF and astral characters.

| Complete job | Run elapsed vs old | Unchanged old control | RunAsync elapsed vs old | Unchanged old control | Allocation change, both APIs |
| --- | ---: | ---: | ---: | ---: | ---: |
| CSV4,096 records |+1.23%|+0.53%|−2.49%|−3.06%|−0.99–1.05%|
| Text3,072 lines |−1.90%|−4.69%|+0.44%|−3.93%|−1.33%|
| JSON4,096 records |+1.60%|−0.39%|−2.51%|−4.77%|−2.65–2.66%|
| Report3,072 records |−3.39%|−0.33%|+0.74%|−1.02%|−2.74–2.75%|
| Config7,177B |−0.20%|−1.21%|−12.36%|−3.94%|−2.66–2.67%|
| UTF-8 scalar reads |−11.62%|−2.83%|−55.56%|−53.05%|−2.19–2.20%|
| UTF-8 block reads |+1.19%|+5.53%|−2.07%|−10.16%|−15.17–15.23%|
| UTF-16 block reads |−17.86%|−0.54%|−8.93%|−18.88%|−10.45–10.49%|

Allocation is a SEPARATE existing shared-ALC diagnostic:32 warmups/image, five
rotated16-job batches and200ms settle. It includes fixture reset/goldens/result
cleanup; images share CoreLib/hash/GC. Its short CPU/elapsed numbers are diagnostic.
Savings per complete script are about58KB CSV,36KB text,112KB JSON,191KB report,
7KB config. Accounted peaks fall8,337/3,200/12,416B for CSV/text/JSON; report/config
peaks are flat. Read-control peaks fall12,324B; all result/cache/root balances pass.

Elapsed remains mixed: do NOT claim the55% async scalar change when identical old
workers differ53%. Several other async controls differ10–19%; text straddles old
roles. Sync report/UTF-16 and async config are useful observations, not a general
CPU proof. Sync CSV+0.7–1.2% and JSON+1.6–2.0% vs both old roles are provisional
regression debt; do not hide them behind memory savings. B174 CSV/B171 report debt
remains separate. The next coherent combined-storage/CPython checkpoint is the
completion decision for these tradeoffs; no third narrow source variant selected.

Retain for eliminated copies/cache storage and simpler funded window lifetime,
with the uncertainty above. Percentages are evidence, not automatic vetoes.

## Validation and preserved evidence

512 internal /3,148 public focused Release checks PASS per actual .NET11/.NET10:
chunk/BOM/CRLF/astral alignment, codecs/error payloads, read/readline/scalar sizes,
gzip and delayed-I/O cancellation, terminal refusal, retained results/budget reuse.
A finite allowance admits one raw window plus a small result but cannot fund a
second full window. Retained output survives multiple raw-buffer reuses and close.
Cancellation raised during decode-result admission refunds just that result in
all four error modes. Casing and decoding reuse the same existing test budget hook.

Original new-test balance failure omitted mock transport storage refunded by close;
its expectation now uses the observed transport charge, retaining storage bounds.
Initial local golden check compared Windows CRLF stdout literally; normalize only
the printed count line. Source assertions/input bytes never changed. No timed sample
existed in either preparation correction; preparation-notes.md preserves conditions.

Every source/fixture/output/worker hash, sample and warmup, quiet before/after gate,
filesystem and owned service log is preserved. Collection167.560s, owned168.177s
exit0; all goldens/joins/ownership checks pass. Persistent evidence and frozen
workers, RAM guest fixtures only. Retrieved archive SHA256 verified:
`5ed5655097247b8d197834ea7948690985656b3fb993a22d5f1a4f028d71994c`.
Full delivery qualification is GREEN on exact7a3a8afe: Windows/Ubuntu, actual
.NET11/.NET10 Release, installed package consumers and original Stress.
[Terminal41717353893](https://github.com/Lokad/Lython/actions/runs/41717353893)
and delivery-ci-status.json retain all eight passed gates. Never re-query terminal
evidence. Publication and tagging remain postponed.
