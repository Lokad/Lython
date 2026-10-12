# Text-file handle owner qualification

All three fixed 5,000-open/close controls (read, write and append) failed at
262,144 bytes on production `9ee64618`: successful close released buffers but
the 64-byte shell charge remained after the handle was dropped. Matched retained
controls rejected under that same cap. Original binaries and failing receipts
remain preserved; these are correctness probes, not timing baselines.

Commit `f7dfb62113eb216bb773017bf2e4b998375b43d2` registers each text handle's
fixed shell snapshot with the existing owner pool before publication. Closed
handles and captured methods remain funded; dropped closed handles reclaim.
Write/append construction shares one factory. Read buffers and returned values
keep their independent ownership. Denied registration refunds only the unpublished
shell, and failed read construction still releases its private buffers.

The production change adds 21 lines and removes 20. It adds no ownership tier,
refcount, finalizer, configuration or forced-GC bypass. Ordinary registration
fees can reject very small budgets earlier; SPEC.md records this contract.
Host mediation, allocation preflights, end-of-run publication, failed-flush retry,
cancellation and managed stack/thread policies remain enabled.

Qualification covers dropped/retained handles, captured read methods, an escaped
Unicode read value through subsequent churn, exact shell reclamation, denied
writer registration, idempotent close and genuinely delayed read/append paths.
Four CSV/JSON conversion/parse source-closure checks are now permanent public
tests. They establish logical closure before guest recovery, not full parser
lease cleanup or independence from CLR reclamation.

The first full Debug run at f7dfb621 finished with 10,513 passes and two failures.
Two buffer-release tests assumed that a live file funded only its shell and used
a 256-byte budget for encoding. Commit `6d7fe99c936b92b59f2ba81789e168bdf5fddf4b`
adds exactly the ordinary owner-entry fee to those test budgets and checks that
flush returns to the funded construction baseline. The original buffer headroom
and unchanged 262,144-byte churn/retention caps are preserved.

Completed evidence:

- All **8,381 public Debug tests** pass at f7dfb621.
- All **2,134 white-box Debug tests** pass at 6d7fe99c after correcting those
  funding assumptions. Production and the public test tree are identical between
  the two producers. This is a composite Debug qualification, not a new single-head run.
- **123 focused Release checks** pass: 29 white-box at 6d7fe99c and 94 public
  at f7dfb621, including script outputs, failed-flush retry, publication and cancellation.

The focused Release groups take 1.145 / 7.642 seconds including cleanup, each
with a 30-second external cap. Full-suite gates have a separate finite limit;
their durations do not measure runtime performance or embedding latency.
Exact source/binary/scope/receipt identities are in [evidence.json](evidence.json).
Raw failures and corrected receipts remain under ignored
`.git/agent-notes/text-file-owners-20261012/`.

The production tree is `2a4b4441a1556ed6d0ca52a5d1b84f1b101b2284`.
Forced GC, escaping parser/row ownership and broader host-impact qualification
remain open. No performance, collection-count or latency improvement is claimed.
