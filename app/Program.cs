using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
namespace NinjaSageAI;
static class Program {
 [STAThread] static void Main(string[] args){
  if(args.Length==3&&args[0]=="--patch"){try{Patcher.Create(args[1],args[2]);Environment.Exit(0);}catch{Environment.Exit(1);}return;}
  if(args.Length==3&&args[0]=="--native-self-test"){NativeSelfTest(args[1],args[2]);return;}
  ApplicationConfiguration.Initialize();
  if(args.Length==2&&args[0]=="--render-preview"){
   using var form=new MainForm();form.Show();Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(args[1]);return;
  }
  Application.Run(new MainForm());
 }
 static void NativeSelfTest(string probe,string dir){
  Directory.CreateDirectory(dir);
  try{
   var original=Path.Combine(dir,"NinjaSage.swf");var patched=Path.Combine(dir,"patched.swf");var log=Path.Combine(dir,"hook.log");
   File.WriteAllText(original,"ORIGINAL");File.WriteAllText(patched,"PATCHED");
   var name=@"Local\NinjaSageAI-"+Guid.NewGuid().ToString("N");using var control=new EventWaitHandle(false,EventResetMode.ManualReset,name);
   uint error=Native.LaunchGame(probe,original,patched,log,name,out _,out var handle);using var owned=handle;
   if(error!=0)throw new Win32Exception((int)error);
   void WaitStage(string stage){var watch=Stopwatch.StartNew();while(!File.Exists(Path.Combine(dir,stage))){if(watch.ElapsedMilliseconds>5000||Native.WaitForSingleObject(handle,0)==0)throw new Exception("Probe did not reach "+stage);Thread.Sleep(10);}}
   try{
    WaitStage("probe.off");control.Set();WaitStage("probe.on");control.Reset();
    if(Native.WaitForSingleObject(handle,5000)!=0)throw new Exception("Probe did not finish after OFF");
    if(!Native.GetExitCodeProcess(handle,out var exit)||exit!=0)throw new Exception("Probe failed");
    var logText=File.ReadAllText(log);
    if(File.ReadAllText(original)!="ORIGINAL"||!logText.Contains("REDIRECTED")||!logText.Contains("CONTROL_ON")||!logText.Contains("CONTROL_OFF"))throw new Exception("Missing confirmations");
    Environment.Exit(0);
   }finally{if(Native.WaitForSingleObject(handle,0)!=0)Native.TerminateProcess(handle,1);}
  }catch(Exception e){File.WriteAllText(Path.Combine(dir,"failure.txt"),e.ToString());Environment.Exit(1);}
 }
}
public class MainForm:Form {
 readonly TextBox path=new(){Dock=DockStyle.Fill,PlaceholderText="Select Ninja Sage.exe from the extracted game folder"};
 readonly TextBox status=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(20,25,34),ForeColor=Color.FromArgb(205,222,238),BorderStyle=BorderStyle.None};
 readonly Button start=new(){Text="Launch game (starts OFF)",AutoSize=true};
 readonly CheckBox toggle=new(){Text="Enemy skip: OFF",Appearance=Appearance.Button,AutoSize=true,Enabled=false,Padding=new Padding(10,3,10,3)};
 readonly Label state=new(){Text="Normal AI · Start the game, then switch at any time.",Dock=DockStyle.Fill,ForeColor=Color.LightSteelBlue};
 readonly System.Windows.Forms.Timer timer=new(){Interval=300};
 SafeProcessHandle? game;EventWaitHandle? control;string? session,logFile;string observed="";DateTime launched;bool confirmed,timeoutShown,suppressToggle;
 readonly string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NinjaSageAI");
 public MainForm(){
  Text="NinjaSage AI · Live toggle";MinimumSize=new Size(790,550);Size=new Size(920,630);StartPosition=FormStartPosition.CenterScreen;
  Font=new Font("Segoe UI",11);BackColor=Color.FromArgb(27,33,44);ForeColor=Color.White;
  var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=7};Controls.Add(layout);
  foreach(var h in new[]{52,65,42,54,38})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
  layout.Controls.Add(new Label(){Text="Normal first. Switch when needed.",Font=new Font("Segoe UI",22,FontStyle.Bold),AutoSize=true},0,0);
  layout.Controls.Add(new Label(){Text="Launch here with normal enemy behavior. Turn skip ON or OFF while the game stays open.\nChanges apply at the next enemy decision. Live PvP is not patched.",Dock=DockStyle.Fill},0,1);
  var select=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2};select.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));select.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));select.Controls.Add(path,0,0);
  var browse=new Button(){Text="Browse…",Dock=DockStyle.Fill};browse.Click+=(_,_)=>{using var d=new OpenFileDialog(){Filter="Ninja Sage executable|Ninja Sage.exe|Executable|*.exe"};if(d.ShowDialog()==DialogResult.OK)path.Text=d.FileName;};select.Controls.Add(browse,1,0);layout.Controls.Add(select,0,2);
  var buttons=new FlowLayoutPanel(){Dock=DockStyle.Fill};start.Click+=(_,_)=>Launch();toggle.CheckedChanged+=(_,_)=>ChangeMode();buttons.Controls.Add(start);buttons.Controls.Add(toggle);layout.Controls.Add(buttons,0,3);layout.Controls.Add(state,0,4);
  layout.Controls.Add(status,0,5);var logs=new Button(){Text="Open session logs",AutoSize=true};logs.Click+=(_,_)=>{Directory.CreateDirectory(root);Process.Start(new ProcessStartInfo(session??root){UseShellExecute=true});};layout.Controls.Add(logs,0,6);
  Say("Ready. Launch through this app to enable the live switch.");Say("Original files remain unchanged. Closing this launcher returns the switch to OFF.");
  timer.Tick+=(_,_)=>Poll();timer.Start();
 }
 void Say(string message)=>status.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
 void SetToggleOff(){suppressToggle=true;toggle.Checked=false;toggle.Text="Enemy skip: OFF";suppressToggle=false;}
 void ReleaseSession(){control?.Reset();control?.Dispose();control=null;game?.Dispose();game=null;toggle.Enabled=false;SetToggleOff();start.Enabled=true;confirmed=false;logFile=null;}
 void Launch(){
  try{
   if(game!=null&&Native.WaitForSingleObject(game,0)!=0)throw new InvalidOperationException("Close this game session before launching another.");
   ReleaseSession();var exe=Path.GetFullPath(path.Text.Trim());if(!File.Exists(exe))throw new FileNotFoundException("Select the game executable.");
   var dir=Path.GetDirectoryName(exe)!;var swf=Path.Combine(dir,"NinjaSage.swf");if(!File.Exists(swf))throw new FileNotFoundException("NinjaSage.swf must be next to the executable.");
   using(var f=File.OpenRead(exe)){using var r=new BinaryReader(f);if(r.ReadUInt16()!=0x5a4d)throw new InvalidDataException("Invalid executable.");f.Position=60;int pe=r.ReadInt32();f.Position=pe;if(r.ReadUInt32()!=0x4550||r.ReadUInt16()!=0x14c)throw new InvalidOperationException("This launcher supports the supplied 32-bit AIR game only.");}
   start.Enabled=false;session=Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(session);
   var patched=Path.Combine(session,"NinjaSage.test.swf");Patcher.Create(swf,patched);Say("Exact build verified. Runtime controls prepared with skip OFF.");
   var eventName=@"Local\NinjaSageAI-"+Guid.NewGuid().ToString("N");control=new EventWaitHandle(false,EventResetMode.ManualReset,eventName);
   logFile=Path.Combine(session,"hook.log");observed="";confirmed=false;timeoutShown=false;
   uint err=Native.LaunchGame(exe,swf,patched,logFile,eventName,out var pid,out var handle);game=handle;
   if(err!=0)throw new Win32Exception((int)err,"Launch failed: "+new Win32Exception((int)err).Message);
   launched=DateTime.UtcNow;state.Text="Starting with normal AI · Waiting for runtime control confirmation…";Say($"Process {pid} started. Normal AI requested.");
  }catch(Exception e){ReleaseSession();Say("ERROR: "+e.Message);MessageBox.Show(this,e.Message,"Launch failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
 void ChangeMode(){
  if(suppressToggle)return;
  try{
   if(!confirmed||game==null||Native.WaitForSingleObject(game,0)!=258||control==null){SetToggleOff();return;}
   if(toggle.Checked)control.Set();else control.Reset();
   toggle.Text=toggle.Checked?"Enemy skip: ON":"Enemy skip: OFF";
   state.Text=toggle.Checked?"Skip requested · Takes effect at the next enemy decision.":"Normal AI requested · Takes effect at the next enemy decision.";
   Say(toggle.Checked?"Switch ON. Enemy skip requested.":"Switch OFF. Normal enemy behavior requested.");
  }catch(Exception e){control?.Reset();SetToggleOff();Say("Switch failed: "+e.Message);}
 }
 void Poll(){
  try{
   if(logFile!=null){
    if(File.Exists(logFile)){
     using var f=new FileStream(logFile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var r=new StreamReader(f);var text=r.ReadToEnd();
     if(text.Length>observed.Length){
      var added=text[observed.Length..];
      foreach(var line in added.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
       if(line=="HOOK_READY")Say("Runtime control DLL loaded.");
       if(line=="REDIRECTED"&&!confirmed){confirmed=true;toggle.Enabled=true;state.Text="Normal AI · The live switch is ready.";Say("Patched SWF opened. Skip is OFF; live switch is ready.");}
       if(line=="CONTROL_ON"){Say("Game checked the switch: ON.");if(toggle.Checked)state.Text="Skip ON · Game has read the enabled switch.";}
       if(line=="CONTROL_OFF"){Say("Game checked the switch: OFF.");if(!toggle.Checked)state.Text="Normal AI · Game has read the disabled switch.";}
       if(line.Contains("FAILED")){Say("Runtime control error: "+line);control?.Reset();SetToggleOff();toggle.Enabled=false;}
      }
      observed=text;
     }
    }
    if(!confirmed&&!timeoutShown&&(DateTime.UtcNow-launched).TotalSeconds>30){timeoutShown=true;Say("Runtime control has not been confirmed. The switch remains disabled.");state.Text="Waiting for confirmation · Do not assume runtime controls are active.";}
   }
   if(game!=null&&Native.WaitForSingleObject(game,0)==0){ReleaseSession();Say("Game exited. The next session will start with skip OFF.");state.Text="Normal AI · Ready for a new session.";}
  }catch(IOException){}catch(Exception e){Say("Status error: "+e.Message);ReleaseSession();}
 }
 protected override void Dispose(bool disposing){if(disposing){timer.Dispose();ReleaseSession();}base.Dispose(disposing);}
}
