# Original full qualification failure

Run 44247104176, committed 7db1de02, Windows/Ubuntu: eight existing
InstructionFailureRoutingTests failed on each platform. Cancellation/admission
raised by the next instruction entry incorrectly acquired producer-operation
Python context when result forwarding performed that check inside the producer.
Full downstream qualification did not run; this run is never treated as green.
Inputs and complete logs are retained; no assertions were weakened.
