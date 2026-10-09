using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace NinjaSageAI;
static class Program {
 [STAThread] static void Main(string[] args){
  if(args.Length==3&&args[0]=="--patch"){try{Patcher.Create(args[1],args[2]);Environment.Exit(0);}catch{Environment.Exit(1);}return;}
  if(args.Length==3&&args[0]=="--native-self-test"){
   try{
    Directory.CreateDirectory(args[2]);var original=Path.Combine(args[2],"NinjaSage.swf");var patched=Path.Combine(args[2],"patched.swf");var log=Path.Combine(args[2],"hook.log");
    File.WriteAllText(original,"ORIGINAL");File.WriteAllText(patched,"PATCHED");
    uint error=MainForm.LaunchGame(args[1],original,patched,log,out var pid);if(error!=0)throw new Win32Exception((int)error);
    using var process=Process.GetProcessById((int)pid);if(!process.WaitForExit(15000)){process.Kill();throw new Exception("Test timed out");}
    if(process.ExitCode!=0||File.ReadAllText(original)!="ORIGINAL"||!File.ReadAllText(log).Contains("REDIRECTED"))throw new Exception("Native bridge failed");
    Environment.Exit(0);
   }catch(Exception e){File.WriteAllText(Path.Combine(args[2],"failure.txt"),e.ToString());Environment.Exit(1);}return;
  }
  ApplicationConfiguration.Initialize();
  if(args.Length==2&&args[0]=="--render-preview"){
   using var form=new MainForm();form.Show();Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(args[1]);return;
  }
  Application.Run(new MainForm());
 }
}
public class MainForm:Form {
 [DllImport("NinjaSageHook.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode,ExactSpelling=true)]
 internal static extern uint LaunchGame(string exe,string original,string patched,string log,out uint pid);
 readonly TextBox path=new(){Dock=DockStyle.Fill,PlaceholderText="Select Ninja Sage.exe from the extracted game folder"};
 readonly TextBox status=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(20,25,34),ForeColor=Color.FromArgb(205,222,238),BorderStyle=BorderStyle.None};
 readonly Button start=new(){Text="Launch with enemy skip",AutoSize=true};
 readonly System.Windows.Forms.Timer timer=new(){Interval=1000};
 Process? game;string? session,logFile;string observed="";DateTime launched;
 readonly string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NinjaSageAI");
 public MainForm(){
  Text="NinjaSage AI · Test launcher";MinimumSize=new Size(760,490);Size=new Size(870,560);StartPosition=FormStartPosition.CenterScreen;
  Font=new Font("Segoe UI",11);BackColor=Color.FromArgb(27,33,44);ForeColor=Color.White;
  var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=6};Controls.Add(layout);
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,70));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
  layout.Controls.Add(new Label(){Text="Enemy turns: SKIP",Font=new Font("Segoe UI",22,FontStyle.Bold),AutoSize=true},0,0);
  layout.Controls.Add(new Label(){Text="Starts the game with a test DLL. Enemy-side turns in the main battle system skip.\nOriginal game files stay intact. Close the game to switch modes. Live PvP is not patched.",Dock=DockStyle.Fill},0,1);
  var select=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2};select.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));select.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));select.Controls.Add(path,0,0);
  var browse=new Button(){Text="Browse…",Dock=DockStyle.Fill};browse.Click+=(_,_)=>{using var d=new OpenFileDialog(){Filter="Ninja Sage executable|Ninja Sage.exe|Executable|*.exe"};if(d.ShowDialog()==DialogResult.OK)path.Text=d.FileName;};select.Controls.Add(browse,1,0);layout.Controls.Add(select,0,2);
  var buttons=new FlowLayoutPanel(){Dock=DockStyle.Fill};start.Click+=(_,_)=>Launch(true);var normal=new Button(){Text="Launch normal",AutoSize=true};normal.Click+=(_,_)=>Launch(false);buttons.Controls.Add(start);buttons.Controls.Add(normal);layout.Controls.Add(buttons,0,3);
  layout.Controls.Add(status,0,4);var logs=new Button(){Text="Open test logs",AutoSize=true};logs.Click+=(_,_)=>{Directory.CreateDirectory(root);Process.Start(new ProcessStartInfo(session??root){UseShellExecute=true});};layout.Controls.Add(logs,0,5);
  Say("Ready. Select the executable from the supplied game build.");Say("Runtime verification: Windows hook tests are separate from in-game battle validation.");
  timer.Tick+=(_,_)=>Poll();timer.Start();
 }
 void Say(string message)=>status.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
 void Launch(bool patch){
  try{
   if(game!=null&&!game.HasExited)throw new InvalidOperationException("Close the game launched here before starting another session.");
   var exe=Path.GetFullPath(path.Text.Trim());if(!File.Exists(exe))throw new FileNotFoundException("Select the game executable.");
   var dir=Path.GetDirectoryName(exe)!;var swf=Path.Combine(dir,"NinjaSage.swf");if(!File.Exists(swf))throw new FileNotFoundException("NinjaSage.swf must be next to the executable.");
   // Refuse incompatible native architectures before loading the injected library.
   using(var f=File.OpenRead(exe)){using var r=new BinaryReader(f);if(r.ReadUInt16()!=0x5a4d)throw new InvalidDataException("Invalid executable.");f.Position=60;int pe=r.ReadInt32();f.Position=pe;if(r.ReadUInt32()!=0x4550||r.ReadUInt16()!=0x14c)throw new InvalidOperationException("This launcher supports the supplied 32-bit AIR game only.");}
   logFile=null;observed="";
   if(!patch){game=Process.Start(new ProcessStartInfo(exe){WorkingDirectory=dir,UseShellExecute=true});Say("Normal launch requested; no test DLL injected.");return;}
   start.Enabled=false;
   session=Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(session);
   var patched=Path.Combine(session,"NinjaSage.test.swf");Patcher.Create(swf,patched);Say("Exact build verified. Test copy created; original untouched.");
   logFile=Path.Combine(session,"hook.log");uint err=LaunchGame(exe,swf,patched,logFile,out var pid);
   if(err!=0)throw new Win32Exception((int)err,"DLL launch failed: "+new Win32Exception((int)err).Message);
   game=Process.GetProcessById((int)pid);launched=DateTime.UtcNow;Say($"Process {pid} started. Waiting for DLL and SWF confirmation…");
  }catch(Exception e){Say("ERROR: "+e.Message);MessageBox.Show(this,e.Message,"Launch failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}finally{start.Enabled=true;}
 }
 void Poll(){
  try{
   if(logFile!=null&&File.Exists(logFile)){
    using var f=new FileStream(logFile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var r=new StreamReader(f);var text=r.ReadToEnd();
    if(text!=observed){if(text.Contains("HOOK_READY")&&!observed.Contains("HOOK_READY"))Say("DLL hook installed.");if(text.Contains("REDIRECTED")&&!observed.Contains("REDIRECTED"))Say("Patched SWF opened by the game. Verify enemy turns in a test battle.");if(text.Contains("FAILED")&&!observed.Contains("FAILED"))Say("Hook/redirect failed. See hook.log.");observed=text;}
    if(!observed.Contains("REDIRECTED")&&(DateTime.UtcNow-launched).TotalSeconds>30&&!observed.Contains("TIMEOUT")){Say("No patched-SWF read confirmed yet. Do not assume the patch is active.");observed+="TIMEOUT";launched=DateTime.UtcNow.AddYears(1);}
   }
   if(game!=null&&game.HasExited){Say("Game process exited. A normal launch uses the original AI.");game.Dispose();game=null;logFile=null;}
  }catch(IOException){}catch(Exception e){Say("Status: "+e.Message);game=null;logFile=null;}
 }
 protected override void Dispose(bool disposing){if(disposing){timer.Dispose();game?.Dispose();}base.Dispose(disposing);}
}
