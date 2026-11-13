using System;using System.IO;using System.Drawing;using System.Windows.Forms;using System.Reflection;using SevenDaysBrowser;
class AppearanceTests {
 [STAThread] static void Main(){
 var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"开发验证","appearance-test-state");Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("ANAN_SERVER_BROWSER_DATA",root);
 using(var f=new BrowserForm(true)){f.Show();Application.DoEvents();var flags=BindingFlags.Instance|BindingFlags.NonPublic;var t=f.GetType();var lang=(ComboBox)t.GetField("languageChoice",flags).GetValue(f);var theme=(ComboBox)t.GetField("themeChoice",flags).GetValue(f);
 lang.SelectedIndex=1;theme.SelectedIndex=1;Application.DoEvents();if(!f.Text.Contains("Server Browser")||f.BackColor!=Color.White)throw new Exception("English light failed");
 var data=(System.Collections.Generic.Dictionary<string,ServerRow>)t.GetField("servers",flags).GetValue(f);
 data["192.0.2.1:26900"]=new ServerRow{Address="192.0.2.1:26900",Name="Description check",Map="World",Version="V3.4.0",Eac=true,Description=@"[D3D3D3]QQ group\n[00FF00]Welcome\n[FF00FF]Online\n[FFFF00]Always open"};
 t.GetMethod("RefreshRows",flags).Invoke(f,null);var grid=(DataGridView)t.GetField("grid",flags).GetValue(f);grid.CurrentCell=grid.Rows[0].Cells[4];grid.Rows[0].Selected=true;t.GetMethod("UpdateDetails",flags).Invoke(f,null);
 var desc=(RichTextBox)t.GetField("description",flags).GetValue(f);if(desc.Text.Contains(@"\n")||!desc.Text.Contains("\n"))throw new Exception("Literal newline conversion failed");
 foreach(string word in new[]{"QQ group","Welcome","Online","Always open"}){desc.Select(desc.Text.IndexOf(word),1);var c=desc.SelectionColor;Func<byte,double> linear=v=>v/255.0<=0.04045?v/255.0/12.92:Math.Pow((v/255.0+0.055)/1.055,2.4);double luminance=0.2126*linear(c.R)+0.7152*linear(c.G)+0.0722*linear(c.B);if(luminance>0.181)throw new Exception("Low contrast description color: "+word);}
 t.GetMethod("RenderDescription",flags).Invoke(f,new object[]{"[B][I][U][S]Styled[/S][/U][/I][/B] plain [FF0000]red[0000FF]blue[-]red[-] [SUB]low[/SUB][SUP]high[/SUP] [url=https://example.com]link[/url] 🌙"});
 if(desc.Text.Contains("[")||!desc.Text.Contains("Styled plain redbluered"))throw new Exception("Formatting wrappers leaked into description");
 desc.Select(desc.Text.IndexOf("Styled"),1);if(!desc.SelectionFont.Bold||!desc.SelectionFont.Italic||!desc.SelectionFont.Underline||!desc.SelectionFont.Strikeout)throw new Exception("Style combination failed");
 desc.Select(desc.Text.IndexOf("plain"),1);if(desc.SelectionFont.Style!=FontStyle.Regular)throw new Exception("Closing style tags failed");
 desc.Select(desc.Text.IndexOf("blue"),1);if(desc.SelectionColor.B<=desc.SelectionColor.R)throw new Exception("Nested blue color failed");
 desc.Select(desc.Text.LastIndexOf("red"),1);if(desc.SelectionColor.R<=desc.SelectionColor.B)throw new Exception("Color stack reset failed");
 desc.Select(desc.Text.IndexOf("low"),1);if(desc.SelectionCharOffset>=0)throw new Exception("Subscript failed");
 desc.Select(desc.Text.IndexOf("high"),1);if(desc.SelectionCharOffset<=0)throw new Exception("Superscript failed");
 desc.Select(desc.Text.IndexOf("🌙"),2);if(!SystemGlyphCoverage.Covers(desc.SelectionFont.Name,"🌙"))throw new Exception("Glyph fallback selected a font without the requested glyph");
 t.GetMethod("RenderDescription",flags).Invoke(f,new object[]{"[FFFF00]❀原版拓展❀好玩耐玩❀"});
 for(int j=0;j<desc.TextLength;j++)if(desc.Text[j]=='❀'){desc.Select(j,1);if(desc.SelectionFont.Name!="Segoe UI Symbol"||!SystemGlyphCoverage.Covers(desc.SelectionFont.Name,"❀"))throw new Exception("Flower does not use the installed symbol font");}
 if(desc.Text!="❀原版拓展❀好玩耐玩❀")throw new Exception("Flower text was replaced or lost");
 t.GetMethod("RenderDescription",flags).Invoke(f,new object[]{"[FFC0CB]❤\uFE0F爸❤妈🔥火焰"});
 if(desc.Text.Contains("\uFE0F")||!desc.Text.Contains("❤爸❤妈🔥火焰"))throw new Exception("Presentation selectors leaked or emoji text lost");
 foreach(string symbol in new[]{"❤","🔥"}){desc.Select(desc.Text.IndexOf(symbol),symbol.Length);if(desc.SelectionFont.Name!="Segoe UI Emoji")throw new Exception("Verified game emoji does not use its supported system font");}
 lang.SelectedIndex=0;theme.SelectedIndex=0;Application.DoEvents();if(!f.Text.Contains("七日杀")||f.BackColor!=Color.Black)throw new Exception("Chinese dark failed");desc.Select(desc.Text.IndexOf("Welcome"),1);if(desc.SelectionColor.ToArgb()!=Color.Lime.ToArgb())throw new Exception("Dark description color changed");
 lang.SelectedIndex=1;theme.SelectedIndex=1;f.Close();}
 using(var f=new BrowserForm(true)){if(f.BackColor!=Color.White||!f.Text.Contains("Server Browser"))throw new Exception("Preferences did not restore");f.Close();}
 Console.WriteLine("PASS: language/themes, description contrast/newlines, uppercase NGUI styles, nested color reset, sub/sup, URL wrappers and Emoji font fallback.");
 }
}
