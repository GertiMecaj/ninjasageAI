using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace NinjaSageAI;
public record Edit(int offset,string before,string after);
public record Profile(string originalSha256,string patchedUncompressedSha256,string[] methods,Edit[] edits);
public static class Patcher {
 public static Profile GetProfile(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("NinjaSageAI.profile.json")!;return JsonSerializer.Deserialize<Profile>(s)!;}
 public static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
 public static void Create(string original,string output){
  var p=GetProfile();var raw=File.ReadAllBytes(original);
  if(Hash(raw)!=p.originalSha256)throw new InvalidOperationException("Unsupported game build. This tool only supports the supplied NinjaSage.rar build. No files were changed.");
  if(raw.Length<10||raw[0]!='C'||raw[1]!='W'||raw[2]!='S')throw new InvalidDataException("Expected compressed SWF.");
  using var un=new MemoryStream();un.Write(raw,0,8);
  using(var z=new ZLibStream(new MemoryStream(raw,8,raw.Length-8),CompressionMode.Decompress))z.CopyTo(un);
  var data=un.ToArray();if(BitConverter.ToUInt32(data,4)!=data.Length)throw new InvalidDataException("SWF length mismatch.");
  using var result=new MemoryStream();int cursor=0;
  foreach(var edit in p.edits.OrderBy(x=>x.offset)){
   var before=Convert.FromBase64String(edit.before);var after=Convert.FromBase64String(edit.after);
   if(edit.offset<cursor||edit.offset+before.Length>data.Length||!data.AsSpan(edit.offset,before.Length).SequenceEqual(before))throw new InvalidDataException("Patch precondition failed.");
   result.Write(data,cursor,edit.offset-cursor);result.Write(after);cursor=edit.offset+before.Length;
  }
  result.Write(data,cursor,data.Length-cursor);var patched=result.ToArray();
  if(Hash(patched)!=p.patchedUncompressedSha256)throw new InvalidDataException("Patched output verification failed.");
  using(var f=new FileStream(output,FileMode.CreateNew,FileAccess.Write)){
   f.Write(patched,0,8);using var z=new ZLibStream(f,CompressionLevel.Optimal);z.Write(patched,8,patched.Length-8);
  }
 }
}
