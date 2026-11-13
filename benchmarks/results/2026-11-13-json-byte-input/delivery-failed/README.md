# Initial JSON byte-input delivery: stale diagnostic assertions

CI43473827059 on23a65e9e/frozenbff487b5 fails five existing diagnostic-message
assertions per platform. Both Windows and Ubuntu.NET11 white-box4691/4691pass;
public17597/17602pass. Remaining.NET10, stress and package steps are skipped.
All576frozen production inputs match the exact commit. This is not full delivery.

The five invalid inputs (int, float, list, dict, None) are correctly rejected by
LA3158 saying str or bytes. The old test expected string-only wording. Preserve
all five rejection/no-runtime-failure checks and add the diagnostic-code assertion
while updating wording. No runtime change, timeout relaxation or timing replay.
Original terminal state, complete job logs and source proof are retained here.

The corrected round-trip/byte-input/module checks pass135cases per actual.NET11/10.
