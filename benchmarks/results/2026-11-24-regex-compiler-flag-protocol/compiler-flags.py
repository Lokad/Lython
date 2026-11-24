import re


def check(expression, subject, options, fail=0):
    trace = []

    class Program:
        def __and__(self, bit):
            trace.append(("compiler-and", bit))
            if len(trace) == fail:
                raise LookupError("compiler operation")
            return options & bit

        def __rand__(self, bit):
            trace.append(("compiler-rand", bit))
            return options & bit

        def __or__(self, bit):
            trace.append(("compiler-or", bit))
            return options | bit

    class Requested:
        def __and__(self, bit):
            trace.append(("requested-and", bit))
            return 0

        def __ior__(self, bit):
            trace.append(("parser-ior", bit))
            return bit

        def __ror__(self, state):
            trace.append(("program", state))
            return Program()

        def __or__(self, state):
            trace.append(("reported", state))
            return re.UNICODE

    try:
        value = re.compile(expression, Requested())
        print(repr(expression), repr(subject), value.flags, value.search(subject) is not None)
    except Exception as error:
        print(repr(expression), type(error).__name__, str(error))
    print(trace)


check("x", "X", re.I | re.U)
check(".", "\n", re.S)
check(r"\w\s", "é ", re.U)
check(r"\w\s", "é ", 0)
check(r"(?i:x).", "X\n", re.S)
check(r"(?<=a+)x", "ax", re.U)
check("x", "X", re.I | re.U, fail=7)
