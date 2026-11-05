# Separate quoted-EOF correctness correction

After the measured723b9638/c10ddd55 source, CPython3.13.2 confirms non-strict
quoted EOF emits the final row; strict EOF raises csv.Error('unexpected end of
data'). Route that final row through existing FinishRecord admission/publication,
then await cursor disposal and release only private scratch. No second parser,
new memory owner, buffering policy or performance claim.

Release build26.90s/zero warnings/errors. Focus107internal/366public CSV per
actual.NET11/10 pass;18final native-input cases per target also verify escaping
host results remain charged until disposal and return allowance to baseline.
Existing cancellation/refusal/retention controls remain; new controls cancel
during final-row construction and refuse its admission before publication.
Full platform/stress/installed qualification will batch at the next delivery.

One initial new refusal test incorrectly required a raw private Finish call to
throw again after failure had already advanced field state. Corrected assertion
checks the execution context's actual terminal admission boundary and identical
first origin. Production needed no extra policy/checkpoint. This failure and
all development logs remain in ignored notes/csv-eof-d45. No timeouts weakened.

The prior measured source's full local suites each pass4109internal/15993public
on actual.NET11/10; their logs are included here. Its independent full delivery
42025698023 is GREEN/all eight required gates, cached and associated with723b9638,
not this later correction.
