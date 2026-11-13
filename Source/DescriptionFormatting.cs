using System;using System.Drawing;using System.Collections.Generic;using System.Globalization;using System.Text;using System.Text.RegularExpressions;
namespace SevenDaysBrowser {
 public sealed partial class BrowserForm {
  // NGUIText.ParseSymbol: case-insensitive style tags, colour stack, alpha, sub/sup and URL wrappers.
  private void RenderDescription(string text){
   text=text.Replace("\uFE0F","").Replace("\uFE0E","");
   description.Clear();var colors=new Stack<Color>();colors.Push(ink);FontStyle style=FontStyle.Regular;int sub=0,alpha=255;bool ignoreColor=false;int start=0;
   var fonts=new Dictionary<string,Font>();
   Action<string> append=chunk=>{
    Color color=ignoreColor?ink:colors.Peek();double opacity=color.A/255.0*alpha/255.0;
    color=Color.FromArgb((int)(color.R*opacity+description.BackColor.R*(1-opacity)),(int)(color.G*opacity+description.BackColor.G*(1-opacity)),(int)(color.B*opacity+description.BackColor.B*(1-opacity)));
    color=ReadableDescriptionColor(color);var elements=StringInfo.GetTextElementEnumerator(chunk);var buffer=new StringBuilder();string family=null;
    Action flush=()=>{if(buffer.Length==0)return;string key=family+"/"+style+"/"+sub;Font font;if(!fonts.TryGetValue(key,out font)){font=GameFontFallback.CreateFont(family,description.Font.Size*(sub==0?1:0.75f),style);fonts.Add(key,font);}description.SelectionFont=font;description.SelectionColor=color;description.SelectionCharOffset=sub==0?0:(sub==1?-1:1)*(int)(description.Font.Size/3);description.AppendText(buffer.ToString());buffer.Length=0;};
    while(elements.MoveNext()){string element=elements.GetTextElement();string face=GameFontFallback.Resolve(element,description.Font.FontFamily.Name);if(family!=null&&face!=family)flush();family=face;buffer.Append(element);}flush();
   };
   try{
    foreach(Match match in Regex.Matches(text,@"\[([0-9a-f]{8}|[0-9a-f]{6}|[0-9a-f]{2}|-|/?(?:b|i|u|s|c|t|sub|sup)|url=[^\]]*|/url)\]",RegexOptions.IgnoreCase)){
     append(text.Substring(start,match.Index-start));string tag=match.Groups[1].Value.ToLowerInvariant();
     if(tag=="-"){if(colors.Count>1)colors.Pop();}
     else if(Regex.IsMatch(tag,@"\A[0-9a-f]{6}(?:[0-9a-f]{2})?\z")){Color color=ColorTranslator.FromHtml("#"+tag.Substring(0,6));colors.Push(Color.FromArgb(tag.Length==8?Convert.ToInt32(tag.Substring(6,2),16):colors.Peek().A,color));}
     else if(Regex.IsMatch(tag,@"\A[0-9a-f]{2}\z"))alpha=Convert.ToInt32(tag,16);
     else if(tag=="sub")sub=1;else if(tag=="sup")sub=2;else if(tag=="/sub"||tag=="/sup")sub=0;
     else if(tag=="c")ignoreColor=true;else if(tag=="/c")ignoreColor=false;
     else {bool close=tag.StartsWith("/");string name=close?tag.Substring(1):tag;FontStyle flag=name=="b"?FontStyle.Bold:name=="i"?FontStyle.Italic:name=="u"?FontStyle.Underline:name=="s"?FontStyle.Strikeout:FontStyle.Regular;if(close)style&=~flag;else style|=flag;}
     start=match.Index+match.Length;
    }
    append(text.Substring(start));description.SelectAll();description.SelectionAlignment=System.Windows.Forms.HorizontalAlignment.Center;description.Select(0,0);
   }finally{foreach(var font in fonts.Values)font.Dispose();}
  }
 }
}
