from pathlib import Path
r=Path(__file__).parent
s=(r.parent/'closure-operands-r235/inspect-jit.py').read_text(encoding='utf-8')
s=s.replace("case=json.loads((r/'catalog.json').read_text(encoding='utf-8'))['cases'][1]", "case=next(c for c in json.loads((r/'catalog.json').read_text(encoding='utf-8'))['cases'] if c['id']=='scripts.report-render.large')")
s=s.replace('*ReadOperand* *ExecuteInputInstruction*','*ReadNativeValue* *TryReadRecordValue* *TryReadNativeStringValue* *MaterializeNativeValues*').replace("lane='warm'","lane='warm-async'")
(r/'inspect-jit.py').write_text(s,encoding='utf-8')
