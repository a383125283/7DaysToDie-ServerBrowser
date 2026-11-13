using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace SevenDaysBrowser {
 internal sealed class SearchStateButton:Button {
  internal bool Searching;
  protected override void OnPaint(PaintEventArgs e){
   if(!Searching){base.OnPaint(e);return;}
   e.Graphics.Clear(BackColor);using(var pen=new Pen((GameTheme.Light?Color.FromArgb(0,125,45):Color.FromArgb(0,255,0))))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
   TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,(GameTheme.Light?Color.FromArgb(0,125,45):Color.FromArgb(0,255,0)),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  }
 }
 internal sealed class RedStopButton:Button {
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(BackColor);using(var pen=new Pen(Enabled?GameTheme.Red:GameTheme.Background))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
   TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?GameTheme.Red:GameTheme.Red,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  }
 }
 public sealed partial class BrowserForm {
  private readonly Dictionary<string,Button> sectionButtons=new Dictionary<string,Button>();
  private readonly Dictionary<string,Control> detailPages=new Dictionary<string,Control>();
  private readonly Dictionary<string,Label> ruleLabels=new Dictionary<string,Label>();
  private string lastSelectedAddress;
  private Control BuildDetailsPages(Control server){
   var host=new Panel{Dock=DockStyle.Fill,BackColor=GameTheme.Background};
   detailPages["服务器"]=server;
   detailPages["普通"]=BuildRulePage(new[]{"ModdedConfig","StockSettings","SandboxPreset","SandboxCode","GameDifficulty","XPMultiplier","DayNightLength","DayLightLength"},new[]{"修改配置","原版设置","沙盒预设","沙盒代码","游戏难度","经验倍率 %","一天时长（分）","白昼时长（时）"});
   detailPages["高级"]=BuildRulePage(new[]{"MaxChunkAge","BuildCreate","MaxSpawnedZombies","MaxSpawnedAnimals","LootAbundance","LootRespawnDays","DropOnDeath","DropOnQuit"},new[]{"区块重置天数","创造模式","最大僵尸数量","最大动物数量","战利品丰度 %","战利品刷新天数","死亡掉落","退出掉落"});
   detailPages["多人"]=BuildRulePage(new[]{"PlayerKillingMode","LandClaimSize","LandClaimExpiryTime","LandClaimDecayMode","LandClaimOnlineDurabilityModifier","LandClaimOfflineDurabilityModifier","LandClaimOfflineDelay","BedrollExpiryTime","PartySharedKillRange"},new[]{"玩家伤害模式","领地范围","领地到期天数","领地衰减方式","在线领地耐久倍率","离线领地耐久倍率","离线延迟（分）","睡袋到期天数","组队经验共享范围"});
   foreach(var page in detailPages.Values)host.Controls.Add(page);
   SelectDetailsPage("服务器");return host;
  }
  private Control BuildRulePage(string[] keys,string[] names){
   var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(8),BackColor=GameTheme.Background};
   var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=keys.Length+1};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
   var intro=new Label{Text="",Dock=DockStyle.Fill,ForeColor=Color.LightGray,Height=52};table.Controls.Add(intro,0,0);table.SetColumnSpan(intro,2);table.RowStyles.Add(new RowStyle(SizeType.Absolute,0));
   for(int i=0;i<keys.Length;i++){table.RowStyles.Add(new RowStyle(SizeType.Absolute,52));table.Controls.Add(new Label{Text=names[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,Font=new Font(Font.FontFamily,14,FontStyle.Bold),ForeColor=GameTheme.Foreground},0,i+1);var value=new Label{Text="",Dock=DockStyle.Fill,AutoEllipsis=true,Margin=new Padding(8,7,2,7),TextAlign=ContentAlignment.MiddleCenter,Font=new Font(Font.FontFamily,15,FontStyle.Bold),BackColor=GameTheme.Background,BorderStyle=BorderStyle.FixedSingle,ForeColor=GameTheme.Foreground};GameTheme.StyleValue(value);ruleLabels[keys[i]]=value;table.Controls.Add(value,1,i+1);}
   scroll.Controls.Add(table);return scroll;
  }
  private void SelectDetailsPage(string name){
   foreach(var pair in detailPages)pair.Value.Visible=pair.Key==name;
   Control selected;if(detailPages.TryGetValue(name,out selected))selected.BringToFront();
   foreach(var pair in sectionButtons){pair.Value.BackColor=pair.Key==name?(GameTheme.Light?Color.FromArgb(225,225,225):Color.FromArgb(32,32,32)):GameTheme.Background;pair.Value.ForeColor=pair.Key==name?GameTheme.Foreground:GameTheme.Icon;pair.Value.FlatAppearance.BorderColor=pair.Key==name?GameTheme.Red:GameTheme.Border;}
  }
  private void UpdateRuleLabels(ServerRow row){foreach(var pair in ruleLabels){string value;pair.Value.Text=row!=null&&row.Rules!=null&&row.Rules.TryGetValue(pair.Key,out value)?FormatRule(pair.Key,value):(row==null?"":"未知");}}
  internal static string FormatRule(string key,string value){
   bool flag;if(bool.TryParse(value,out flag))return flag?"开启":"关闭";
   int n;if(int.TryParse(value,out n)){
    if(key=="PlayerKillingMode"&&n>=0&&n<4)return new[]{"禁止玩家伤害","仅盟友","仅陌生人","所有玩家"}[n];
    if(key=="LandClaimDecayMode"&&n>=0&&n<3)return new[]{"缓慢","快速","不衰减"}[n];
    if(key=="MaxChunkAge"&&n==-1)return "关闭";
    if((key=="LandClaimOnlineDurabilityModifier"||key=="LandClaimOfflineDurabilityModifier")&&n==0)return "无限";
    if(key=="PartySharedKillRange"&&n==0)return "关闭";
    if(key=="GameDifficulty"&&n>=0&&n<6)return new[]{"拾荒者","冒险家","游牧民","战士","生存专家","疯狂"}[n];
   }
   return value;
  }
 }
}




