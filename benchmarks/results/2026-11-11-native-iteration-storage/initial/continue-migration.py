from pathlib import Path
p=Path(__file__).parent/'migrate.py';t=p.read_text(encoding='utf-8')
helper=t[:t.index("edit('Runtime/LythonRuntime.Executable.cs'")]
work=t[t.index("edit('Runtime/LythonRuntime.Executable.NativeReturn.cs'"):]
work=work.replace("('result = PyNone.Instance;','result = default;',2)","('        result = PyNone.Instance;','        result = default;',2)")
work=work.replace("('            return result;\\n','',1)","('            context.State.SetCurrentException(entryException);\\n            return result;', '            context.State.SetCurrentException(entryException);',1)")
work=work.replace("('bool consumedByIteration = false)\\n    {\\n        if (index < code.FirstTemporarySlot)','ref ExecutableResult returned, bool consumedByIteration = false)\\n    {\\n        if (index < code.FirstTemporarySlot)',1)","('PyException? entryException,\\n        bool consumedByIteration = false)', 'PyException? entryException,\\n        ref ExecutableResult returned, bool consumedByIteration = false)',1)")
exec(compile(helper+work,str(p),'exec'))
