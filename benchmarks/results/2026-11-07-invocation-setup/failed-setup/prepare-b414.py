from pathlib import Path
base=Path(__file__).parent
assert (base/'failure-receipt.json').exists()
s=(base/'prepare.py').read_text(encoding='utf-8')
s=s.replace('r=Path(__file__).parent;home=r.parent;',"base=Path(__file__).parent;r=base/'b414';r.mkdir();home=base.parent;")
s=s.replace('b413','b414').replace('B413','B414')
s=s.replace('def adapt(s):return',"def adapt(s, collection_root='invocation-setup-b414-20261107'):return")
s=s.replace(".replace('report-cost-b410-20261107',name)",".replace('report-cost-b410-20261107',collection_root)")
s=s.replace("contract='''R56/B414", "contract='''B413 original setup failure is preserved/verified locally under a fresh maintenance lease; no guest/profiler started. adapt() incorrectly captured a filename-loop variable for the root. B414 binds the intended root explicitly; all intended source/core/worker/input/profile sizes/gates unchanged. R56/B414")
exec(compile(s,str(base/'prepare.py'),'exec'),{'__file__':str(base/'prepare.py')})
r=base/'b414';root='/home/lokad/lython-bench-checks/invocation-setup-b414-20261107'
assert ('root='+root+'\n')in(r/'run.sh').read_text(encoding='utf-8')
for name in ['ops-profile.py','archive.py','retrieve.py']:
 text=(r/name).read_text(encoding='utf-8');assert root in text and '/lython-bench-checks/run.sh'not in text and '/lython-bench-checks/retrieve.py'not in text
print('Corrected root binding statically verified across collector/runner/archive/retrieval before launch.')
