# Close ownership fixture correction

Original full run43223095054 fails two whitebox cases on both platforms at
`closed < before`. Sharing transfers the complete window to execution/result
ownership; closing the handle must keep it funded. Both17,443-case public suites
pass. Stress/.NET10/package steps were skipped after the failure, not qualified.
Original logs/terminal state are preserved and cached.

The corrected fixture checks actual retained backing capacity and idempotent close.
Added short inline-line cases still require `closed < before`, preserving private
window reclamation detection. Four combinations cover immediate/delayed hosts and
shared/independent results.68focused actual.NET11/10 case-executions pass (34each),
including refusal/cancellation/pending users. Production code/bits are unchanged;
no performance retiming is required. Full corrected-source delivery follows.
