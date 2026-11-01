# Casing final-output cancellation correction

2026-11-01, source46ed95e5 (core source9003d045; ordinary adapterd703767c).
This correctness correction is independent of the B76 bulk-casing prototype,
which is absent from this change. No performance gain is claimed.

## Demonstrated problem

The host allowance may request cancellation while admitting the final UTF-8
output. Lower/upper/capitalize/title/swapcase then return that private result
without another native completion check. All five new controls fail against
isolated original core SHA256
`9fa3f53570baa97deb7d62f8ff5e03968aaa873bcd629ee6b3de1ea21c5feb6f`:
Assert.Throws observes no exception. The exact same fresh test assembly is copied
to a private output tree with only that original core substituted. Current build
outputs are not replaced. This is an owned semantic reproduction, not a timing.

## Shared invariant and coverage

Every casing method checks before final projection, then shares one completion
check after the existing output allocation. If cancellation/failure occurs, only
the private unpublished result refunds its existing ownership snapshot. Input
aliases remain live/funded. Builder cleanup is unchanged and unconditional;
first-origin refused admission remains terminal. No extra public option, memory
layer, collection, quota, worker, backend or dependency is introduced.

Fifteen independent controls cover all five methods: cancellation signaled during
final-output funding, refusal of working construction, and refusal of result
storage while working storage remains owned. They assert original input, first
structured refusal, current cancellation, zero reservation/returned private bytes
and reusable shared allowance. Growth hooks describe allocation phases rather
than exact execution checkpoints or fuel/call counts.

Fresh nonincremental Release solution build succeeds. Focused checks pass on
actual Windows.NET11RC3/.NET10.0.8:269 white-box and633 public cases per target.
Existing Unicode/context/expansion goldens, retained methods/results, host
suspension/cancellation, input/copy overlap refusal and shared-budget controls
remain unchanged. The three-buffer allocation assertion remains unchanged too.
Full Windows/Ubuntu, both-target ordinary/original Stress and explicit Release
installed-package qualification belongs to this correction's delivery checkpoint;
its outcome is pending rather than inferred from focused results.

[Exact reproduction](baseline-final-cancellation.log), original failing TRX and
[fixed binary identities/check hashes](qualification.json) preserve the evidence.
The separate B76 equivalence probe and any later performance trial do not qualify
this correction's cross-platform delivery or predict a latency improvement.
