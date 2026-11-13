using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// A single distributable file; runtime DLLs are unpacked into the current user's cache.
internal static class SingleFileLauncher {
 [STAThread] private static int Main(string[] args){
  try{
   string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Anan","SevenDaysServerBrowser");
   Directory.CreateDirectory(data);
   using(var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("BrowserPayload.zip")){
    string key;using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(payload)).Replace("-","");
    payload.Position=0;
    string runtime=Path.Combine(data,"Runtime",key);
    using(var mutex=new Mutex(false,"Local\\AnanServerBrowser-"+key)){
     bool locked=false;
     try{
      try{locked=mutex.WaitOne(30000);}catch(AbandonedMutexException){locked=true;}
      if(!locked)throw new IOException("另一个窗口正在准备运行文件，请稍后重试。");
      Directory.CreateDirectory(runtime);
      using(var zip=new ZipArchive(payload,ZipArchiveMode.Read))foreach(var entry in zip.Entries){
       string file=Path.GetFullPath(Path.Combine(runtime,entry.FullName));
       if(!file.StartsWith(runtime+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("运行包路径无效。");
       if(entry.Name.Length==0)continue;
       bool same=false;
       if(File.Exists(file)&&new FileInfo(file).Length==entry.Length){
        using(var sha=SHA256.Create())using(var expected=entry.Open())using(var actual=File.OpenRead(file))same=sha.ComputeHash(expected).SequenceEqual(sha.ComputeHash(actual));
       }
       if(same)continue;
       Directory.CreateDirectory(Path.GetDirectoryName(file));
       using(var input=entry.Open())using(var output=File.Create(file))input.CopyTo(output);
      }
     }finally{if(locked)mutex.ReleaseMutex();}
    }
    // Preserve local settings across program updates; do not package the user's favorites.
    foreach(string name in new[]{"favorites.txt","game-path.txt"}){
     string saved=Path.Combine(data,name),nearby=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name);
     if(!File.Exists(saved)&&File.Exists(nearby))File.Copy(nearby,saved);
    }
    string gamePath=Path.Combine(data,"game-path.txt"),defaultPath=Path.Combine(runtime,"game-path.txt");
    if(!File.Exists(gamePath)&&File.Exists(defaultPath)&&Directory.Exists(File.ReadAllText(defaultPath).Trim()))File.Copy(defaultPath,gamePath);
    var start=new ProcessStartInfo(Path.Combine(runtime,"BrowserCore.exe"),string.Join(" ",args.Select(Quote))){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=runtime};
    start.EnvironmentVariables["ANAN_SERVER_BROWSER_DATA"]=data;
    using(var process=Process.Start(start)){process.WaitForExit();return process.ExitCode;}
   }
  }catch(Exception ex){MessageBox.Show("无法启动服务器浏览器：\n"+ex.Message,"七日杀服务器浏览器",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
 }
 private static string Quote(string value){
  var text=new StringBuilder("\"");int slashes=0;
  foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){text.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}text.Append('\\',slashes).Append(c);slashes=0;}
  return text.Append('\\',slashes*2).Append('"').ToString();
 }
}
