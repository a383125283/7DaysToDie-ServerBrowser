using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Steamworks;
using System.Collections.Concurrent;
using System.Web.Script.Serialization;

namespace SevenDaysBrowser
{
    public sealed class ServerRow
    {
        public string Name, Map, Address, Version, Region="Unknown";
        public List<string> AdvertisedRegions=new List<string>();
        public int Players, Capacity; public int Ping=-1;
        public Dictionary<string,string> Rules=new Dictionary<string,string>();
        public string Description="", Language=""; public int? WorldSize, GameTime; public bool? Eac, Crossplay, Dedicated;
        public bool Password, IsFriend, IsHistory, IsLan;
        public static ServerRow FromSteam(gameserveritem_t s)
        {
            return new ServerRow { Name=s.GetServerName(), Map=s.GetMap(),
                Address=s.m_NetAdr.GetConnectionAddressString(), Version=ServerDetails.SteamVersion(s.m_nServerVersion),
                Players=s.m_nPlayers, Capacity=s.m_nMaxPlayers, Ping=s.m_nPing, Password=s.m_bPassword };
        }
    }
    public static class Connection
    {
        public static string Normalize(string text)
        {
            int colon=text.LastIndexOf(':'); int port; IPAddress ip;
            if(colon<1 || !IPAddress.TryParse(text.Substring(0,colon).Trim('[',']',' '),out ip)
                || !int.TryParse(text.Substring(colon+1),out port) || port<1 || port>65535)
                throw new ArgumentException("请输入有效的 IP:端口，例如 192.0.2.1:26900。");
            if(ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)
                throw new ArgumentException("当前 Steam 连接入口仅支持 IPv4 地址。");
            return ip+":"+port;
        }
        public static string UriFor(string address) { return "steam://connect/"+Normalize(address); }
        public static string LaunchUriFor(string address) { return "steam://run/251570//"+Uri.EscapeDataString("-SessionInvite=STM"+Normalize(address))+"/"; }
    }
    public sealed partial class BrowserForm : Form
    {
        private readonly Dictionary<string,ServerRow> servers=new Dictionary<string,ServerRow>();
        private readonly DataGridView grid=new PagedServerGrid();
        private List<ServerRow> visible=new List<ServerRow>();
        private readonly TextBox filter=new TextBox(), manual=new TextBox();
        private readonly CheckBox populated=new CheckBox(), unlocked=new CheckBox();
        private int sortColumn=7;
        private bool sortDescending;
        private string selectedRegion="All";
        private readonly FlowLayoutPanel regionTabs=new FlowLayoutPanel();
        private readonly System.Threading.SemaphoreSlim detailsGate=new System.Threading.SemaphoreSlim(24);
        private System.Threading.CancellationTokenSource detailsCancel=new System.Threading.CancellationTokenSource();
        private readonly HashSet<string> detailsQueued=new HashSet<string>();
        private readonly HashSet<string> priorityQueued=new HashSet<string>();
        private readonly System.Threading.SemaphoreSlim priorityGate=new System.Threading.SemaphoreSlim(2);
        private readonly Label status=new Label();
        private readonly RichTextBox description=new RichTextBox();
        private readonly Dictionary<string,Label> detailLabels=new Dictionary<string,Label>();
        private readonly HashSet<string> favorites=new HashSet<string>();
        private bool refreshing;
        private readonly string favoritesFile=RuntimePaths.DataFile("favorites.txt");
        private readonly Button search, stop;
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        private Process worker;
        private readonly ConcurrentQueue<Tuple<int,string>> messages=new ConcurrentQueue<Tuple<int,string>>();
        private readonly JavaScriptSerializer json=new JavaScriptSerializer();
        private int generation;
        private bool searching, dirty, connecting;
        private DateTime started, lastRender;
        private string phase="请先登录 Steam，再点击搜索；仅查询期间可能显示游戏运行中。";
        private Color bg {get{return GameTheme.Background;}} private Color card {get{return bg;}} private Color ink {get{return GameTheme.Foreground;}}

