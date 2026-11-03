import glob,re
names=["", "a", "a/", "a/b", "a/x/b", "a/x/y/b", "a/.x/b", "a/b/", "a.py", "x/a.py", ".a.py", "x/.a.py", "a/.hidden/b", "😀", "🙏", "🚀", "a😀b", "a::b", ".hidden", "b", "q", "-", "[", "]", "a\n", "x\n.py", "a\n/b", "a/.", "a/..", "a\\b"]
patterns=["*", "**", "a/**/b", "a/**/**/b", "**/*.py", "a/*/b", "[.]hidden", "[!a-c]", "[a--c]", "[😀-🙏]", "a😀b", "a::b", "*/", "", "?.txt", "[[]", "[]]"]
separators=["/", "/\\", "😀"]
values=[]
for separator in separators:
 for recursive in [False,True]:
  for hidden in [False,True]:
   for pattern in patterns:
    regex=re.compile(glob.translate(pattern,recursive=recursive,include_hidden=hidden,seps=separator))
    for name in names:
     values.append('1' if regex.match(name) is not None else '0')
print(''.join(values))
