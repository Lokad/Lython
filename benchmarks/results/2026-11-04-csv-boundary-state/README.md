# Bounded CSV boundary/state parser

2026-11-04. One parser skips ordinary UTF8 payload in bounded spans and handles
structural byte candidates in five explicit record/field states. Verify full
Unicode dialect scalars after lead-byte searches. Remove four overlapping flags,
three cached option references and matching helpers; **20 net production lines
deleted**. One conversion flag remains for QUOTE_NONNUMERIC. Preserve existing
B150 native record storage/field descriptors/batched construction, working and
retained owners, immediate numeric conversion/error timing and ready queue.
No alternate parser/cache/pool/configuration/owner/collector or unsafe code.
Caller source/DictReader async machinery is unchanged. Stack-local boundary sets
allocate no managed storage;4096-byte working/check bounds remain. Growth signals
observe cancellation before further parsing/publication.

Five states also preserve record presence after ignored spaces: all-space records
produce one empty field, and repeated space delimiters skip leading spaces at the
next field. Two new public CPython goldens fail on saved5b36b41d (13 older blank
cases pass) and pass through both APIs/current targets. Existing101 internal/347
public per actual11/10 pass, including numeric error/source-pull ordering, mutation,
retained headers/rows/aliases, real suspended callbacks, refused admission, actual
cancellation during growth, scratch cleanup and budget reuse.84 independent
CPython3.13.2 writer/reader cases match per API/target, with shared Unicode lead
bytes, NUL, multiline and4096 boundaries. [Validation](validation.json), [exact
sources, new goldens, RED logs and all checks](source-and-validation.tgz).

## Combined delivery comparison

This compares **D10 bulk newline plus D11 parser** to qualifieda00fe76d/frozen1dcca6f2;
it does not isolate D11 or add earlier percentages. Candidate9562510b plus parser
patch,frozen21cddae7. [Exact identities](candidate-identity.json). Existing complete
sources/fixtures reused, including B150 ragged/long-field cases. Quiet AzureF4as_v7,
Ubuntu24.04.4/.NET11RC3, normal GC/tiering, identical auxiliaries/CoreLib/hash
witnesses and three isolated images (two qualified-source controls).

| Complete workload / phase | Candidate elapsed | Change vs both qualified roles | Same-source spread | Candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| Short CSV / warm;4,096records/57,361B |5.1718ms|+3.48% /+0.98%|2.48%|3.04%|
| Short CSV / fresh;4,096records/57,361B |5.1480ms|−0.42% /−2.64%|2.28%|0.30%|
| Ragged CSV / warm;1,024records/235,537B |8.1916ms|−0.05% /−0.87%|0.83%|0.86%|
| Long-field CSV / warm;16records/524,717B |0.2789ms|−86.99% /−86.99%|0.00%|1.02%|
| Text helpers / warm;3,072lines |2.9803ms|−3.22% /−3.35%|0.14%|1.18%|
| JSON-to-Markdown / fresh;3,072records |4.9526ms|−0.49% /+0.09%|0.58%|2.00%|
| Short CSV / compilation |81.987µs|+1.06% /−1.07%|2.16%|0.22%|

Negative means less elapsed. Long-field process allocation−2.54%; other rows
essentially unchanged. Retain coherent simplification, correctness fixes and the
large complete long-field improvement. Short CSV warm has possible+1.0..3.5%
debt with similar control/IQR dispersion; fresh is slightly better/control-limited.
Ragged and report near unchanged, text modestly shorter but old-role IQR2.7–3.1%
limits precision. Compilation straddles controls with unchanged frontend. Keep
all adverse values and earlier isolated D10/D7 debt; do not chain percentages or
claim a broad CPU/RSS/embedding-latency improvement. [All metrics](assessment.json).

Seven-case193s collection ends normally below300s. Existing driver only:6sec guest
warmup/image, alternating preparation,2sec settle,9rotated200ms baseline-sized
samples. Actual mediated RAM-file I/O, complete stdout/file checks and result
cleanup included; fixture reset/staging/startup/hash/final cached teardown excluded.
Fresh includes compile/dispose; compilation separate. All exact images, independent
CPython3.13.16 full goldens, per-job owner baseline/finalzero, fixtures/quiet gates
pass. Service inactive/MainPID0, global lease free; no background work abandoned.
[Plan](pre-measurement-plan.md), [raw observations](observations.tgz), SHA256
`32d07c941dd35068796b9b3130db17d01dc249e2c4ee811085462c88b6c403b9`.

Full Windows/Ubuntu original Stress/installed Release consumer qualification is
pending the combined D10/D11 delivery checkpoint. An earlier green revision does
not qualify these changes. The primary external CPython table still measures
qualifieda00; this source pair is separate. Publication remains postponed.
[Independent producer capability observation](producer-observation.md) does not
change core dependencies or the normal adapter pin.
