from pathlib import Path
import hashlib, json, re, subprocess, tarfile, textwrap
root=Path('D:/code/Lython'); home=Path(__file__).parent; out=root/'benchmarks/results/2026-11-09-private-small-integers'
baseline=json.loads((home/'baseline.json').read_text(encoding='utf-8'))
for name,expected in baseline['productionSources'].items(): assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==expected,name
for variant in ['', 'refined/']:
    identity=json.loads((out/variant/'identity.json').read_text(encoding='utf-8'))
    for phase in ['warm-sync','warm-async','compile-sync']:
        data=json.loads((out/variant/phase/'measurements.json').read_text(encoding='utf-8'))
        assert all(data[k] for k in ['allGoldensPassed','allBalancesZero','ownedFixturesRemoved','imageIsolationVerified'])
    with tarfile.open(out/'source-checks.tar.xz') as tar:
        name=('wrapper' if not variant else 'refined')+'/Lokad.Lython.dll'
        assert hashlib.sha256(tar.extractfile(name).read()).hexdigest()==identity['cores']['candidate']
        assert hashlib.sha256(tar.extractfile('catalog.json').read()).hexdigest()==identity['catalogSHA256']

test=root/'tests/Lokad.Lython.PublicApi.Tests/GuestIntegerRepresentationTests.cs'; text=test.read_text(encoding='utf-8')
python='C:/Python313/python.exe'; results=[]
for expression,expected in re.findall(r'\[InlineData\("([^"]+)", "([^"]+)"\)\]',text):
    answer=subprocess.run([python,'-I','-S','-c','print('+expression+')'],text=True,capture_output=True,check=True)
    assert answer.stdout.strip()==expected
    results.append(dict(source=expression,stdout=answer.stdout))
source=textwrap.dedent(re.search(r'const string source = """\n(.*?)\n\s*""";',text,re.S)[1])
wrapped='def job():\n'+textwrap.indent(source,'    ')+'\nprint(job())\n'
answer=subprocess.run([python,'-I','-S','-c',wrapped],text=True,capture_output=True,check=True)
assert answer.stdout=='True\n1 float True True\n123456789\n',answer.stdout
results.append(dict(source=source,adaptation='Wrap guest top-level return in one ordinary function; print its result.',stdout=answer.stdout))
(out/'reference-check.json').write_text(json.dumps(dict(python=subprocess.check_output([python,'--version'],text=True).strip(),
    sourceSHA256=hashlib.sha256(test.read_bytes()).hexdigest(),cases=results,
    boundary='Independent Python values/stdout only; public CLR BigInteger types and memory ownership are checked in both .NET target suites.'),indent=2)+'\n',encoding='utf-8')
readme=out/'README.md'; text=readme.read_text(encoding='utf-8')
text=text.replace('all original failure/build/check logs remain in qualification/.',
'''original failure/build/check logs remain in qualification/. One intermediate
823/824 passing summary was overwritten by the corrected824/824 run; its failure
was the same remaining concrete-constant assertion in the preserved nine-failure
log. Raw measurement captures were not overwritten.''')
text=text.replace('823/824 passing summary','823-passed/1-failed internal log')
for old,new in [('a24B','a 24 B'),('a32B','a 32 B'),('reserves32B','reserves 32 B'),('58production','58 production'),
                ('save6.91%','save 6.91%'),('below0.04%','below 0.04%'),('Initial176','Initial 176'),('passes16838of','passes 16,838 of '),
                ('16931 and fails93','16,931 and fails 93'),('affected821','affected 821'),('all733existing','all 733 existing'),
                ('pass824focused','pass 824 focused'),('all16modified','all 16 modified'),('run48retained','run 48 retained'),
                ('eachactual11/10','each actual .NET 11/10')]: text=text.replace(old,new)
text += '\n[Independent CPython semantic controls](reference-check.json) pass under local CPython3.13.2; they are not timings.\n'
readme.write_text(text,encoding='utf-8',newline='\n')
print('Six lane gates, both measured core/catalog identities, exact restored production and five independent Python semantic controls pass.')
