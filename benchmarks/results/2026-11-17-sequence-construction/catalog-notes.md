# Targeted constructor workload

Reuse the seven existing complete scripts unchanged. Add sync/async hosting
variants of the existing JSON-to-Markdown report: materialize rendered records
as tuple(render(record) for record in records), then write those lines. Input
JSON, render helper, header, output files and stdout goldens remain unchanged.
This is an additional ordinary script shape, not a causal comparison against
the old script's streaming error order. Both source roles run the same new
script; original fixtures/goldens are independently checked with CPython.
Async hosting uses the existing completed-I/O comparison host; genuine pending
I/O/cancellation remain focused correctness checks, not latency claims here.

One seven-job source pair: normalization warm; async tuple-report warm/fresh;
ordinary generator-to-list warm/fresh; numeric and CSV warm controls. Four-second
full-request warmup, existing two-process/five-rotation protocol, external240s
service budget. No further unchanged retiming or new benchmark framework.
