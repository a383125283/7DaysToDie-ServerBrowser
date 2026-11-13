using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace SevenDaysBrowser {
 public sealed partial class BrowserForm {
  private ComboBox languageChoice,themeChoice;
  private bool english,loadingAppearance;
  private void BuildAppearanceControls(Panel title){
   var options=new FlowLayoutPanel{Dock=DockStyle.Right,Width=350,WrapContents=false,FlowDirection=FlowDirection.LeftToRight};
   languageChoice=new ComboBox{Width=160,DropDownStyle=ComboBoxStyle.DropDownList,Name="languageChoice"};languageChoice.Items.AddRange(new object[]{"简体中文","English"});languageChoice.SelectedIndex=0;
   themeChoice=new ComboBox{Width=170,DropDownStyle=ComboBoxStyle.DropDownList,Name="themeChoice"};themeChoice.Items.AddRange(new object[]{"深色 / Dark","浅色 / Light"});themeChoice.SelectedIndex=1;
   options.Controls.Add(languageChoice);options.Controls.Add(themeChoice);title.Controls.Add(options);
   foreach(var choice in new[]{languageChoice,themeChoice}){choice.Width=choice.Items.Cast<object>().Max(item=>TextRenderer.MeasureText(item.ToString(),choice.Font).Width)+SystemInformation.VerticalScrollBarWidth+8;}
   options.Width=languageChoice.Width+languageChoice.Margin.Horizontal+themeChoice.Width+themeChoice.Margin.Horizontal;
   languageChoice.SelectedIndexChanged+=(s,e)=>{english=languageChoice.SelectedIndex==1;ApplyLocalization();SaveAppearance();};
   themeChoice.SelectedIndexChanged+=(s,e)=>{GameTheme.Light=themeChoice.SelectedIndex==1;ApplyTheme();SaveAppearance();};
  }
  private void LoadAppearance(){
   loadingAppearance=true;string file=RuntimePaths.DataFile("appearance.txt");
   if(File.Exists(file)){var values=File.ReadAllLines(file);languageChoice.SelectedIndex=values.Contains("English")?1:0;themeChoice.SelectedIndex=values.Contains("Light")?1:0;}
   loadingAppearance=false;GameTheme.Light=themeChoice.SelectedIndex==1;ApplyTheme();ApplyLocalization();
  }
  private void SaveAppearance(){if(!loadingAppearance)File.WriteAllLines(RuntimePaths.DataFile("appearance.txt"),new[]{english?"English":"Chinese",GameTheme.Light?"Light":"Dark"});}
  private void ApplyTheme(){
   Recolor(this);grid.BackgroundColor=bg;grid.GridColor=GameTheme.Border;
   foreach(var style in new[]{grid.DefaultCellStyle,grid.RowsDefaultCellStyle,grid.AlternatingRowsDefaultCellStyle,grid.ColumnHeadersDefaultCellStyle}){style.BackColor=bg;style.ForeColor=ink;style.SelectionBackColor=GameTheme.Selected;style.SelectionForeColor=Color.White;}
   stop.ForeColor=GameTheme.Red;
   foreach(var pair in sectionButtons){if(detailPages[pair.Key].Visible){SelectDetailsPage(pair.Key);break;}}
   description.Tag=null;RefreshRows();Invalidate(true);
  }
  private void Recolor(Control parent){
   parent.BackColor=bg;parent.ForeColor=ink;
   var button=parent as Button;if(button!=null){button.FlatAppearance.BorderColor=GameTheme.Border;button.FlatAppearance.MouseOverBackColor=GameTheme.Light?Color.FromArgb(230,230,230):GameTheme.Hover;}
   foreach(Control child in parent.Controls)Recolor(child);
  }
  private static readonly Dictionary<string,string> Translations=MakeTranslations();
  private static Dictionary<string,string> MakeTranslations(){
   var map=new Dictionary<string,string>();
   foreach(string line in TranslationData.Split('\n')){int split=line.IndexOf('|');if(split>0)map[line.Substring(0,split)]=line.Substring(split+1).TrimEnd('\r');}return map;
  }
  private string Translate(string value){
   if(string.IsNullOrEmpty(value))return value;
   value=System.Text.RegularExpressions.Regex.Replace(value,english?@"第 (\d+) 天  ":@"Day (\d+)  ",english?"Day $1  ":"第 $1 天  ");
   foreach(var pair in Translations.OrderByDescending(p=>english?p.Key.Length:p.Value.Length))value=value.Replace(english?pair.Key:pair.Value,english?pair.Value:pair.Key);
   return value;
  }
  private void TranslateControls(Control control){
   if(!(control is TextBoxBase)&&!(control is ComboBox)&&control!=grid&&(!detailLabels.ContainsKey("name")||control!=detailLabels["name"])){
    string text=Translate(control.Text);if(control.Text!=text)control.Text=text;
   }
   if(control is DataGridView||control is ComboBox||control is TextBoxBase)return;
   foreach(Control child in control.Controls)TranslateControls(child);
  }
  private void ApplyLocalization(){
   if(languageChoice==null)return;TranslateControls(this);
   foreach(var button in statisticButtons)if(button!=null)statisticsTips.SetToolTip(button,Translate(statisticsTips.GetToolTip(button)));
   foreach(DataGridViewColumn column in grid.Columns){column.HeaderText=Translate(column.HeaderText);column.HeaderCell.ToolTipText=Translate(column.HeaderCell.ToolTipText);}
  }
  private const string TranslationData=@"扩展服务器搜索 · 自动去重 · 一键连接|Extended server search · Automatic deduplication · One-click connect
专用服务器|Dedicated Servers
主机联机|Peer Hosts
好友服务器|Friends
收藏与历史|Favorites and History
局域网服务器|LAN Servers
；再次点击取消分类|; click again to show all
收藏；点击星号收藏或取消收藏|Favorite; click the star to toggle
服务器类型：PC 专用服务器 / 主机联机|Type: PC dedicated server / peer host
EAC 反作弊：亮盾开启，暗盾关闭或未知|EAC: bright shield on, dim shield off or unknown
亮锁：有密码；暗锁：无密码|Bright lock: password required; dim lock: no password
请先选中一个服务器。|Select a server first.
复制失败|Copy failed
无法发起连接|Unable to connect
请输入有效的 IP:端口，例如 192.0.2.1:26900。|Enter a valid IP:port, for example 192.0.2.1:26900.
当前 Steam 连接入口仅支持 IPv4 地址。|This Steam connection method supports IPv4 only.
正在释放查询会话，稍后发起连接…|Releasing search session before connecting…
已向运行中的游戏发送连接请求：|Connection request sent to the running game: 
已请求 Steam 启动七日杀并加入：|Requested Steam to launch and join: 
查询超时，已释放 Steam 会话并保留结果。|Search timed out. Steam session released; results retained.
查询进程已退出。|Search process exited.
查询启动失败：|Search failed to start: 
收到无效条目：|Invalid result: 
本轮查询达到时间上限，保留结果|Search time limit reached; results retained
，Steam＋EOS 响应 |, Steam + EOS records: 
 条；EOS |; EOS 
 批 / | batches / 
 条| records
；部分分组仍达上限，结果可能不全|; some groups reached the limit; results may be incomplete
，已释放查询会话。响应记录 |; search session released. Records: 
EOS 无法启动：|EOS could not start: 
EOS 登录超时|EOS sign-in timed out
EOS 查询超时，已保留返回结果|EOS search timed out; results retained
；保留 Steam 查询结果。|; Steam results retained.
EOS 查询失败：|EOS search failed: 
七日杀 · 服务器浏览器|7 Days to Die · Server Browser
七日杀服务器浏览器|7 Days to Die Server Browser
名称 / IP|Name / IP
新搜索|New Search
正在搜索|Searching
停止|Stop
只看有人的|Has players
无需密码|No password
连接至 IP…|Connect to IP…
连接服务器|Connect to Server
复制地址|Copy Address
服务器名称|Server Name
服务器 IP|Server IP
游戏端口|Game Port
游戏版本|Version
游戏时间|Game Time
世界规模|World Size
跨平台联机|Crossplay
北美东部|NA East
北美西部|NA West
中美洲|C. Am.
南美洲|S. Am.
大洋洲|Oce.
未知地区|Other
俄罗斯|Russia
欧洲|Europe
亚洲|Asia
中东|M. East
非洲|Africa
全部|All
普通|General
高级|Advanced
多人|Multiplayer
地区|Region
语言|Language
服务器|Servers
世界|World
人数|Players
延迟|Ping
开启|On
关闭|Off
未知|Unknown
修改配置|Modified Config
原版设置|Default Settings
沙盒预设|Sandbox Preset
沙盒代码|Sandbox Code
游戏难度|Difficulty
经验倍率 %|XP Multiplier %
一天时长（分）|Day Length (min)
白昼时长（时）|Daylight (hours)
区块重置天数|Chunk Reset Days
创造模式|Creative Mode
最大僵尸数量|Max Zombies
最大动物数量|Max Animals
战利品丰度 %|Loot Abundance %
战利品刷新天数|Loot Respawn Days
死亡掉落|Drop on Death
退出掉落|Drop on Quit
玩家伤害模式|Player Damage
领地范围|Claim Size
领地到期天数|Claim Expiry Days
领地衰减方式|Claim Decay
在线领地耐久倍率|Online Claim Durability
离线领地耐久倍率|Offline Claim Durability
离线延迟（分）|Offline Delay (min)
睡袋到期天数|Bedroll Expiry Days
组队经验共享范围|Shared XP Range
禁止玩家伤害|No Player Damage
仅盟友|Allies Only
仅陌生人|Strangers Only
所有玩家|All Players
缓慢|Slow
快速|Fast
不衰减|No Decay
无限|Unlimited
拾荒者|Scavenger
冒险家|Adventurer
游牧民|Nomad
战士|Warrior
生存专家|Survivalist
疯狂|Insane
联系方式|Contact
界面预览（无演示服务器数据）|Preview (no sample servers)
无法连接 Steam，请先登录拥有七日杀的 Steam 账号。|Cannot connect to Steam. Sign in to a Steam account that owns 7 Days to Die.
请先登录 Steam，再点击搜索；仅查询期间可能显示游戏运行中。|Sign in to Steam, then search. Steam may show the game as running during queries.
正在查询，Steam 可能暂时显示七日杀运行中…|Searching; Steam may temporarily show the game as running…
正在登录 EOS 服务器目录…|Signing in to the EOS server directory…
EOS 已登录，正在查询游戏服务器目录…|EOS signed in. Querying the game server directory…
正在补查 EOS：第 |Searching EOS: batch 
 批，待查 |, pending 
 批，已返回 |, received 
 条…| records…
搜索完成|Search complete
已停止搜索。|Search stopped.
已复制：|Copied: 
去重后 |Unique: 
 台 / 本页显示 | / on this page: 
 台 / 未取得地区 | / region pending: 
 台| servers
 秒| s
";
 }
}
