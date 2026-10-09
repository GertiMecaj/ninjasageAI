import struct,zlib
from pathlib import Path
class Reader:
 def __init__(self,b):self.b=b;self.p=0
 def u8(self):v=self.b[self.p];self.p+=1;return v
 def u30(self):
  v=0
  for i in range(5):
   c=self.u8();v|=(c&127)<<(7*i)
   if c<128:return v
  return v
 def take(self,n):v=self.b[self.p:self.p+n];self.p+=n;return v
class ABC(Reader):
 def __init__(self,b):
  super().__init__(b);self.take(4)
  for k in range(2):
   for _ in range(self.u30()-1):self.u30()
  self.take(8*max(0,self.u30()-1));self.string_count_pos=self.p;string_count=self.u30();self.string_count_end=self.p;self.s=['']
  for _ in range(string_count-1):self.s.append(self.take(self.u30()).decode('utf8','replace'))
  self.string_end=self.p
  self.ns=[(0,0)]
  for _ in range(self.u30()-1):self.ns.append((self.u8(),self.u30()))
  for _ in range(self.u30()-1):
   for j in range(self.u30()):self.u30()
  self.multiname_count_pos=self.p;multiname_count=self.u30();self.multiname_count_end=self.p
  self.mn=[''];self.mndata=[None]
  for _ in range(multiname_count-1):
   k=self.u8();a=[]
   if k in (7,13):
    a=[self.u30(),self.u30()];name=self.s[a[1]];ns=self.s[self.ns[a[0]][1]];name=ns+':'+name if ns else name
   elif k in (15,16):a=[self.u30()];name=self.s[a[0]]
   elif k in (17,18):name='*'
   elif k in (9,14):a=[self.u30(),self.u30()];name=self.s[a[0]]
   elif k in (27,28):a=[self.u30()];name='*'
   elif k==29:
    a=[self.u30()];a += [self.u30() for j in range(self.u30())];name='generic'
   else:raise ValueError(('mn',k,self.p))
   self.mn.append(name);self.mndata.append((k,a))
  self.multiname_end=self.p
  self.methods=[]
  for _ in range(self.u30()):
   n=self.u30();ret=self.u30();params=[self.u30() for j in range(n)];name=self.s[self.u30()];flags=self.u8()
   if flags&8:
    for j in range(self.u30()):self.u30();self.u8()
   if flags&128:
    for j in range(n):self.u30()
   self.methods.append(dict(name=name,ret=self.mn[ret] if ret else '*',params=params))
  for _ in range(self.u30()):
   self.u30();n=self.u30()
   for j in range(n*2):self.u30()
  self.classes=[];self.labels={};nc=self.u30()
  for _ in range(nc):
   name=self.mn[self.u30()];sup=self.u30();flags=self.u8()
   if flags&8:self.u30()
   for j in range(self.u30()):self.u30()
   init=self.u30();self.labels[init]=name+'::<init>';traits=self.traits(name);self.classes.append(name)
  for name in self.classes:
   init=self.u30();self.labels[init]=name+'::<cinit>';self.traits(name+' static')
  for _ in range(self.u30()):self.u30();self.traits('script')
  self.bodies=[]
  for _ in range(self.u30()):
   start=self.p;idx=self.u30();stack=self.u30();locals_=self.u30();scope=self.u30();maxscope=self.u30();lp=self.p;n=self.u30();cp=self.p;code=self.take(n);exc=self.u30()
   for j in range(exc*5):self.u30()
   self.traits('body');self.bodies.append(dict(idx=idx,label=self.labels.get(idx,''),start=start,end=self.p,lp=lp,cp=cp,code=code,stack=stack,locals=locals_,scope=scope,maxscope=maxscope,exc=exc))
  assert self.p==len(b),(self.p,len(b))
 def traits(self,owner):
  out=[]
  for _ in range(self.u30()):
   name=self.mn[self.u30()];kind=self.u8();k=kind&15
   if k in (0,6):
    self.u30();self.u30();v=self.u30()
    if v:self.u8()
   elif k in (1,2,3):
    self.u30();m=self.u30();self.labels[m]=owner+'::'+name
   elif k in (4,5):self.u30();self.u30()
   else:raise ValueError(('trait',kind))
   if kind&64:
    for j in range(self.u30()):self.u30()
   out.append(name)
  return out
def tags(b):
 p=8+(5+4*(b[8]>>3)+7)//8+4
 while p<len(b):
  start=p;v=struct.unpack_from('<H',b,p)[0];p+=2;n=v&63;t=v>>6
  if n==63:n=struct.unpack_from('<I',b,p)[0];p+=4
  yield t,start,p,b[p:p+n];p+=n
if __name__=='__main__':
 b=Path('analysis/main.uncompressed').read_bytes()
 for t,start,p,data in tags(b):
  if t!=82:continue
  off=data.index(b'\0',4)+1;a=ABC(data[off:]);Path('analysis/main.abc').write_bytes(data[off:]);print('ABC',len(a.classes),'classes',len(a.methods),'methods')
  for x in a.bodies:
   if any(c in x['label'] for c in ('Combat:EnemyAI::','Combat:EnemyModel::')) or any(x['label'].endswith('::'+s) for s in ['chargePlayer','handleSkipTurns','enemyAttacked','hitEnemyNpc']):
    print(x['idx'],x['label'],a.methods[x['idx']]['ret'],'bytes',len(x['code']),x['code'].hex() if len(x['code'])<150 else '')
