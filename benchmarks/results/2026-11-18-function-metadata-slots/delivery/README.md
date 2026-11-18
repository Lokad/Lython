# Function construction and metadata delivery

[CI 44417439480](https://github.com/Lokad/Lython/actions/runs/44417439480)
ran source `2215ec040`. Ubuntu .NET 11 passed all 18,088 public cases.
Seven white-box cases failed. Two expected deletion of standard function
attributes to allocate a dictionary entry even though the wrapper now
funds the field when created. Five found that reusing the annotation
branch operation for a function declaration triggered unnecessary mirror
storage for the entire module. [Original failures](ubuntu-net11-failures.txt).

The compiler now uses its lexical variable-annotation fact for that
mirror decision. The existing tests still require ordinary annotated
functions to stay executable without unnecessary module mirroring.
The fixed-storage test now constructs its function through execution
and verifies that reset still works with the allowance fully reserved.
Custom-attribute admission refusal, retained alias and funded capacity
assertions remain in the suite.
