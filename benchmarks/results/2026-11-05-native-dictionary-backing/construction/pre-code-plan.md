# D40 unknown JSON object construction refinement

Before editing,2026-11-05. Initial4b61edd7 B263 completed161s/all gates, source,
all samples and adverse report allocation preserved under native-dictionary-d40/.
CSV warm−3.9–7.1%,ordinary fresh−4.5–5.3%,JSON warm−4.7–5.8%; allocation−3.3–14.8%.
Report elapsed−.55/+ .98% with1.52%control is inconclusive, allocation+5.13%.

Source cause: JSON passes capacity1 after seeing its initial property quote. The
old BCL made at least3 slots; the new compact table rounds1 to2. Three-field
report records therefore allocate two-slot backing plus a growth reservation
before the final four-slot backing. The observed extra196,602B/3,072 records is
approximately64B/record, consistent with those changed construction costs.
No new trace or speculative runtime-state explanation is needed.

Change that unknown-width producer's hint to4, keeping empty objects at0 and
all other known dictionary capacities compact. No global minimum/floor, record-
schema prediction, pre-parser, alternate backing or new owner. Eliminate the
intermediate two-slot backing/loan for ordinary3-field objects. Two-field JSON
records retain more capacity; expect less allocation reduction there than the
initial variant, not an automatic universal win. First refusal may occur earlier.
Retain terminal admission, replacement peaks and current callback/cancellation
and empty/truncated-input behavior. Native-table mutation remains atomic as in
the BCL; private growth/collision work checks cancellation. No new limitation
or time policy is hidden.

Rebuild nonincrementally; same focused actual11/10, then same six whole-script
jobs/controls/warmup/boundary/gates in<=180s. No added framework or CPython replay.
Keep initial source/adverse results and choose retain/refine/withdraw/debt after
this bounded source refinement. All rate estimates remain unchanged. Corpus
counts: CSV4,096records/5,120physical lines including quoted newlines; JSON4,096
records plus1,024blank lines; report3,072records. Existing input counts are correct.
