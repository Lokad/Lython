import copy,hashlib,json,pathlib,shutil
root=pathlib.Path(__file__).parent
old=root.parent/'call-construction-b113/census'
root.mkdir(exist_ok=True)
shutil.copy2(old/'census.csproj',root/'census.csproj')
text=(old/'Program.cs').read_text()
text=text.replace('simpleReturn = Get(code, "SimpleReturn")!.ToString(),',
    'native = Get(code, "NativeReturn") is not null, nativeNodes = (Get(code, "NativeReturn") as Array)?.Length,')
(root/'Program.cs').write_text(text,encoding='utf-8',newline='\n')
catalog=json.loads((root.parent/'native-json-tokens-b123/catalog.json').read_text(encoding='utf-8-sig'))
other=json.loads((root.parent/'current-script-cpu-b107/catalog.json').read_text(encoding='utf-8-sig'))
catalog['cases'].append(next(c for c in other['cases'] if c['id']=='scripts.file-search.large'))
replacements={
 'scripts.csv-summary-helpers.large':("    return int(value)","    parsed = int(value)\n    return parsed"),
 'scripts.json-summary-helpers.large':("    return record['amount']","    amount = record['amount']\n    return amount"),
 'scripts.text-summary-helpers.large':("    return line.lower().split()","    normalized = line.lower()\n    words = normalized.split()\n    return words"),
 'scripts.file-search.large':("    return line.startswith('NEEDLE:')","    matched = line.startswith('NEEDLE:')\n    return matched")}
for case in list(catalog['cases']):
 if case['id'] not in replacements:continue
 variant=copy.deepcopy(case);before,after=replacements[case['id']]
 assert before in variant['source'];variant['source']=variant['source'].replace(before,after,1)
 variant['id']=case['id']+'.locals'
 variant['sourceSha256']=hashlib.sha256(variant['source'].encode()).hexdigest()
 catalog['cases'].append(variant)
(root/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
