using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.Sessions;
using Steamworks;

namespace SevenDaysBrowser {
 internal sealed class EosDirectory:IDisposable {
  internal sealed class Query {
   internal bool Crossplay;
   internal int Min=-1,Max=int.MaxValue;
   internal string Region;
  }
  internal static readonly int[,] Ranges={{0,0},{1,8},{9,32},{33,128},{129,512},{513,2048},{2049,int.MaxValue}};
  private readonly Queue<Query> pending=new Queue<Query>();
  private readonly Action<ServerRow> found;
  private readonly Action<string> progress;
  private PlatformInterface platform;
  private ProductUserId user;
  private SessionSearch active;
  private DateTime deadline,nextQuery;
  private bool loginPending,initialized;
  private CallResult<EncryptedAppTicketResponse_t> ticketCallback;
  internal bool Done {get;private set;}
  internal int Queries,Records,Saturated;
  internal string Error;
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
  private static extern IntPtr LoadLibrary(string path);

  internal EosDirectory(Action<ServerRow> found,Action<string> progress){this.found=found;this.progress=progress;}
  internal void Begin(){
   try{
    string game=FindGame();string managed=Path.Combine(game,"7DaysToDie_Data","Managed");
    // Load SDK and configuration from the user's installed game. Never write credentials or tickets to disk/logs.
    if(LoadLibrary(Path.Combine(game,"7DaysToDie_Data","Plugins","x86_64","EOSSDK-Win64-Shipping.dll"))==IntPtr.Zero)throw new IOException("无法加载游戏的 EOS SDK");
    ResolveEventHandler resolver=(sender,args)=>{string file=Path.Combine(managed,new AssemblyName(args.Name).Name+".dll");return File.Exists(file)?Assembly.LoadFrom(file):null;};
    AppDomain.CurrentDomain.AssemblyResolve+=resolver;
    object creds;
    try{creds=Assembly.LoadFrom(Path.Combine(managed,"Assembly-CSharp.dll")).GetType("Platform.EOS.EosCreds",true).GetField("ClientCredentials",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).GetValue(null);}
    finally{AppDomain.CurrentDomain.AssemblyResolve-=resolver;}
    Func<string,string> value=name=>(string)creds.GetType().GetField(name).GetValue(creds);
    var init=new InitializeOptions{ProductName="7 Days To Die",ProductVersion="server-browser-0.8.1"};
    var result=PlatformInterface.Initialize(ref init);
    if(result!=Result.Success&&result!=Result.AlreadyConfigured)throw new InvalidOperationException("EOS 初始化："+result);
    initialized=true;
    var options=new WindowsOptions{ProductId=value("ProductId"),SandboxId=value("SandboxId"),DeploymentId=value("DeploymentId"),ClientCredentials=new ClientCredentials{ClientId=value("ClientId"),ClientSecret=value("ClientSecret")},IsServer=false,Flags=PlatformFlags.DisableOverlay|PlatformFlags.DisableSocialOverlay};
    platform=PlatformInterface.Create(ref options);
    if(platform==null)throw new InvalidOperationException("EOS 平台初始化失败");
    progress("正在登录 EOS 服务器目录…");loginPending=true;deadline=DateTime.UtcNow.AddSeconds(35);
    ticketCallback=CallResult<EncryptedAppTicketResponse_t>.Create((reply,ioFailure)=>{
     if(ioFailure||reply.m_eResult!=EResult.k_EResultOK){Fail("Steam 登录票据："+reply.m_eResult);return;}
     uint length;SteamUser.GetEncryptedAppTicket(null,0,out length);
     var bytes=new byte[length];uint read;
     if(length==0||!SteamUser.GetEncryptedAppTicket(bytes,bytes.Length,out read)){Fail("无法读取 Steam 登录票据");return;}
     string token=BitConverter.ToString(bytes,0,(int)read).Replace("-","");
     var login=new Epic.OnlineServices.Connect.LoginOptions{Credentials=new Epic.OnlineServices.Connect.Credentials{Type=ExternalCredentialType.SteamAppTicket,Token=token}};
     platform.GetConnectInterface().Login(ref login,null,(ref Epic.OnlineServices.Connect.LoginCallbackInfo info)=>{
      if(info.ResultCode==Result.Success){user=info.LocalUserId;loginPending=false;pending.Enqueue(new Query());pending.Enqueue(new Query{Crossplay=true});nextQuery=DateTime.UtcNow;progress("EOS 已登录，正在查询游戏服务器目录…");}
      else if(Common.IsOperationComplete(info.ResultCode))Fail(info.ResultCode==Result.InvalidUser?"请先用当前 Steam 账号正常启动游戏并登录在线服务，再重试":"EOS 登录："+info.ResultCode);
     });
    });
    ticketCallback.Set(SteamUser.RequestEncryptedAppTicket(null,0));
   }catch(Exception ex){Fail("EOS 无法启动："+ex.GetBaseException().Message);}
  }
  private static string FindGame(){
   string file=RuntimePaths.DataFile("game-path.txt");
   string game=File.Exists(file)?File.ReadAllText(file).Trim().Trim('"'):"";
   if(!Directory.Exists(Path.Combine(game,"7DaysToDie_Data"))){string path;SteamApps.GetAppInstallDir(new AppId_t(251570),out path,2048);game=path;}
   if(!Directory.Exists(Path.Combine(game,"7DaysToDie_Data")))throw new DirectoryNotFoundException("未找到七日杀，请在 game-path.txt 填写游戏目录");
   return game;
  }
  private void Fail(string error){Error=error;Done=true;progress(error+"；保留 Steam 查询结果。");}
  internal void Tick(){
   if(Done)return;
   if(platform!=null)platform.Tick();
   if(Done)return;
   if(loginPending){if(DateTime.UtcNow>deadline)Fail("EOS 登录超时");return;}
   if(active!=null){if(DateTime.UtcNow>deadline)Fail("EOS 查询超时，已保留返回结果");return;}
   if(DateTime.UtcNow<nextQuery)return;
   if(pending.Count==0){Done=true;return;}
   Search(pending.Dequeue());
  }
  private static void Set(SessionSearch search,string key,AttributeDataValue value,ComparisonOp op){
   var options=new SessionSearchSetParameterOptions{Parameter=new AttributeData{Key=key,Value=value},ComparisonOp=op};
   var result=search.SetParameter(ref options);if(result!=Result.Success)throw new InvalidOperationException("EOS 查询条件："+result);
  }
  private void Search(Query query){
   try{
    var options=new CreateSessionSearchOptions{MaxSearchResults=200};SessionSearch search;
    var result=platform.GetSessionsInterface().CreateSessionSearch(ref options,out search);
    if(result!=Result.Success)throw new InvalidOperationException("EOS 创建查询："+result);
    active=search;
    Set(search,SessionsInterface.SEARCH_BUCKET_ID,"<WeDontCare>",ComparisonOp.Equal);
    Set(search,"-BoolValues-",",AllowCrossplay="+(query.Crossplay?"1":"0")+",",ComparisonOp.Contains);
    if(query.Crossplay){Set(search,"-BoolValues-",",SanctionsIgnored=0,",ComparisonOp.Contains);Set(search,"MaxPlayers",(long?)8,ComparisonOp.Lessthanorequal);}
    else Set(search,"PlayGroup","~$#$~standalone",ComparisonOp.Contains);
    if(query.Min>=0){Set(search,"CurrentPlayers",(long?)query.Min,ComparisonOp.Greaterthanorequal);Set(search,"CurrentPlayers",(long?)query.Max,ComparisonOp.Lessthanorequal);}
    if(query.Region!=null)Set(search,"Region","~$#$~"+query.Region.ToLowerInvariant(),ComparisonOp.Contains);
    Queries++;deadline=DateTime.UtcNow.AddSeconds(30);
    progress("正在补查 EOS：第 "+Queries+" 批，待查 "+pending.Count+" 批，已返回 "+Records+" 条…");
    var find=new SessionSearchFindOptions{LocalUserId=user};
    search.Find(ref find,query,(ref SessionSearchFindCallbackInfo info)=>{
     if(Done)return;
     if(info.ResultCode!=Result.Success){Fail("EOS 查询失败："+info.ResultCode);return;}
     try{
      var countOptions=new SessionSearchGetSearchResultCountOptions();uint count=search.GetSearchResultCount(ref countOptions);
      for(uint i=0;i<count;i++){
       var copy=new SessionSearchCopySearchResultByIndexOptions{SessionIndex=i};SessionDetails details;
       if(search.CopySearchResultByIndex(ref copy,out details)!=Result.Success)continue;
       try{var row=Read(details);if(row!=null){found(row);Records++;}}finally{details.Release();}
      }
      foreach(var extra in Expand(query,count))pending.Enqueue(extra);
      if(count>=200&&query.Region!=null)Saturated++;
      search.Release();active=null;nextQuery=DateTime.UtcNow.AddMilliseconds(250);
     }catch(Exception ex){Fail("EOS 结果处理失败："+ex.Message);}
    });
   }catch(Exception ex){Fail(ex.Message);}
  }
  internal static IEnumerable<Query> Expand(Query query,uint count){
   if(count<200)yield break;
   if(query.Min<0){for(int i=0;i<Ranges.GetLength(0);i++)yield return new Query{Crossplay=query.Crossplay,Min=Ranges[i,0],Max=Ranges[i,1]};}
   else if(query.Region==null){foreach(string region in ServerDetails.RegionIds.Skip(1).Take(10))yield return new Query{Crossplay=query.Crossplay,Min=query.Min,Max=query.Max,Region=region};}
  }
  private static ServerRow Read(SessionDetails details){
   var options=new SessionDetailsCopyInfoOptions();SessionDetailsInfo? info;
   if(details.CopyInfo(ref options,out info)!=Result.Success||!info.HasValue)return null;
   var attributes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   var countOptions=new SessionDetailsGetSessionAttributeCountOptions();uint count=details.GetSessionAttributeCount(ref countOptions);
   for(uint i=0;i<count;i++){
    var copy=new SessionDetailsCopySessionAttributeByIndexOptions{AttrIndex=i};SessionDetailsAttribute? attr;
    if(details.CopySessionAttributeByIndex(ref copy,out attr)!=Result.Success||!attr.HasValue||!attr.Value.Data.HasValue)continue;
    var data=attr.Value.Data.Value;var value=data.Value;
    string key=data.Key,text=value.ValueType==AttributeType.String?(string)value.AsUtf8:value.ValueType==AttributeType.Int64?value.AsInt64.GetValueOrDefault().ToString(CultureInfo.InvariantCulture):value.ValueType==AttributeType.Boolean?value.AsBool.GetValueOrDefault().ToString():"";
    attributes[key]=text;
   }
   return Parse(attributes,info.Value.HostAddress,(int)info.Value.Settings.Value.NumPublicConnections,(int)(info.Value.Settings.Value.NumPublicConnections-info.Value.NumOpenPublicConnections));
  }
  internal static ServerRow Parse(Dictionary<string,string> attributes,string host,int capacity,int players){
   var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   foreach(var pair in attributes){string text=pair.Value??"";int marker=text.IndexOf("~$#$~",StringComparison.Ordinal);if(marker>=0)text=text.Substring(0,marker);values[pair.Key]=text=="##EMPTY##"?"":text;}
   string bools;if(values.TryGetValue("-BoolValues-",out bools))foreach(string item in bools.Split(',')){int equals=item.IndexOf('=');if(equals>0)values[item.Substring(0,equals)]=item.Substring(equals+1)=="1"?"True":"False";}
   Func<string,string,string> get=(key,fallback)=>{string value;return values.TryGetValue(key,out value)?value:fallback;};
   int port;if(!int.TryParse(get("Port",""),out port))return null;
   string address;try{address=Connection.Normalize(get("IP",host)+":"+port);}catch(ArgumentException){return null;}
   var row=new ServerRow{Address=address,Name=get("GameHost",get("GameName","")),Map=get("LevelName",""),Version=get("ServerVersion","未知"),Capacity=capacity,Players=players,Ping=-1};
   int n;if(int.TryParse(get("CurrentPlayers",""),out n))row.Players=n;if(int.TryParse(get("MaxPlayers",""),out n))row.Capacity=n;
   row.Password=get("IsPasswordProtected","False")=="True";
   // EOS normalizes attribute keys to uppercase; the game's TCP metadata uses enum casing.
   var canonical=("Region ServerVersion ServerDescription Language WorldSize CurrentServerTime EACEnabled AllowCrossplay IsDedicated "+
    "ModdedConfig StockSettings SandboxPreset SandboxCode GameDifficulty XPMultiplier DayNightLength DayLightLength MaxChunkAge BuildCreate MaxSpawnedZombies MaxSpawnedAnimals LootAbundance LootRespawnDays DropOnDeath DropOnQuit PlayerKillingMode LandClaimSize LandClaimExpiryTime LandClaimDecayMode LandClaimOnlineDurabilityModifier LandClaimOfflineDurabilityModifier LandClaimOfflineDelay BedrollExpiryTime PartySharedKillRange").Split(' ').ToDictionary(key=>key,StringComparer.OrdinalIgnoreCase);
   foreach(var pair in values){string key; if(!canonical.TryGetValue(pair.Key,out key))key=pair.Key;row.Rules[key]=pair.Value;ServerDetails.Parse(key+":"+pair.Value+";",row);}
   return row;
  }
  public void Dispose(){if(ticketCallback!=null)ticketCallback.Dispose();if(active!=null){active.Release();active=null;}if(platform!=null){platform.Release();platform=null;}if(initialized){PlatformInterface.Shutdown();initialized=false;}}
 }
}
