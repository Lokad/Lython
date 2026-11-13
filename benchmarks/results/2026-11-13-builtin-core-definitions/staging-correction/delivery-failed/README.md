# Corrected runtime, original allocation observation: failed delivery

CI43446552390 on cb8736ff/frozen5a184a1d passes the complete Windows11/10,
original stress and installed-package gates. Ubuntu11 ordinary tests pass;
Ubuntu10 public tests fail one existing staging allocation assertion (94592
process-global bytes versus65536). Its later package/stress gates are skipped.
This is not full cross-platform qualification. The unchanged runtime subsequently
uses same-thread allocation observations with equal-thread assertions, retaining
the original bound, refusal, publication and ownership checks.

Full logs,12step summaries and all573source matches are preserved here. Do not
requery this terminal run or discard its original failed condition.
