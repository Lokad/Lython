# R579 and R581 delivery checkpoint

[CI 44412602615](https://github.com/Lokad/Lython/actions/runs/44412602615)
ran source `0547e5489`. Windows .NET 11 passed 4,897 white-box and
18,083 public tests. Windows .NET 10 passed all 4,897 white-box tests
and 18,082 public tests. One JSON preflight test observed 131,952
process-wide bytes by the end of failure cleanup, above its 131,072-byte
limit. [Original Windows failure](windows-net10-failure.txt).

The fixture now measures the encoding thread from the host gate to the
first denial, before exception formatting and teardown. The same byte
bound, terminal shared-memory failure, suppressed later output and
complete refund are checked. All eight focused cases passed locally on
Windows .NET 10 and .NET 11. Include the correction in the next full
Windows and Ubuntu delivery batch.
