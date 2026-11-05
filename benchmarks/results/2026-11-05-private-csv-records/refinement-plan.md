# One transfer path refinement

Before inspecting initial timings, remove the draft's extra source-list forwarding
methods. One source TryMoveNext/Async returns the record array; csv.reader and
fieldnames call the existing narrow adoption factory explicitly. This deletes an
extra async forwarding state machine and20 lines rather than retaining a second
return-shape API for white-box convenience. Update retained-public-row tests to
exercise actual CsvReaderObject adoption; preserve all ownership assertions.

Initial core77b20fbd/source snapshot and B257 raw conditions stay immutable.
Repeat the same five jobs/boundary/warmup/controls after focused validation; one
refinement, no automatic subsequent retiming. Original failed quiet pre-gate also
remains. No API, parsing, budget policy or public semantic change.