        public BrowserForm(bool preview)
        {
            GameFontFallback.Initialize();
            Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text="七日杀服务器浏览器 v0.9.3"; Size=new Size(1600,960); MinimumSize=new Size(1300,740);
            StartPosition=FormStartPosition.CenterScreen; BackColor=bg; ForeColor=ink;
            Font=new Font("Microsoft YaHei UI",10); AutoScaleMode=AutoScaleMode.Dpi;
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(18,18,18,6),ColumnCount=1,RowCount=7};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));root.RowStyles.Add(new RowStyle(SizeType.Absolute,0));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,68));root.RowStyles.Add(new RowStyle(SizeType.Absolute,30));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            Controls.Add(root);
            if(File.Exists(favoritesFile))foreach(var line in File.ReadAllLines(favoritesFile))favorites.Add(line);
            var title=new Panel {Dock=DockStyle.Fill,BackColor=bg};
            var heading=new Label {Text="七日杀 · 服务器浏览器",Font=new Font(Font.FontFamily,16,FontStyle.Bold),ForeColor=GameTheme.Foreground,AutoSize=true,Location=new Point(12,0),TextAlign=ContentAlignment.MiddleLeft};
            var subtitle=new Label {Text="扩展服务器搜索 · 自动去重 · 一键连接",Font=new Font(Font.FontFamily,10),AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
            title.Controls.Add(heading);title.Controls.Add(subtitle);
            root.Controls.Add(title,0,0);BuildAppearanceControls(title);
            Action alignTitle=()=>{var options=title.Controls.Cast<Control>().First(c=>c.Dock==DockStyle.Right);subtitle.SetBounds(heading.Right+18,heading.Top,Math.Max(0,title.ClientSize.Width-options.Width-heading.Right-30),heading.Height);};
            heading.SizeChanged+=(s,e)=>alignTitle();title.SizeChanged+=(s,e)=>alignTitle();alignTitle();
            var banner=new Panel{Dock=DockStyle.Fill,BackColor=GameTheme.Background,Margin=new Padding(3,0,3,0)};
            var tabs=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,BackColor=Color.Transparent,Padding=Padding.Empty};
            foreach(string page in new[]{"服务器","普通","高级","多人"}){string key=page;var tab=Button(page,()=>SelectDetailsPage(key));tab.Width=96;tab.Height=30;tab.Padding=Padding.Empty;tab.Margin=new Padding(0,2,6,2);tab.Font=new Font(Font.FontFamily,10,FontStyle.Regular);tab.AutoSize=false;tab.BackColor=GameTheme.Background;tab.FlatAppearance.BorderColor=GameTheme.Border;tabs.Controls.Add(tab);sectionButtons[key]=tab;}
            tabs.SizeChanged+=(sender,e)=>FitNavigation(tabs);
            banner.Controls.Add(tabs);
            var toolbar=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
            search=Button("新搜索",()=>StartSearch()); stop=Button("停止",()=>StopSearch("已停止搜索。"));
            stop.Enabled=false;
            filter.Width=210;filter.BackColor=card;filter.ForeColor=ink;filter.Margin=new Padding(12,7,6,0);
            toolbar.Controls.Add(new Label {Text="名称 / IP",AutoSize=true,Margin=new Padding(12,10,0,0)});toolbar.Controls.Add(GameTheme.WrapInput(filter));
            populated.Text="只看有人的";populated.AutoSize=true;populated.Margin=new Padding(8,10,0,0);
            unlocked.Text="无需密码";unlocked.AutoSize=true;unlocked.Margin=new Padding(10,10,0,0);
            toolbar.Controls.Add(populated);toolbar.Controls.Add(unlocked);
            root.Controls.Add(toolbar,0,2);
            grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;
            grid.AllowUserToResizeRows=false;grid.AllowUserToResizeColumns=false;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;grid.RowTemplate.Resizable=DataGridViewTriState.False;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowHeadersVisible=false;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor=card;grid.BorderStyle=BorderStyle.None;grid.AutoGenerateColumns=false;
            grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=GameTheme.Background;
            grid.ColumnHeadersDefaultCellStyle.ForeColor=GameTheme.Foreground;grid.ColumnHeadersHeight=44;
            grid.DefaultCellStyle.BackColor=card;grid.DefaultCellStyle.ForeColor=ink;grid.DefaultCellStyle.SelectionBackColor=GameTheme.Selected;
            grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.AlternatingRowsDefaultCellStyle.BackColor=GameTheme.Background;
            grid.GridColor=GameTheme.Border;grid.RowTemplate.Height=40;grid.DefaultCellStyle.Font=new Font(Font.FontFamily,12,FontStyle.Bold);grid.ColumnHeadersDefaultCellStyle.Font=new Font(Font.FontFamily,12,FontStyle.Bold);
            AddColumn("★", "favorite",32);AddColumn("类型", "type",40);AddColumn("EAC", "eac",38);AddColumn("密码", "password",38);
            AddColumn("服务器名称", "name",270);AddColumn("世界", "map",145);AddColumn("人数", "players",85);AddColumn("延迟", "ping",65);
            grid.Columns[4].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;grid.Columns[4].MinimumWidth=170;grid.AdvancedColumnHeadersBorderStyle.All=DataGridViewAdvancedCellBorderStyle.Single;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
            grid.Columns[0].HeaderCell.ToolTipText="收藏；点击星号收藏或取消收藏";
            grid.Columns[1].HeaderCell.ToolTipText="服务器类型：PC 专用服务器 / 主机联机";
            grid.Columns[2].HeaderCell.ToolTipText="EAC 反作弊：亮盾开启，暗盾关闭或未知";
            grid.Columns[3].HeaderCell.ToolTipText="亮锁：有密码；暗锁：无密码";
            grid.CellPainting+=PaintStatusIcon;
            grid.CellClick+=(sender,e)=>{if(e.RowIndex<0||e.RowIndex>=visible.Count)return;manual.Text=visible[e.RowIndex].Address;if(e.ColumnIndex==0){string address=visible[e.RowIndex].Address;if(!favorites.Add(address))favorites.Remove(address);File.WriteAllLines(favoritesFile,favorites);RefreshRows();}UpdateDetails();};
            grid.SelectionChanged+=(sender,e)=>{if(!refreshing)UpdateDetails();};
            grid.VirtualMode=true;
            grid.CellValueNeeded+=(sender,e)=>{
                if(e.RowIndex<0||e.RowIndex>=visible.Count)return;
                var row=visible[e.RowIndex];
                switch(e.ColumnIndex){case 0:e.Value=favorites.Contains(row.Address)?"★":"☆";break;case 1:e.Value=row.Dedicated.HasValue?(row.Dedicated.Value?"PC":"主"):"?";break;case 2:e.Value="";break;case 3:e.Value="";break;case 4:e.Value=row.Name;break;case 5:e.Value=row.Map;break;case 6:e.Value=row.Players+" / "+row.Capacity;break;case 7:e.Value=row.Ping<0?"未知":row.Ping.ToString();break;}
            };
            grid.ColumnHeaderMouseClick+=(sender,e)=>{if(e.ColumnIndex<0)return;pageIndex=0;if(sortColumn==e.ColumnIndex)sortDescending=!sortDescending;else{sortColumn=e.ColumnIndex;sortDescending=false;}RefreshRows();};
            var listPanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=1,Margin=Padding.Empty};
            listPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            regionTabs.SizeChanged+=(sender,e)=>FitNavigation(regionTabs);
            regionTabs.Margin=new Padding(3,0,3,0);regionTabs.Dock=DockStyle.Fill;regionTabs.WrapContents=false;regionTabs.AutoScroll=false;
            for(int i=0;i<ServerDetails.RegionIds.Length;i++){
                string id=ServerDetails.RegionIds[i];var tab=Button(ServerDetails.RegionNames[i],()=>{selectedRegion=id;pageIndex=0;RefreshRows();});
                tab.Font=new Font(Font.FontFamily,9,FontStyle.Bold);tab.AutoSize=false;tab.Height=34;tab.TextAlign=ContentAlignment.MiddleCenter;tab.Width=TextRenderer.MeasureText(tab.Text,tab.Font).Width+20;tab.Tag=id;tab.Padding=new Padding(2,0,2,0);tab.Margin=new Padding(0,2,4,0);regionTabs.Controls.Add(tab);
            }
                        var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Vertical,SplitterWidth=12};
            var leftList=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
            leftList.RowStyles.Add(new RowStyle(SizeType.Absolute,42));leftList.RowStyles.Add(new RowStyle(SizeType.Absolute,54));leftList.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            leftList.Controls.Add(regionTabs,0,0);leftList.Controls.Add(BuildStatisticsBar(),0,1);leftList.Controls.Add(grid,0,2);split.Panel1.Controls.Add(leftList);
            var rightInfo=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};
            rightInfo.RowStyles.Add(new RowStyle(SizeType.Absolute,42));rightInfo.RowStyles.Add(new RowStyle(SizeType.Percent,100));rightInfo.Controls.Add(banner,0,0);rightInfo.Controls.Add(BuildDetailsPanel(),0,1);split.Panel2.Controls.Add(rightInfo);
            split.Size=new Size(1400,500);split.SplitterDistance=850;split.Panel1MinSize=730;split.Panel2MinSize=330;
            listPanel.Controls.Add(split,0,0);root.Controls.Add(listPanel,0,3);
            var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(0,12,0,0)};
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            var leftActions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};leftActions.Controls.Add(search);leftActions.Controls.Add(stop);
            var centerActions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            manual.Width=185;manual.BackColor=GameTheme.Background;manual.ForeColor=ink;manual.Margin=new Padding(4,2,8,0);
            centerActions.Controls.Add(GameTheme.WrapInput(manual,42));centerActions.Controls.Add(Button("连接至 IP…",()=>ConnectAddress(manual.Text)));centerActions.Controls.Add(Button("复制地址",()=>CopySelected()));
            var rightActions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,FlowDirection=FlowDirection.RightToLeft};rightActions.Controls.Add(Button("连接服务器",()=>ConnectSelected()));
            actions.Controls.Add(leftActions,0,0);actions.Controls.Add(centerActions,1,0);actions.Controls.Add(rightActions,2,0);
            root.Controls.Add(actions,0,4);
            status.Dock=DockStyle.Fill;status.ForeColor=GameTheme.Foreground;status.Text=phase;root.Controls.Add(status,0,5);
            var note=new Label {Dock=DockStyle.Fill,ForeColor=GameTheme.Disabled,TextAlign=ContentAlignment.BottomLeft,Margin=new Padding(3,0,3,0),Font=new Font(Font.FontFamily,11),Text="©️2026 安安ANAN　｜　联系方式 QQ：383125283"};
            root.Controls.Add(note,0,6);
            ((PagedServerGrid)grid).PageScroll=direction=>{int target=Math.Max(0,Math.Min(pageCount-1,pageIndex+direction));if(target!=pageIndex){pageIndex=target;RefreshRows();if(grid.RowCount>0)grid.FirstDisplayedScrollingRowIndex=direction>0?0:grid.RowCount-1;}};
            filter.TextChanged+=(s,e)=>{pageIndex=0;RefreshRows();};populated.CheckedChanged+=(s,e)=>{pageIndex=0;RefreshRows();};unlocked.CheckedChanged+=(s,e)=>{pageIndex=0;RefreshRows();};
            grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex!=0)ConnectSelected();};
            FitNavigation(regionTabs);FitNavigation(tabs);
            timer.Interval=25;timer.Tick+=(s,e)=>Tick();
            FormClosed+=(s,e)=>{timer.Stop();Release();};
            if(preview){phase="界面预览（无演示服务器数据）";status.Text=phase;}
            else {timer.Start();}
            LoadAppearance();
        }
        private Button Button(string text,Action action)
        {
            var button=text=="停止"?(Button)new RedStopButton():text=="新搜索"?(Button)new SearchStateButton():new Button();
            button.Text=text;button.AutoSize=true;button.Height=42;button.Padding=new Padding(14,3,14,3);button.Font=new Font(Font.FontFamily,12,FontStyle.Bold);button.BackColor=GameTheme.Background;button.ForeColor=text=="停止"?GameTheme.Red:ink;button.FlatStyle=FlatStyle.Flat;button.Margin=new Padding(0,2,8,0);
            button.FlatAppearance.BorderColor=GameTheme.Foreground;button.FlatAppearance.MouseOverBackColor=GameTheme.Hover;button.FlatAppearance.MouseDownBackColor=GameTheme.Selected;button.Click+=(s,e)=>action();return button;
        }
        private static void FitNavigation(FlowLayoutPanel strip)
        {
            if(strip.Controls.Count==0||strip.ClientSize.Width<=0)return;
            int gap=4,count=strip.Controls.Count,total=Math.Max(count,strip.ClientSize.Width-strip.Padding.Horizontal-gap*(count-1));
            strip.SuspendLayout();
            for(int i=0;i<count;i++){var button=(Button)strip.Controls[i];button.AutoSize=false;button.Padding=Padding.Empty;button.Margin=new Padding(0,2,i<count-1?gap:0,2);button.Width=total/count+(i<total%count?1:0);}
            strip.ResumeLayout();
        }
        private void AddColumn(string title,string name,int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn {HeaderText=title,Name=name,Width=width,SortMode=DataGridViewColumnSortMode.Programmatic,Resizable=DataGridViewTriState.False});
        }
        private void StartSearch()
        {
            if(connecting)return;
            Release();pageIndex=0;detailsQueued.Clear();priorityQueued.Clear();servers.Clear();RefreshRows();started=DateTime.UtcNow;searching=true;
            search.Enabled=false;stop.Enabled=true;phase="正在查询，Steam 可能暂时显示七日杀运行中…";UpdateStatus();
            int current=generation;
            try
            {
                worker=new Process{StartInfo=new ProcessStartInfo(Application.ExecutablePath,"--worker"){
                    UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
                    WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,StandardOutputEncoding=System.Text.Encoding.UTF8}};
                worker.OutputDataReceived+=(sender,e)=>{if(e.Data!=null)messages.Enqueue(Tuple.Create(current,e.Data));};
                worker.ErrorDataReceived+=(sender,e)=>{};
                worker.Start();worker.BeginOutputReadLine();worker.BeginErrorReadLine();
            }
            catch(Exception ex){StopSearch("查询启动失败："+ex.Message);}
        }
        private void StopSearch(string message)
        {
            searching=false;Release();search.Enabled=true;stop.Enabled=false;phase=message;RefreshRows();
        }
        private void Release()
        {
            generation++;detailsCancel.Cancel();detailsCancel.Dispose();detailsCancel=new System.Threading.CancellationTokenSource();
            if(worker!=null)
            {
                try{if(!worker.HasExited){worker.Kill();worker.WaitForExit(3000);}}catch(InvalidOperationException){}
                worker.Dispose();worker=null;
            }
        }
        internal static bool HasResponse(ServerRow row){return row.Ping>=0&&row.Ping<int.MaxValue;}
        internal static ServerRow MergeServerRecords(ServerRow old,ServerRow incoming){
            var winner=HasResponse(incoming)&&(!HasResponse(old)||incoming.Ping<old.Ping)?incoming:old;
            var other=ReferenceEquals(winner,old)?incoming:old;
            ServerDetails.MergeRegions(old,incoming);winner.AdvertisedRegions=incoming.AdvertisedRegions;
            winner.IsFriend|=other.IsFriend;winner.IsHistory|=other.IsHistory;winner.IsLan|=other.IsLan;
            if(ServerDetails.IsDamagedText(winner.Name)&&!ServerDetails.IsDamagedText(other.Name))winner.Name=other.Name;if(ServerDetails.IsDamagedText(winner.Map)&&!ServerDetails.IsDamagedText(other.Map))winner.Map=other.Map;
            if(string.IsNullOrEmpty(winner.Version)||(!winner.Version.Contains("(b")&&!string.IsNullOrEmpty(other.Version)&&other.Version.Contains("(b")))winner.Version=other.Version;
            foreach(var rule in other.Rules)if(!winner.Rules.ContainsKey(rule.Key))winner.Rules[rule.Key]=rule.Value;
            if(string.IsNullOrEmpty(winner.Description))winner.Description=other.Description;if(string.IsNullOrEmpty(winner.Language))winner.Language=other.Language;
            if(!winner.WorldSize.HasValue)winner.WorldSize=other.WorldSize;if(!winner.GameTime.HasValue)winner.GameTime=other.GameTime;
            if(!winner.Eac.HasValue)winner.Eac=other.Eac;if(!winner.Crossplay.HasValue)winner.Crossplay=other.Crossplay;if(!winner.Dedicated.HasValue)winner.Dedicated=other.Dedicated;
            ServerDetails.ApplyRegion(winner);return winner;
        }
        private void Tick()
        {
            Tuple<int,string> message;
            while(messages.TryDequeue(out message))
            {
                if(message.Item1!=generation)continue;
                string line=message.Item2;
                if(line.StartsWith("SERVER "))
                {
                    try{var row=json.Deserialize<ServerRow>(line.Substring(7));row.Address=Connection.Normalize(row.Address);ServerRow old;if(!HasResponse(row))row.Ping=-1;if(servers.TryGetValue(row.Address,out old))row=MergeServerRecords(old,row);servers[row.Address]=row;dirty=true;QueueDetails(row);}
                    catch(Exception ex){phase="收到无效条目："+ex.Message;}
                }
                else if(line.StartsWith("DETAIL ")){var row=json.Deserialize<ServerRow>(line.Substring(7));ServerRow existing;if(servers.TryGetValue(row.Address,out existing)){ServerDetails.MergeRegions(existing,row);existing.AdvertisedRegions=row.AdvertisedRegions;existing.Region=row.Region;if(!existing.Version.Contains("(b")||row.Version.Contains("(b"))existing.Version=row.Version;CopyDetails(row,existing);ServerDetails.ApplyQueriedNames(row,existing);if(HasResponse(row)&&(!HasResponse(existing)||row.Ping<existing.Ping))existing.Ping=row.Ping;dirty=true;}}
                else if(line.StartsWith("STATUS "))phase=line.Substring(7);
                else if(line.StartsWith("ERROR "))phase=line.Substring(6);
                else if(line.StartsWith("DONE "))phase=line.Substring(5);
            }
            if(searching&&worker!=null&&worker.HasExited)
            {
                // The next timer tick drains trailing output before finalizing.
                worker.WaitForExit();worker.Dispose();worker=null;
                searching=false;search.Enabled=true;stop.Enabled=false;dirty=true;
                if(phase.StartsWith("正在查询"))phase="查询进程已退出。";
            }
            if(searching&&DateTime.UtcNow-started>TimeSpan.FromMinutes(10))StopSearch("查询超时，已释放 Steam 会话并保留结果。");
            if(DateTime.UtcNow-lastRender>TimeSpan.FromMilliseconds(500)){lastRender=DateTime.UtcNow;if(dirty){dirty=false;RefreshRows();}else if(searching)UpdateStatus();}
        }
        private void UpdateStatus()
        {
            status.ForeColor=phase.StartsWith("无法连接 Steam",StringComparison.Ordinal)?GameTheme.Red:GameTheme.Foreground;
            search.Text=searching?"正在搜索":"新搜索";((SearchStateButton)search).Searching=searching;search.Invalidate();
            status.Text=phase+"  去重后 "+servers.Count+" 台 / 本页显示 "+grid.Rows.Count+" 台 / 未取得地区 "+servers.Values.Count(r=>r.Region=="Unknown")+" 台"+(searching?"  ·  "+(int)(DateTime.UtcNow-started).TotalSeconds+" 秒":"");ApplyLocalization();
        }
        private void RefreshRows()
        {
            string selected=SelectedAddress();int scroll=grid.FirstDisplayedScrollingRowIndex;
            string text=filter.Text.Trim();IEnumerable<ServerRow> rows=servers.Values;
            if(text.Length>0)rows=rows.Where(r=>r.Name.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0||r.Address.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0);
            if(populated.Checked)rows=rows.Where(r=>r.Players>0);if(unlocked.Checked)rows=rows.Where(r=>!r.Password);
            if(selectedRegion!="All")rows=rows.Where(r=>ServerDetails.MatchesRegion(r,selectedRegion));
            var categoryBase=rows.ToList();UpdateStatistics(categoryBase);rows=categoryBase.Where(MatchesCategory);
            Func<ServerRow,IComparable> key;
            switch(grid.Columns[sortColumn].Name){case "favorite":key=r=>favorites.Contains(r.Address);break;case "type":key=r=>r.Dedicated.HasValue?(r.Dedicated.Value?1:0):-1;break;case "eac":key=r=>r.Eac.HasValue?(r.Eac.Value?1:0):-1;break;case "password":key=r=>r.Password;break;case "name":key=r=>r.Name;break;case "map":key=r=>r.Map;break;case "players":key=r=>r.Players;break;default:key=r=>r.Ping<0?int.MaxValue:r.Ping;break;}
            rows=sortDescending?rows.OrderByDescending(key).ThenBy(r=>r.Address):rows.OrderBy(key).ThenBy(r=>r.Address);
            foreach(DataGridViewColumn column in grid.Columns)column.HeaderCell.SortGlyphDirection=column.Index==sortColumn?(sortDescending?SortOrder.Descending:SortOrder.Ascending):SortOrder.None;
            foreach(Button tab in regionTabs.Controls){tab.BackColor=(string)tab.Tag==selectedRegion?GameTheme.Selected:GameTheme.Background;tab.ForeColor=(string)tab.Tag==selectedRegion?Color.White:ink;}
            var ordered=rows.ToList();pageCount=Math.Max(1,(ordered.Count+PageSize-1)/PageSize);pageIndex=Math.Min(pageIndex,pageCount-1);UpdatePager();refreshing=true;grid.SuspendLayout();visible=ordered.Skip(pageIndex*PageSize).Take(PageSize).ToList();grid.RowCount=visible.Count;int select=visible.FindIndex(r=>r.Address==selected);
            grid.ClearSelection();if(select>=0){grid.Rows[select].Selected=true;grid.CurrentCell=grid.Rows[select].Cells[4];}
            if(scroll>=0&&scroll<grid.Rows.Count)grid.FirstDisplayedScrollingRowIndex=scroll;
            grid.ResumeLayout();grid.Invalidate();refreshing=false;UpdateDetails();UpdateStatus();
        }
        private static string VersionKey(string text){return System.Text.RegularExpressions.Regex.Replace(text??"",@"\d+",m=>m.Value.PadLeft(8,'0'));}
        private async void QueueDetails(ServerRow row,bool priority=false)
        {
            if(!(priority?priorityQueued:detailsQueued).Add(row.Address))return;var gate=priority?priorityGate:detailsGate;
            int current=generation;var token=detailsCancel.Token;bool entered=false;
            // Copy so background network operations never mutate the UI's row objects.
            var copy=json.Deserialize<ServerRow>(json.Serialize(row));
            try{
                await gate.WaitAsync(token);entered=true;
                await System.Threading.Tasks.Task.Run(()=>ServerDetails.Fetch(copy,token),token);
                if(!token.IsCancellationRequested)messages.Enqueue(Tuple.Create(current,"DETAIL "+json.Serialize(copy)));
            }catch(System.OperationCanceledException){}catch(System.Net.Sockets.SocketException){}catch(IOException){}catch(AggregateException){}catch(ObjectDisposedException){}
            finally{if(entered)gate.Release();}
        }
        private string SelectedAddress(){return grid.SelectedRows.Count==0||grid.SelectedRows[0].Index>=visible.Count?null:visible[grid.SelectedRows[0].Index].Address;}
        private void ConnectSelected(){var address=SelectedAddress();if(address==null){MessageBox.Show(this,Translate("请先选中一个服务器。"),Translate("连接服务器"));return;}ConnectAddress(address);}
        private void CopySelected(){var address=SelectedAddress();if(address==null)return;try{Clipboard.SetText(address);phase="已复制："+address;UpdateStatus();}catch(Exception ex){MessageBox.Show(this,Translate(ex.Message),Translate("复制失败"));}}
        private async void ConnectAddress(string address)
        {
            if(connecting)return;
            try
            {
                string normalized=Connection.Normalize(address);
                connecting=true;StopSearch("正在释放查询会话，稍后发起连接…");search.Enabled=false;
                await System.Threading.Tasks.Task.Delay(2500);
                if(IsDisposed)return;
                bool gameRunning=Process.GetProcessesByName("7DaysToDie").Length>0||Process.GetProcessesByName("7DaysToDie_EAC").Length>0;
                string uri=gameRunning?Connection.UriFor(normalized):Connection.LaunchUriFor(normalized);
                Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});
                phase=gameRunning?"已向运行中的游戏发送连接请求："+normalized:"已请求 Steam 启动七日杀并加入："+normalized;
                UpdateStatus();
            }
            catch(Exception ex){MessageBox.Show(this,Translate(ex.Message),Translate("无法发起连接"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
            finally{connecting=false;if(!IsDisposed)search.Enabled=true;}
        }
    }
    static class QueryWorker
    {
        private static readonly List<ISteamMatchmakingServerListResponse> responses=new List<ISteamMatchmakingServerListResponse>();
        internal static void Run()
        {
            var output=new StreamWriter(Console.OpenStandardOutput(),new System.Text.UTF8Encoding(false)){AutoFlush=true};Console.SetOut(output);
            bool initialized=false;var requests=new List<HServerListRequest>();EosDirectory eos=null;
            try
            {
                initialized=SteamAPI.Init();if(!initialized){Console.WriteLine("ERROR 无法连接 Steam，请先登录拥有七日杀的 Steam 账号。");return;}
                int completed=0,count=0;var json=new JavaScriptSerializer();var app=new AppId_t(251570);var filters=new MatchMakingKeyValuePair_t[0];
                for(int source=0;source<5;source++){
                    int type=source;
                    var response=new ISteamMatchmakingServerListResponse((h,i)=>{
                        try{var raw=SteamMatchmakingServers.GetServerDetails(h,i);if(raw==null||raw.m_nAppID!=251570||raw.m_NetAdr.GetConnectionPort()==0)return;
                            var row=ServerRow.FromSteam(raw);row.IsFriend=type==1;row.IsLan=type==2;row.IsHistory=type==3||type==4;row.Address=Connection.Normalize(row.Address);Console.WriteLine("SERVER "+json.Serialize(row));count++;}
                        catch(Exception){}
                    },(h,i)=>{},(h,r)=>{completed++;});responses.Add(response);
                    HServerListRequest request;
                    switch(source){case 1:request=SteamMatchmakingServers.RequestFriendsServerList(app,filters,0,response);break;case 2:request=SteamMatchmakingServers.RequestLANServerList(app,response);break;case 3:request=SteamMatchmakingServers.RequestHistoryServerList(app,filters,0,response);break;case 4:request=SteamMatchmakingServers.RequestFavoritesServerList(app,filters,0,response);break;default:request=SteamMatchmakingServers.RequestInternetServerList(app,filters,0,response);break;}
                    if(request==HServerListRequest.Invalid)completed++;else requests.Add(request);
                }
                eos=new EosDirectory(row=>{Console.WriteLine("SERVER "+json.Serialize(row));count++;},message=>Console.WriteLine("STATUS "+message));
                eos.Begin();DateTime steamUntil=DateTime.UtcNow.AddSeconds(45),until=DateTime.UtcNow.AddMinutes(9);bool steamReleased=false;
                while(DateTime.UtcNow<until){
                    SteamAPI.RunCallbacks();eos.Tick();
                    if(!steamReleased&&(completed>=5||DateTime.UtcNow>=steamUntil)){foreach(var request in requests){SteamMatchmakingServers.CancelQuery(request);SteamMatchmakingServers.ReleaseRequest(request);}requests.Clear();steamReleased=true;}
                    if(steamReleased&&eos.Done)break;
                    System.Threading.Thread.Sleep(25);
                }
                string ending=eos.Done?"搜索完成":"本轮查询达到时间上限，保留结果";
                Console.WriteLine("DONE "+ending+"，Steam＋EOS 响应 "+count+" 条；EOS "+eos.Queries+" 批 / "+eos.Records+" 条"+(eos.Error==null?"":"；"+eos.Error)+(eos.Saturated>0?"；部分分组仍达上限，结果可能不全":""));
            }
            catch(Exception ex){Console.WriteLine("ERROR "+ex.Message);}
            finally{if(eos!=null)eos.Dispose();foreach(var request in requests){SteamMatchmakingServers.CancelQuery(request);SteamMatchmakingServers.ReleaseRequest(request);}if(initialized)SteamAPI.Shutdown();responses.Clear();}
        }
    }
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            if(args.Length>0&&args[0]=="--worker"){QueryWorker.Run();return;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0&&args[0]=="--preview")
            {
                using(var form=new BrowserForm(true)){form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}form.Close();}return;
            }
            using(var form=new BrowserForm(false))
            {
                System.Windows.Forms.Timer smoke=null;
                if(args.Length>0&&args[0]=="--smoke")
                {
                    form.Shown+=(s,e)=>typeof(BrowserForm).GetMethod("StartSearch",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(form,null);
                    smoke=new System.Windows.Forms.Timer{Interval=25000};
                    smoke.Tick+=(s,e)=>{
                        smoke.Stop();
                        var list=(List<ServerRow>)typeof(BrowserForm).GetField("visible",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(form);
                        var view=(DataGridView)typeof(BrowserForm).GetField("grid",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(form);
                        int chosen=list.FindIndex(r=>r.Eac.HasValue&&!string.IsNullOrWhiteSpace(r.Description));
                        if(chosen<0)chosen=list.FindIndex(r=>r.Eac.HasValue);
                        if(chosen>=0){view.CurrentCell=view.Rows[chosen].Cells[4];view.Rows[chosen].Selected=true;typeof(BrowserForm).GetMethod("UpdateDetails",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(form,null);}
                        using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"live-preview.png"));}
                        var field=typeof(BrowserForm).GetField("servers",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                        var data=(Dictionary<string,ServerRow>)field.GetValue(form);
                        File.WriteAllText("smoke-result.txt","Unique endpoints="+data.Count+Environment.NewLine+string.Join(Environment.NewLine,data.Values.Take(3).Select(r=>r.Name+" "+r.Address)));
                        form.Close();
                    };smoke.Start();
                }
                Application.Run(form);if(smoke!=null)smoke.Dispose();
            }
        }
    }
}




























