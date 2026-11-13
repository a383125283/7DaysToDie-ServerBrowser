using System;
using System.IO;
namespace SevenDaysBrowser {
 internal static class RuntimePaths {
  internal static string DataFile(string name){
   string root=Environment.GetEnvironmentVariable("ANAN_SERVER_BROWSER_DATA");
   if(string.IsNullOrEmpty(root))root=AppDomain.CurrentDomain.BaseDirectory;
   Directory.CreateDirectory(root);return Path.Combine(root,name);
  }
 }
}
