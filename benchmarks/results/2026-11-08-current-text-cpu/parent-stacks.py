from pathlib import Path
import collections,contextlib,io,runpy
r=Path(__file__).parent
with contextlib.redirect_stdout(io.StringIO()):data=runpy.run_path(str(r/'attribute.py'))
samples=data['samples']
for tag,tokens in [('monitor',['Monitor::Enter','Monitor::Exit']),('hash',['ComputeStructuralHashCode']),('input',['ExecuteInputInstruction'])]:
    counts=collections.Counter()
    for sample in samples:
        if not sample or not any(t in sample[0][1] for t in tokens):continue
        parents=tuple(name.split('(',1)[0] for _,name in sample[1:] if '[Lokad.Lython]' in name)[:4]
        counts[parents]+=1
    print(tag)
    for stack,count in counts.most_common(12):
        print(count,' -> '.join(s[-140:] for s in stack))
