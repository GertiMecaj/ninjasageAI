"""Generate an exact-build patch from a user-supplied SWF; never bundles game assets."""
from abc_reader import ABC,tags
from pathlib import Path
import sys,zlib,hashlib,json,struct,base64

def u30(v):
 out=bytearray()
 while v>127:out.append((v&127)|128);v>>=7
 out.append(v);return bytes(out)
def generate(path):
 raw=Path(path).read_bytes();assert raw[:3]==b'CWS'
 b=raw[:8]+zlib.decompress(raw[8:]);assert len(b)==struct.unpack_from('<I',b,4)[0]
 edits=[];targets=['setActionsAvailable','handleNonControllableAttacker','handleSkipTurns'];found=[]
 for t,start,p,data in tags(b):
  if t!=82:continue
  off=data.index(b'\0',4)+1;a=ABC(data[off:]);delta=0
  # Multiname entries with the lexical namespace set of Combat.Battle.
  def mn(name):
   entries=[i for i,x in enumerate(a.mndata) if x and x[0]==9 and x[1][1]==1551 and a.mn[i]==name]
   assert len(entries)==1,(name,entries)
   return u30(entries[0])
  # Preserve existing pushscope; non-enemy branches fall through unchanged.
  skip=b'\xd0\x66'+mn('agility_bar_manager')+b'\x4f'+mn('startRun')+b'\x00\x47'
  guard=b'\xd0\x66'+mn('attacker_model')+b'\x46'+mn('getPlayerTeam')+b'\x00\x2c'+u30(a.s.index('enemy'))+b'\xab\x12'+len(skip).to_bytes(3,'little')+skip
  for x in a.bodies:
   if x['label'] not in ['Combat:Battle::'+n for n in targets]:continue
   assert not x['exc'] and x['code'][:2]==b'\xd0\x30' and x['stack']>=2
   new=x['code'][:2]+guard+x['code'][2:]
   old=b[p+off+x['lp']:p+off+x['cp']+len(x['code'])]
   replacement=u30(len(new))+new
   edits.append((p+off+x['lp'],old,replacement));delta+=len(replacement)-len(old);found.append(x['label'])
  if delta:
   assert p-start==6
   edits.append((start+2,b[start+2:start+6],struct.pack('<I',len(data)+delta)))
 assert len(found)==3,found
 delta=sum(len(n)-len(o) for _,o,n in edits)
 edits.append((4,b[4:8],struct.pack('<I',len(b)+delta)))
 patched=b
 for pos,old,new in sorted(edits,reverse=True):assert patched[pos:pos+len(old)]==old;patched=patched[:pos]+new+patched[pos+len(old):]
 # Full ABC round-trip parse checks table and method-body bounds.
 for t,_,_,data in tags(patched):
  if t==82:ABC(data[data.index(b'\0',4)+1:])
 profile={'originalSha256':hashlib.sha256(raw).hexdigest(),'patchedUncompressedSha256':hashlib.sha256(patched).hexdigest(),'methods':found,'edits':[{'offset':p,'before':base64.b64encode(o).decode(),'after':base64.b64encode(n).decode()} for p,o,n in sorted(edits)]}
 return profile,patched
if __name__=='__main__':
 profile,patched=generate(sys.argv[1]);Path(sys.argv[2]).write_text(json.dumps(profile,indent=2));print(profile['originalSha256'],profile['methods'])
