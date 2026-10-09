"""Verify AVM2 guard behavior from the exact bytes shipped in the app profile."""
import base64,json,unittest
from pathlib import Path
p=json.loads((Path(__file__).parents[1]/'app/profile.json').read_text())
def u30(b,pos):
 value=0
 for shift in range(0,35,7):
  c=b[pos];pos+=1;value|=(c&127)<<shift
  if c<128:return value,pos
 raise ValueError('u30')
def unpack(b):n,pos=u30(b,0);assert n==len(b)-pos;return b[pos:]
class GuardTest(unittest.TestCase):
 def test_exactly_three_methods(self):self.assertEqual(len(p['methods']),3)
 def test_guard_branches_and_preservation(self):
  methods=[x for x in p['edits'] if len(base64.b64decode(x['before']))>10]
  self.assertEqual(len(methods),3)
  for edit in methods:
   old=unpack(base64.b64decode(edit['before']));new=unpack(base64.b64decode(edit['after']));delta=len(new)-len(old)
   self.assertEqual(new[delta+2:],old[2:]);self.assertEqual(new[:2],b'\xd0\x30')
   # Evaluate the actual inserted AVM2 instructions, including the encoded branch.
   for team in ['enemy','player','neutral','',None]:
    stack=[];pc=2;called=0;returned=False
    while pc<delta+2:
     op=new[pc];pc+=1
     if op==0xd0:stack.append('this')
     elif op==0x66:
      idx,pc=u30(new,pc);self.assertEqual(stack.pop(),'this');stack.append({27503:'actor',27417:'agility'}[idx])
     elif op==0x46:
      idx,pc=u30(new,pc);n,pc=u30(new,pc);self.assertEqual((idx,n,stack.pop()),(27508,0,'actor'));stack.append(team)
     elif op==0x2c:
      idx,pc=u30(new,pc);self.assertEqual(idx,10560);stack.append('enemy')
     elif op==0xab:stack.append(stack.pop()==stack.pop())
     elif op==0x12:
      offset=int.from_bytes(new[pc:pc+3],'little',signed=True);pc+=3
      if not stack.pop():pc+=offset
     elif op==0x4f:
      idx,pc=u30(new,pc);n,pc=u30(new,pc);self.assertEqual((idx,n,stack.pop()),(27529,0,'agility'));called+=1
     elif op==0x47:returned=True;break
     else:self.fail(f'Unexpected guard opcode {op:x}')
    self.assertEqual(called,1 if team=='enemy' else 0);self.assertEqual(returned,team=='enemy');self.assertEqual(stack,[])
    if team!='enemy':self.assertEqual(pc,delta+2)
if __name__=='__main__':unittest.main()
