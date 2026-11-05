import json
records=[]
for n in [1021,1023,1024,1025]:
 for strict in [False,True]:
  p='a'*n
  token='"'+p+('\\n\\"\\\\\\u00e9' if strict else '\n\\"')+'😀'+p+'"'
  expected=p+('\n"\\é' if strict else '\n"')+'😀'+p
  value,end=json.JSONDecoder(strict=strict).raw_decode(token+' ignored')
  assert value==expected and end==len(token)
  records.append(dict(prefix=n,strict=strict,passed=True))
print(json.dumps(records))
