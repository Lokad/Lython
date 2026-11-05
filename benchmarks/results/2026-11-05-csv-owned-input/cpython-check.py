import csv,io,json

rows=[]
for text in ('a,b\rc,d\r','a,b\n\n','a,b\r\n\n','\n\n','bad\nnext'):
    try:value=list(csv.reader([text]));error=None
    except Exception as ex:value=None;error=(type(ex).__name__,str(ex))
    rows.append(dict(input=text,value=value,error=error))

class Interrupted(io.RawIOBase):
    def __init__(self):self.data=b'"prefix\n'+b'x'*20000+b'tail",v\n';self.position=0;self.failed=False
    def readable(self):return True
    def readinto(self,buffer):
        if self.position<8:count=min(len(buffer),8-self.position)
        elif self.position<1032:count=min(len(buffer),1032-self.position)
        elif not self.failed:self.failed=True;raise OSError('interrupted read')
        else:count=min(len(buffer),len(self.data)-self.position)
        buffer[:count]=self.data[self.position:self.position+count];self.position+=count;return count

raw=Interrupted()
reader=csv.reader(io.TextIOWrapper(raw,encoding='utf-8',newline=''))
try:next(reader)
except OSError:print('read',reader.line_num)
row=next(reader)
print(len(row[0]),row[0].startswith('prefix\n'),row[0].endswith('tail"'),row[1],reader.line_num)
generic=csv.reader(['a,b\n\n','"prefix\n',42,'tail",v\n','last,row\n'])
print(next(generic),generic.line_num)
try:next(generic)
except csv.Error:print('bad',generic.line_num)
print(next(generic),generic.line_num)
print(next(generic),generic.line_num)
print(json.dumps(rows,ensure_ascii=False))
