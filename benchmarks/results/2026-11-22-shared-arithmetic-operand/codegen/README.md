# B1358 Local arithmetic code generation

The full frozen source pair ran the typed ASCII meter and arithmetic
range loop on local Windows x64 with .NET 11 RC3, with tiering disabled
to inspect fully optimized code. Both workers verified their images
and restored the shared allowance. [Quiet VM timing](../README.md).

The reference executable loop calls `TryEvaluateIntegerInstruction`
for integer operands. That method handles operand reads and attempts
the integer operation. The candidate first calls
`TryEvaluateBuiltinArithmetic`. That method has a 216-byte local stack
frame, calls the separate operand classifier and then calls
`TryEvaluateMachineArithmetic` for the integer case. The reference
integer method's local frame is 120 bytes. The figures are from x64
prologue stack subtraction and exclude saved registers.

The additional classifier and dispatch are concrete costs in this
Windows build. The timed Ubuntu results measure normal tiering and
leave attribution of their magnitude open. A future design should
preserve the existing integer entry path and demonstrate removed
work on a complete affected script.

[Disassembly, image identities and procedure](windows-fullopts.tar.xz).
