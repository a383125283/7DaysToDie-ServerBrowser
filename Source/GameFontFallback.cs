using System;using System.IO;using System.Drawing;using System.Drawing.Text;using System.Collections.Generic;using System.Globalization;using System.Runtime.InteropServices;
namespace SevenDaysBrowser {
 internal static class GameFontFallback {
  private sealed class Entry{internal string Name;internal FontFamily Family;internal HashSet<int> Scalars;}
  private static readonly List<Entry> gameFonts=new List<Entry>();
  private static readonly Dictionary<string,string> choices=new Dictionary<string,string>();
  private static readonly PrivateFontCollection collection=new PrivateFontCollection();
  private static bool initialized;
  private static readonly HashSet<int> gameEmoji=new HashSet<int>();
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]private static extern int AddFontResourceEx(string file,uint flags,IntPtr reserved);
  internal static void Initialize(){
   if(initialized)return;initialized=true;string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Fonts"),manifest=Path.Combine(root,"fonts.txt");if(!File.Exists(manifest))return;
   string emoji=Path.Combine(root,"GameEmoji.cmap");if(File.Exists(emoji))using(var reader=new BinaryReader(File.OpenRead(emoji)))while(reader.BaseStream.Position<reader.BaseStream.Length)gameEmoji.Add(reader.ReadInt32());
   foreach(string line in File.ReadAllLines(manifest)){var parts=line.Split('\t');if(parts.Length!=3)continue;string path=Path.Combine(root,parts[1]);if(AddFontResourceEx(path,0x10,IntPtr.Zero)==0)continue;
    collection.AddFontFile(path);FontFamily family=null;foreach(var candidate in collection.Families)if(candidate.Name==parts[0]){family=candidate;break;}if(family==null)continue;
    var scalars=new HashSet<int>();using(var reader=new BinaryReader(File.OpenRead(Path.Combine(root,parts[2]))))while(reader.BaseStream.Position<reader.BaseStream.Length)scalars.Add(reader.ReadInt32());gameFonts.Add(new Entry{Name=parts[0],Family=family,Scalars=scalars});
   }
  }
  internal static Font CreateFont(string name,float size,FontStyle style){Initialize();foreach(var entry in gameFonts)if(entry.Name==name)return new Font(entry.Family,size,style);return new Font(name,size,style);}
  internal static bool Covers(string name,string text){
   Initialize();foreach(var entry in gameFonts)if(entry.Name==name){for(int i=0;i<text.Length;i++){int cp=char.ConvertToUtf32(text,i);if(char.IsHighSurrogate(text[i]))i++;if(!Ignorable(cp)&&!entry.Scalars.Contains(cp))return false;}return true;}
   return false;
  }
  private static bool Ignorable(int cp){return cp<32||cp==0x200D||cp>=0xFE00&&cp<=0xFE0F||cp>=0xE0100&&cp<=0xE01EF;}
  internal static string Resolve(string text,string primary){
   Initialize();string key=primary+"\0"+text,chosen;if(choices.TryGetValue(key,out chosen))return chosen;
   if(gameEmoji.Contains(char.ConvertToUtf32(text,0))){choices[key]="Segoe UI Emoji";return "Segoe UI Emoji";}
   // RichEdit must use an installed face when one covers the text. Private
   // subset faces can print correctly while displaying missing-glyph boxes.
   if(SystemGlyphCoverage.Covers(primary,text)){choices[key]=primary;return primary;}
   foreach(string face in new[]{"Segoe UI Symbol","Segoe UI Emoji","Segoe UI","Arial"})if(SystemGlyphCoverage.Covers(face,text)){choices[key]=face;return face;}
   chosen=primary;
   foreach(var entry in gameFonts)if(Covers(entry.Name,text)){chosen=entry.Name;break;}
   choices[key]=chosen;return chosen;
  }
 }
}
