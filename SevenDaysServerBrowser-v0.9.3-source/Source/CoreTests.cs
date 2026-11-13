using System.Linq;
using System;
using SevenDaysBrowser;
using Steamworks;
class CoreTests {
 static void Check(bool ok,string m){if(!ok){Console.WriteLine("FAIL: "+m);throw new Exception(m);}}
 [STAThread] static void Main(){
  Check(Connection.UriFor("192.0.2.10:26900")=="steam://connect/192.0.2.10:26900","Steam URI");
  Check(Uri.UnescapeDataString(Connection.LaunchUriFor("192.0.2.10:26900"))=="steam://run/251570//-SessionInvite=STM192.0.2.10:26900/","launch invite URI");
  foreach(string bad in new[]{"192.0.2.10:0","192.0.2.10:65536","bad:26900","192.0.2.10:26900/evil","192.0.2.10:26900\" -noeac","::1:26900"}){
   bool rejected=false;try{Connection.UriFor(bad);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid endpoint: "+bad);
  }
  var s=new gameserveritem_t();s.m_NetAdr.Init(0xC000020A,26902,26900);s.SetServerName("同名服");s.SetMap("Navezgane");s.m_nPlayers=8;s.m_nMaxPlayers=20;s.m_nPing=88;
  var row=ServerRow.FromSteam(s);Check(row.Address=="192.0.2.10:26900","must use connection port rather than query port");
  var dict=new System.Collections.Generic.Dictionary<string,ServerRow>();dict[row.Address]=row;
  s.SetServerName("新名称");var same=ServerRow.FromSteam(s);dict[same.Address]=same;Check(dict.Count==1,"same endpoint updates");
  s.m_NetAdr.SetConnectionPort(26901);var different=ServerRow.FromSteam(s);dict[different.Address]=different;Check(dict.Count==2,"different port preserved");
  var unanswered=new ServerRow{Address="192.0.2.8:26900",Name="No response",Map="Wrong",Players=9999,Ping=-1,Password=true};
  var responding=new ServerRow{Address=unanswered.Address,Name="Responding",Map="Real",Players=8,Capacity=20,Ping=80,Password=false,Eac=false};
  var kept=BrowserForm.MergeServerRecords(unanswered,responding);Check(kept.Name=="Responding"&&kept.Map=="Real"&&kept.Players==8&&!kept.Password&&kept.Ping==80,"responding duplicate replaces unanswered record, including its actual fields");
  kept=BrowserForm.MergeServerRecords(kept,unanswered);Check(kept.Name=="Responding"&&kept.Players==8&&kept.Ping==80,"late unanswered duplicate cannot overwrite responding record");
  kept=BrowserForm.MergeServerRecords(kept,new ServerRow{Address=kept.Address,Name="Faster",Ping=20});Check(kept.Name=="Faster"&&kept.Ping==20,"lowest valid response latency wins");
  kept=BrowserForm.MergeServerRecords(kept,new ServerRow{Address=kept.Address,Name="Equal",Ping=20});Check(kept.Name=="Faster","equal latency retains current record");
  kept=BrowserForm.MergeServerRecords(kept,new ServerRow{Address=kept.Address,Name="Invalid sentinel",Ping=int.MaxValue});Check(kept.Name=="Faster","invalid ping sentinel cannot win");
  Check(BrowserForm.HasResponse(new ServerRow{Ping=0})&&!BrowserForm.HasResponse(new ServerRow()),"zero latency valid; unmeasured defaults to unknown");
  var firstUnknown=new ServerRow{Name="First",Ping=-1};Check(BrowserForm.MergeServerRecords(firstUnknown,new ServerRow{Name="Second",Ping=-1}).Name=="First","all-unanswered keeps one stable record");
  var broken=new ServerRow{Address="192.0.2.88:26900",Name="\uFFFD\uFFFDName",Map="\uFFFDMap",Ping=15};
  var readable=new ServerRow{Address=broken.Address,Name="正常中文服",Map="正常地图",Ping=-1};
  var repaired=BrowserForm.MergeServerRecords(broken,readable);Check(repaired.Name=="正常中文服"&&repaired.Map=="正常地图"&&repaired.Ping==15,"readable EOS text repairs damaged low-latency Steam record without replacing its ping");
  repaired=BrowserForm.MergeServerRecords(repaired,new ServerRow{Address=broken.Address,Name="\uFFFDnew",Map="\uFFFDnew",Ping=10});Check(repaired.Name=="正常中文服"&&repaired.Map=="正常地图"&&repaired.Ping==10,"faster damaged discovery cannot reintroduce replacement characters");
  var direct=new ServerRow();ServerDetails.Parse("GameHost:（梦境）纯净二服;LevelName:纯净服;",direct);ServerDetails.ApplyQueriedNames(direct,repaired);Check(repaired.Name=="（梦境）纯净二服"&&repaired.Map=="纯净服","authoritative TCP names and map overwrite damaged discovery text");
  direct.Rules["GameHost"]="\uFFFD";direct.Rules["LevelName"]="\uFFFD";ServerDetails.ApplyQueriedNames(direct,repaired);Check(repaired.Name=="（梦境）纯净二服"&&repaired.Map=="纯净服","damaged TCP text cannot overwrite readable values");
  Check(ServerDetails.SteamVersion(10340)=="V3.4.0","Steam version display");
  Check(ServerDetails.SteamVersion(10206)=="V2.6","older V version display");
  Check(ServerDetails.FullVersion("V.3.40.10")=="V3.4.0 (b10)","full build version");
  Check(ServerDetails.FullVersion("Alpha.21.2.37")=="A21.2 (b37)","Alpha version");
  var detailRow=new ServerRow{Region="Unknown"};ServerDetails.Parse("Region:Asia;ServerVersion:V.3.40.10;EACEnabled:True;AllowCrossplay:False;IsDedicated:True;WorldSize:8192;CurrentServerTime:7000;Language:Chinese;ServerDescription:[00FF00]测试服务器[-];",detailRow);
  Check(detailRow.Region=="Asia"&&detailRow.Version=="V3.4.0 (b10)"&&detailRow.Eac==true&&detailRow.Crossplay==false&&detailRow.Dedicated==true&&detailRow.WorldSize==8192&&detailRow.GameTime==7000&&detailRow.Language=="Chinese","detail fields");
  var invalidRegion=new ServerRow();ServerDetails.Parse("Region:invented;",invalidRegion);Check(invalidRegion.Region=="Unknown","unrecognized region");
  var eosValues=new System.Collections.Generic.Dictionary<string,string>{{"GAMEHOST","中文测试服~$#$~中文测试服"},{"LEVELNAME","测试地图~$#$~测试地图"},{"PORT","26900"},{"REGION","Asia~$#$~asia"},{"SERVERVERSION","V.3.40.10~$#$~v.3.40.10"},{"-BOOLVALUES-",",IsDedicated=1,IsPasswordProtected=1,EACEnabled=1,AllowCrossplay=0,"},{"GAMEDIFFICULTY","3"},{"CURRENTPLAYERS","9"}};
  var eosRow=EosDirectory.Parse(eosValues,"192.0.2.30",20,0);
  Check(eosRow.Name=="中文测试服"&&eosRow.Map=="测试地图"&&eosRow.Address=="192.0.2.30:26900"&&eosRow.Region=="Asia"&&eosRow.Version=="V3.4.0 (b10)"&&eosRow.Players==9&&eosRow.Password&&eosRow.Eac==true&&eosRow.Crossplay==false&&eosRow.Dedicated==true&&eosRow.Rules["GameDifficulty"]=="3","EOS uppercase keys, search suffix, bool-set and game endpoint");
  eosValues["PORT"]="0";Check(EosDirectory.Parse(eosValues,"192.0.2.30",20,0)==null,"invalid EOS endpoint rejected");
  var baseQuery=new EosDirectory.Query{Crossplay=true};
  Check(!EosDirectory.Expand(baseQuery,199).Any(),"unsaturated EOS query does not expand");
  var partitions=EosDirectory.Expand(baseQuery,200).ToArray();Check(partitions.Length==7&&partitions[0].Min==0&&partitions[6].Max==int.MaxValue&&partitions.All(q=>q.Crossplay),"EOS partition coverage and crossplay branch retained");
  for(int i=1;i<partitions.Length;i++)Check(partitions[i].Min==partitions[i-1].Max+1,"EOS player ranges have no gaps");
  var regions=EosDirectory.Expand(partitions[0],200).ToArray();Check(regions.Length==10&&regions.Any(q=>q.Region=="Asia")&&regions.All(q=>q.Min==0&&q.Max==0)&&!EosDirectory.Expand(regions[0],200).Any(),"saturated EOS player ranges expand by game-defined region and stop at final partition");
  var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();
  int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
  var send=System.Threading.Tasks.Task.Run(()=>{using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
   byte[] bytes=System.Text.Encoding.UTF8.GetBytes("Region:Europe;ServerVersion:V.3.40.10;\r\n");
   byte[] header=System.Text.Encoding.ASCII.GetBytes(bytes.Length.ToString("00000")+"\r\n");
   foreach(byte b in header)stream.WriteByte(b);stream.Write(bytes,0,bytes.Length);
  }});
  var netRow=new ServerRow{Address="127.0.0.1:"+port};ServerDetails.Fetch(netRow,System.Threading.CancellationToken.None);send.Wait();listener.Stop();
  Check(netRow.Region=="Europe"&&netRow.Version=="V3.4.0 (b10)"&&netRow.Ping>=0,"TCP framing and metadata integration");
  using(var form=new BrowserForm(true)){
   form.Show();System.Windows.Forms.Application.DoEvents();
   var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var type=typeof(BrowserForm);
   var data=(System.Collections.Generic.Dictionary<string,ServerRow>)type.GetField("servers",flags).GetValue(form);
   data["192.0.2.1:26900"]=new ServerRow{Name="A",Map="World",Address="192.0.2.1:26900",Version="V3.4.0 (b10)",Region="Asia",Players=2,Ping=20,Eac=true,Description="[00FF00]测试服务器[-]",Language="Chinese",WorldSize=8192,GameTime=7000,Crossplay=false,Dedicated=true};
   data["192.0.2.2:26900"]=new ServerRow{Name="B",Map="World",Address="192.0.2.2:26900",Version="V3.3.0",Region="Europe",Players=10,Ping=100,Eac=false};
   data["192.0.2.1:26900"].Rules["PlayerKillingMode"]="3";data["192.0.2.1:26900"].Rules["XPMultiplier"]="200";
   type.GetMethod("RefreshRows",flags).Invoke(form,null);
   var grid=(System.Windows.Forms.DataGridView)type.GetField("grid",flags).GetValue(form);
   var click=typeof(System.Windows.Forms.DataGridView).GetMethod("OnColumnHeaderMouseClick",flags);
   var evt=new System.Windows.Forms.DataGridViewCellMouseEventArgs(6,-1,0,0,new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,1,0,0,0));
   click.Invoke(grid,new object[]{evt});Check(Convert.ToString(grid.Rows[0].Cells["name"].Value)=="A","ascending numeric player sort");
   click.Invoke(grid,new object[]{evt});Check(Convert.ToString(grid.Rows[0].Cells["name"].Value)=="B","descending numeric player sort");
   var tabs=(System.Windows.Forms.FlowLayoutPanel)type.GetField("regionTabs",flags).GetValue(form);
   foreach(System.Windows.Forms.Button button in tabs.Controls)if((string)button.Tag=="Asia")button.PerformClick();
   Check(grid.RowCount==1&&Convert.ToString(grid.Rows[0].Cells["name"].Value)=="A","region tab filter");
   grid.CurrentCell=grid.Rows[0].Cells[4];grid.Rows[0].Selected=true;System.Windows.Forms.Application.DoEvents();
   type.GetMethod("UpdateDetails",flags).Invoke(form,null);
   var labels=(System.Collections.Generic.Dictionary<string,System.Windows.Forms.Label>)type.GetField("detailLabels",flags).GetValue(form);
   Check(labels["version"].Text=="V3.4.0 (b10)"&&labels["ip"].Text=="192.0.2.1"&&labels["port"].Text=="26900","selection must update detail address/version");
   Check(labels["time"].Text=="第 1 天  07:00"&&labels["crossplay"].Text=="关闭","game time and anti-cheat details");
   var input=(System.Windows.Forms.TextBox)type.GetField("manual",flags).GetValue(form);Check(input.Text=="192.0.2.1:26900","selection autofills endpoint");
   var searchButton=(System.Windows.Forms.Button)type.GetField("search",flags).GetValue(form);
   type.GetField("searching",flags).SetValue(form,true);type.GetMethod("UpdateStatus",flags).Invoke(form,null);
   Check(searchButton.Text=="正在搜索"&&((SearchStateButton)searchButton).Searching,"green searching button state");
   type.GetField("searching",flags).SetValue(form,false);type.GetMethod("UpdateStatus",flags).Invoke(form,null);Check(searchButton.Text=="新搜索","idle search button text");
   Check(!grid.AllowUserToResizeRows&&!grid.AllowUserToResizeColumns,"fixed table dimensions");Check(!labels.ContainsKey("password"),"password detail row removed");
   var buttons=(System.Collections.Generic.Dictionary<string,System.Windows.Forms.Button>)type.GetField("sectionButtons",flags).GetValue(form);
   var pages=(System.Collections.Generic.Dictionary<string,System.Windows.Forms.Control>)type.GetField("detailPages",flags).GetValue(form);
   var rules=(System.Collections.Generic.Dictionary<string,System.Windows.Forms.Label>)type.GetField("ruleLabels",flags).GetValue(form);
   buttons["多人"].PerformClick();Check(pages["多人"].Visible&&!pages["服务器"].Visible&&rules["PlayerKillingMode"].Text=="所有玩家","multiplayer tab and data");
   buttons["普通"].PerformClick();Check(pages["普通"].Visible&&rules["XPMultiplier"].Text=="200","general tab and data");
   buttons["高级"].PerformClick();Check(pages["高级"].Visible,"advanced tab");buttons["服务器"].PerformClick();Check(pages["服务器"].Visible,"server tab");
   var desc=(System.Windows.Forms.RichTextBox)type.GetField("description",flags).GetValue(form);Check(desc.Text=="测试服务器","safe coloured description rendering");
   foreach(System.Windows.Forms.Button button in tabs.Controls)if((string)button.Tag=="All")button.PerformClick();
   Check(grid.RowCount==2&&tabs.Controls.Count==12,"all regions available and switching restores list");
   for(int i=3;i<=38;i++)data["192.0.2."+i+":26900"]=new ServerRow{Name="Server"+i,Map="World",Address="192.0.2."+i+":26900",Version="V3.4.0",Region="Asia",Eac=true,Dedicated=true,Ping=i};
   type.GetMethod("RefreshRows",flags).Invoke(form,null);
   Check(grid.RowCount==15&&(int)type.GetField("pageCount",flags).GetValue(form)==3,"fifteen rows per page");
   ((System.Windows.Forms.Button)type.GetField("nextPage",flags).GetValue(form)).PerformClick();
   Check((int)type.GetField("pageIndex",flags).GetValue(form)==1&&grid.RowCount==15,"next page");
   ((System.Windows.Forms.Button)type.GetField("nextPage",flags).GetValue(form)).PerformClick();Check(grid.RowCount==8,"last page remainder");
   var wheel=grid.GetType().GetMethod("OnMouseWheel",flags);
   wheel.Invoke(grid,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.None,0,10,10,-120)});Check((int)type.GetField("pageIndex",flags).GetValue(form)==2,"wheel stays at last page");
   wheel.Invoke(grid,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.None,0,10,10,120)});Check((int)type.GetField("pageIndex",flags).GetValue(form)==1,"wheel previous page");
   wheel.Invoke(grid,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.None,0,10,10,-120)});Check((int)type.GetField("pageIndex",flags).GetValue(form)==2,"wheel next page");
   form.Height=740;System.Windows.Forms.Application.DoEvents();
   ((System.Windows.Forms.Button)type.GetField("previousPage",flags).GetValue(form)).PerformClick();grid.FirstDisplayedScrollingRowIndex=0;
   Check(grid.DisplayedRowCount(false)<grid.RowCount,"test viewport must require vertical scrolling");
   wheel.Invoke(grid,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.None,0,10,10,-120)});
   Check((int)type.GetField("pageIndex",flags).GetValue(form)==1&&grid.FirstDisplayedScrollingRowIndex>0,"wheel scrolls inside current page before paging");
   grid.FirstDisplayedScrollingRowIndex=grid.RowCount-1;
   wheel.Invoke(grid,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.None,0,10,10,120)});
   Check((int)type.GetField("pageIndex",flags).GetValue(form)==1,"upward wheel scrolls current page before previous page");
   grid.FirstDisplayedScrollingRowIndex=grid.RowCount-1;
   wheel.Invoke(grid,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.None,0,10,10,-120)});
   Check((int)type.GetField("pageIndex",flags).GetValue(form)==2&&grid.FirstDisplayedScrollingRowIndex==0,"bottom wheel enters next page at top");
   wheel.Invoke(grid,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.None,0,10,10,120)});
   Check((int)type.GetField("pageIndex",flags).GetValue(form)==1&&grid.FirstDisplayedScrollingRowIndex+grid.DisplayedRowCount(false)>=grid.RowCount,"top wheel enters previous page at bottom");
   Check(input.Parent.Height==42,"endpoint frame matches button height");
   var stats=(System.Windows.Forms.Button[])type.GetField("statisticButtons",flags).GetValue(form);
   stats[0].PerformClick();Check((int)type.GetField("pageIndex",flags).GetValue(form)==0&&grid.RowCount==15,"category switch resets page");
   stats[2].PerformClick();Check(grid.RowCount==0,"friends classification does not invent friends");
   stats[2].PerformClick();Check(grid.RowCount==15,"second click cancels category");
   grid.ClearSelection();type.GetMethod("UpdateDetails",flags).Invoke(form,null);
   Check(labels.Values.All(label=>label.Text=="")&&desc.Text=="","no-selection details must be blank");
   Check(rules.Values.All(label=>label.Text==""),"no-selection settings must be blank");
   data.Clear();
   var queued=(System.Collections.Generic.HashSet<string>)type.GetField("detailsQueued",flags).GetValue(form);queued.Add(eosRow.Address);
   ((System.Collections.Generic.HashSet<string>)type.GetField("priorityQueued",flags).GetValue(form)).Add(eosRow.Address);
   var messages=(System.Collections.Concurrent.ConcurrentQueue<Tuple<int,string>>)type.GetField("messages",flags).GetValue(form);
   int generation=(int)type.GetField("generation",flags).GetValue(form);var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
   var steamRow=new ServerRow{Address=eosRow.Address,Name="Steam 名称",Map="测试地图",Version="V3.4.0",Ping=20,IsFriend=true};
   foreach(var item in new[]{steamRow,eosRow,steamRow})messages.Enqueue(Tuple.Create(generation,"SERVER "+serializer.Serialize(item)));
   messages.Enqueue(Tuple.Create(generation,"DETAIL "+serializer.Serialize(steamRow)));type.GetMethod("Tick",flags).Invoke(form,null);
   var merged=data[eosRow.Address];Check(data.Count==1&&merged.Region=="Asia"&&merged.Version=="V3.4.0 (b10)"&&merged.Eac==true&&merged.IsFriend&&merged.Ping==20&&merged.Rules["GameDifficulty"]=="3","Steam/EOS dedup preserves EOS metadata and Steam relationship/ping; late empty TCP details cannot erase region/build");
   foreach(string region in new[]{"Europe","Oceania"}){
    var duplicate=new ServerRow{Address=eosRow.Address,Name=eosRow.Name,Version=eosRow.Version,Region=region,Eac=true};
    messages.Enqueue(Tuple.Create(generation,"SERVER "+serializer.Serialize(duplicate)));
   }
   type.GetMethod("Tick",flags).Invoke(form,null);
   Check(data.Count==1&&data[eosRow.Address].Region=="Asia"&&data[eosRow.Address].AdvertisedRegions.Count==3,"duplicate regions accumulate without changing the existing primary region");
   foreach(string region in new[]{"All","Asia"}){
    foreach(System.Windows.Forms.Button button in tabs.Controls)if((string)button.Tag==region)button.PerformClick();
    Check(grid.RowCount==1,"language-classified endpoint remains visible once in: "+region);
   }
   foreach(string region in new[]{"Europe","Oceania","Africa"}){foreach(System.Windows.Forms.Button button in tabs.Controls)if((string)button.Tag==region)button.PerformClick();Check(grid.RowCount==0,"duplicate Chinese endpoint is removed from other regions: "+region);}
   var english=new ServerRow{Region="Europe",Language="English"};
   var laterEnglish=new ServerRow{Region="Oceania",Language="English"};ServerDetails.MergeRegions(english,laterEnglish);
   Check(laterEnglish.Region=="Europe","ambiguous English retains first registered region");
   var russian=new ServerRow{Region="Asia",Language="Russian"};
   var russianDuplicate=new ServerRow{Region="Europe",Language="Russian"};ServerDetails.MergeRegions(russian,russianDuplicate);Check(russianDuplicate.Region=="Russia","duplicate Russian endpoint classified as Russia");
   var singleChinese=new ServerRow{Region="Europe",Language="Chinese"};ServerDetails.RegisterRegion(singleChinese,"Europe");ServerDetails.ApplyRegion(singleChinese);Check(singleChinese.Region=="Europe","single-region entry retains game region");
   string replay=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"开发验证","eos-live-v2.txt");
   if(System.IO.File.Exists(replay)){
    data.Clear();const string cc="150.138.92.64:26900";queued.Add(cc);((System.Collections.Generic.HashSet<string>)type.GetField("priorityQueued",flags).GetValue(form)).Add(cc);
    int records=0;var capturedRegions=new System.Collections.Generic.HashSet<string>();
    foreach(string line in System.IO.File.ReadLines(replay))if(line.StartsWith("SERVER ")){
     var captured=serializer.Deserialize<ServerRow>(line.Substring(7));if(captured.Address!=cc)continue;
     messages.Enqueue(Tuple.Create(generation,line));records++;capturedRegions.Add(captured.Region);
    }
    type.GetMethod("Tick",flags).Invoke(form,null);
    Check(records>1&&data.Count==1&&data[cc].AdvertisedRegions.Count==capturedRegions.Count&&data[cc].Region=="Asia","CC real capture replay merges all advertised regions and classifies Chinese as Asia");
    foreach(string region in ServerDetails.RegionIds){foreach(System.Windows.Forms.Button button in tabs.Controls)if((string)button.Tag==region)button.PerformClick();Check(grid.RowCount==(region=="All"||region=="Asia"?1:0),"CC visibility after real capture replay: "+region);}
    Console.WriteLine("PASS: replayed "+records+" actual CC records; one address visible only in All/Asia, absent from all other region tabs.");
   }
   form.Close();
  }
  Console.WriteLine("PASS: clickable headers, numeric ascending/descending sorting, all region tabs and list switching.");
  Console.WriteLine("PASS: duplicate preference by valid response, lower latency, stable ties and measured TCP metadata latency.");
  Console.WriteLine("PASS: region mapping, version formatting, TCP metadata framing.");
  Console.WriteLine("PASS: EOS parsing, player/region supplementary queries and Steam/EOS UI merge.");
  Console.WriteLine("PASS: Steam URI validation, invalid endpoint rejection, game/query port separation, endpoint dedup and distinct port retention.");
 }
}









