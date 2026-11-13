using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace SevenDaysBrowser {
 internal sealed class ColorValueLabel:Label {
  internal bool UseColorCodes;
  private const TextFormatFlags Flags=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.PreserveGraphicsClipping;
  protected override void OnPaint(PaintEventArgs e){
   var matches=Regex.Matches(Text,@"\[([0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?|-|/?[bicu])\]");
   if(!UseColorCodes||matches.Count==0){base.OnPaint(e);return;}
   var runs=new System.Collections.Generic.List<Tuple<string,Color>>();int start=0;Color color=ForeColor;
   foreach(Match match in matches){if(match.Index>start)runs.Add(Tuple.Create(Text.Substring(start,match.Index-start),color));string tag=match.Groups[1].Value;if(tag=="-")color=ForeColor;else if(tag.Length>=6)color=BrowserForm.ReadableDescriptionColor(ColorTranslator.FromHtml("#"+tag.Substring(0,6)));start=match.Index+match.Length;}
   if(start<Text.Length)runs.Add(Tuple.Create(Text.Substring(start),color));
   int available=Math.Max(0,ClientSize.Width-8),height=TextRenderer.MeasureText(e.Graphics,"Ag",Font,Size.Empty,Flags).Height;
   Func<string,int> measure=text=>TextRenderer.MeasureText(e.Graphics,text,Font,Size.Empty,Flags).Width;
   int width=runs.Sum(run=>measure(run.Item1));bool clipped=width>available;int budget=clipped?Math.Max(0,available-measure("…")):available;
   var visible=new System.Collections.Generic.List<Tuple<string,Color>>();int used=0;
   foreach(var run in runs){string part=run.Item1;while(part.Length>0&&measure(part)>budget-used)part=part.Substring(0,part.Length-1);if(part.Length>0&&char.IsHighSurrogate(part[part.Length-1]))part=part.Substring(0,part.Length-1);if(part.Length>0){visible.Add(Tuple.Create(part,run.Item2));used+=measure(part);}if(part.Length<run.Item1.Length)break;}
   if(clipped){visible.Add(Tuple.Create("…",ForeColor));used+=measure("…");}
   int x=Math.Max(4,(ClientSize.Width-used)/2),y=Math.Max(0,(ClientSize.Height-height)/2);
   e.Graphics.Clear(BackColor);var state=e.Graphics.Save();e.Graphics.SetClip(new Rectangle(4,0,available,ClientSize.Height));
   foreach(var run in visible){TextRenderer.DrawText(e.Graphics,run.Item1,Font,new Point(x,y),run.Item2,Flags);x+=measure(run.Item1);}
   e.Graphics.Restore(state);using(var pen=new Pen(GameTheme.Border))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
  }
 }
 public sealed partial class BrowserForm {
  private static void CopyDetails(ServerRow source,ServerRow target){foreach(var rule in source.Rules)target.Rules[rule.Key]=rule.Value;if(!string.IsNullOrEmpty(source.Description))target.Description=source.Description;if(!string.IsNullOrEmpty(source.Language))target.Language=source.Language;if(source.WorldSize.HasValue)target.WorldSize=source.WorldSize;if(source.GameTime.HasValue)target.GameTime=source.GameTime;if(source.Eac.HasValue)target.Eac=source.Eac;if(source.Crossplay.HasValue)target.Crossplay=source.Crossplay;if(source.Dedicated.HasValue)target.Dedicated=source.Dedicated;}
  private Control BuildDetailsPanel(){
   var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(8),BackColor=GameTheme.Background};
   panel.Paint+=(sender,e)=>{using(var pen=new Pen(GameTheme.Border))e.Graphics.DrawRectangle(pen,0,0,panel.Width-1,panel.Height-1);};
   panel.RowStyles.Add(new RowStyle(SizeType.Absolute,0));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,430));
   var name=new Label{Dock=DockStyle.Fill,ForeColor=GameTheme.Foreground,Text="",Font=new Font(Font.FontFamily,13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};detailLabels["name"]=name;panel.Controls.Add(name,0,0);
   description.Dock=DockStyle.Fill;description.ReadOnly=true;description.BackColor=GameTheme.Background;description.ForeColor=ink;description.BorderStyle=BorderStyle.FixedSingle;description.Font=new Font(Font.FontFamily,18);description.DetectUrls=false;description.LanguageOption&=~RichTextBoxLanguageOptions.AutoFont;description.Text="";
   description.BorderStyle=BorderStyle.None;var descriptionFrame=new Panel{Dock=DockStyle.Fill,BackColor=GameTheme.Background,Padding=new Padding(1)};descriptionFrame.Paint+=(sender,e)=>{using(var pen=new Pen(GameTheme.Border))e.Graphics.DrawRectangle(pen,0,0,descriptionFrame.Width-1,descriptionFrame.Height-1);};descriptionFrame.Controls.Add(description);panel.Controls.Add(descriptionFrame,0,1);
   var info=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=8,Padding=new Padding(0,8,0,0)};info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   string[] keys={"time","size","region","language","crossplay","ip","port","version"};string[] titles={"游戏时间","世界规模","地区","语言","跨平台联机","服务器 IP","游戏端口","游戏版本"};
   for(int i=0;i<keys.Length;i++){info.RowStyles.Add(new RowStyle(SizeType.Percent,12.5f));info.Controls.Add(new Label{Text=titles[i],Font=new Font(Font.FontFamily,18,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=GameTheme.Foreground},0,i);var value=new ColorValueLabel{UseColorCodes=keys[i]=="language",Text="",Font=new Font(Font.FontFamily,18,FontStyle.Bold),BorderStyle=BorderStyle.FixedSingle,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true,BackColor=GameTheme.Background,Margin=new Padding(2)};GameTheme.StyleValue(value);detailLabels[keys[i]]=value;info.Controls.Add(value,1,i);}
   panel.Controls.Add(info,0,2);return BuildDetailsPages(panel);
  }
  private static string BoolState(bool? value){return value.HasValue?(value.Value?"开启":"关闭"):"未知";}
  private void UpdateDetails(){
   if(detailLabels.Count==0)return;string address=SelectedAddress();ServerRow row;
   if(address==null||!servers.TryGetValue(address,out row)){detailLabels["name"].Text="";description.Text="";description.Tag=null;lastSelectedAddress=null;UpdateRuleLabels(null);foreach(var item in detailLabels)if(item.Key!="name")item.Value.Text="";return;}
   if(lastSelectedAddress!=address){manual.Text=address;lastSelectedAddress=address;}
   UpdateRuleLabels(row);if(!row.Eac.HasValue)QueueDetails(row,true);
   detailLabels["name"].Text=row.Name;detailLabels["time"].Text=row.GameTime.HasValue?"第 "+(row.GameTime.Value/24000+1)+" 天  "+(row.GameTime.Value/1000%24).ToString("00")+":"+((int)(row.GameTime.Value/1000.0*60)%60).ToString("00"):"未知";
   detailLabels["size"].Text=row.WorldSize.HasValue?row.WorldSize.Value.ToString():"未知";detailLabels["region"].Text=ServerDetails.RegionName(row.Region);detailLabels["language"].Text=string.IsNullOrWhiteSpace(row.Language)?"未知":row.Language;
   detailLabels["crossplay"].Text=BoolState(row.Crossplay);int colon=address.LastIndexOf(':');detailLabels["ip"].Text=address.Substring(0,colon);detailLabels["port"].Text=address.Substring(colon+1);detailLabels["version"].Text=row.Version;
   string text=string.IsNullOrWhiteSpace(row.Description)?"":row.Description.Replace("\\r\\n","\n").Replace("\\n","\n");
   // Render NGUI colour tags as text styling, never as HTML or executable links.
   if((string)description.Tag!=text){description.Tag=text;RenderDescription(text);}
   ApplyLocalization();
  }
  internal static Color ReadableDescriptionColor(Color color){
   if(!GameTheme.Light)return color;
   while(0.2126*LinearChannel(color.R)+0.7152*LinearChannel(color.G)+0.0722*LinearChannel(color.B)>0.18)
    color=Color.FromArgb((int)(color.R*0.9),(int)(color.G*0.9),(int)(color.B*0.9));
   return color;
  }
  private static double LinearChannel(byte channel){double value=channel/255.0;return value<=0.04045?value/12.92:System.Math.Pow((value+0.055)/1.055,2.4);}
  private void PaintStatusIcon(object sender,DataGridViewCellPaintingEventArgs e){
   if(e.RowIndex<0&&(e.ColumnIndex==6||e.ColumnIndex==7)){
    e.PaintBackground(e.ClipBounds,true);e.Handled=true;
    GameIcons.Draw(e.Graphics,e.ColumnIndex==6?"server_players":"server_ping",new Rectangle(e.CellBounds.X+(e.CellBounds.Width-26)/2,e.CellBounds.Y+(e.CellBounds.Height-26)/2,26,26),GameTheme.Foreground);return;
   }
   if(e.RowIndex>=visible.Count||e.ColumnIndex<0||e.ColumnIndex>3)return;
   e.PaintBackground(e.ClipBounds,true);e.Handled=true;var row=e.RowIndex<0?new ServerRow{Eac=true,Password=true,Dedicated=false}:visible[e.RowIndex];var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float x=e.CellBounds.X+e.CellBounds.Width/2f,y=e.CellBounds.Y+e.CellBounds.Height/2f;
   bool lightSelection=GameTheme.Light&&e.RowIndex>=0&&(e.State&DataGridViewElementStates.Selected)!=0;
   Color foreground=lightSelection?Color.White:GameTheme.Foreground;
   Color inactive=lightSelection?Color.FromArgb(205,175,175):GameTheme.Light?Color.FromArgb(125,125,125):GameTheme.Border;
   string icon;Color color;
   if(e.ColumnIndex==0){icon="server_favorite";color=e.RowIndex<0?foreground:favorites.Contains(row.Address)?(lightSelection?Color.Gold:GameTheme.Red):(lightSelection?Color.FromArgb(215,215,215):GameTheme.Icon);}
   else if(e.ColumnIndex==1){icon=e.RowIndex<0||row.Dedicated==false?"ui_game_symbol_map_house":"ui_platform_pc";color=foreground;}
   else if(e.ColumnIndex==2){icon="server_icon_vac";color=row.Eac==true?foreground:inactive;}
   else{icon="server_locked";color=row.Password?foreground:inactive;}
   GameIcons.Draw(g,icon,new Rectangle((int)x-13,(int)y-13,26,26),color);
  }
 }
}









