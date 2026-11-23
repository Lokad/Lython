# B1519 allocation attribution in the retained CSV reports

The exact `fb3894153` image runs locally on Windows x64, .NET 11 RC3.
Both 4,096-record workload goldens match CPython. Each worker warms
before a six-second EventPipe allocation sample, for 32 verified batches.
Both workers and collectors join. These are allocation tick weights, with
request and cleanup costs included. They estimate allocation distribution.

| Type | Explicit-float share | Typed ASCII share |
| --- | ---: | ---: |
| PyString | 58.51% | 27.03% |
| Boxed Double | 14.79% | 37.55% |
| Object[] | 10.27% | 13.46% |
| PyTuple | 8.98% | 12.07% |

Every explicit-float string tick resolves nearest CSV `UnpackRecord`.
That method also accounts for 58.98% of the typed report's double ticks.
The remaining typed double ticks and all tuple shells resolve nearest
`PyTuple.FromSlots`. The still-observable totals tuple is built from
per-row arithmetic and assigned as the dictionary value.

The next design assessment should check when a field can remain in owned
UTF-8 storage until observed. Failed conversion, dynamic functions,
callbacks, module bindings and field aliases can still observe original
CSV strings. A candidate needs a real removed materialization with those
observations intact. Earlier direct numeric slot ingress and tuple float
layouts have adverse elapsed observations. Keep those controls in any
revised comparison.

[Stacks](summary.json) · [Identity](identity.json) ·
[Raw traces, checks and procedures](observations-and-procedures.tar.xz).
