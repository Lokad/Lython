# CSV invoice field and line-number allocation attribution

R549 reuses the verified R545 allocation trace of the 1,024-row CSV
invoice on `33025cee`. No workload was rerun. The three sampled types
below account for much of the observed allocation weight.

- Of 2,942 `PyString` triggers, 2,940 have the parser's `FinishRecord`
  in their ancestry. These fields are already copied into admitted final
  inline storage or shared payload windows. The invoice uses all five columns.
- All 909 `PyDict` triggers reach `RecordKeys.TryAdoptRecord`. That path
  already transfers, without copying, the funded record array into a dictionary.
- Of 2,209 `BigInteger` triggers, 809 have `DictReader.NextAsync` or
  `GetFieldNamesAsync` as their nearest Lython frame. Another 1,139 reach
  `ExecutableSlot.PublishInteger` and a native function. The reader ancestry
  corresponds to 8.93% of the trace's allocation weight. Method attribution
  cannot identify every originating expression or prove a CPU share.

`DictReader` currently boxes line counters during ordinary native reads and
compares them with zero through the generic Python binary operator. A bounded
redesign can keep its physical counter in the existing integer slot and publish
the Python object when the value is read. Guest-assigned values still need
their truth and equality protocols, and repeated reads still need the same
identity. Preserve updates through lazy headers, leading blanks, exceptions,
reentrancy and async I/O. This source proposal has no measured savings yet.

[Sampled origins](origins.json) · [Inspector](Program.cs.txt) ·
[Original trace and verification](../2026-11-18-invoice-tuple-storage/README.md)
