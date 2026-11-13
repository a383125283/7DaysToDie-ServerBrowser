using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace SevenDaysBrowser {
 public static class ServerDetails {
  public static readonly string[] RegionIds={"All","NorthAmericaEast","NorthAmericaWest","CentralAmerica","SouthAmerica","Europe","Russia","Asia","MiddleEast","Africa","Oceania","Unknown"};
  public static readonly string[] RegionNames={"全部","北美东部","北美西部","中美洲","南美洲","欧洲","俄罗斯","亚洲","中东","非洲","大洋洲","未知地区"};
  public static string RegionName(string id){int i=Array.FindIndex(RegionIds,r=>string.Equals(r,id,StringComparison.OrdinalIgnoreCase));return i<0?"未知地区":RegionNames[i];}
  internal static void RegisterRegion(ServerRow row,string region){if(region!="Unknown"&&region!="All"&&!row.AdvertisedRegions.Contains(region))row.AdvertisedRegions.Add(region);}
  internal static void MergeRegions(ServerRow source,ServerRow target){
   var incoming=target.AdvertisedRegions;target.AdvertisedRegions=new List<string>();
   foreach(string region in source.AdvertisedRegions)RegisterRegion(target,region);
   if(source.AdvertisedRegions.Count==0)RegisterRegion(target,source.Region);
   foreach(string region in incoming)RegisterRegion(target,region);
   if(incoming.Count==0)RegisterRegion(target,target.Region);
   if(string.IsNullOrWhiteSpace(target.Language))target.Language=source.Language;
   ApplyRegion(target);
  }
  internal static void ApplyRegion(ServerRow row){
   if(row.AdvertisedRegions.Count==0)return;
   row.Region=row.AdvertisedRegions[0];
   if(row.AdvertisedRegions.Count<2)return;
   string language=(row.Language??"").Trim().Replace('_','-').ToLowerInvariant();
   if(language=="zh"||language.StartsWith("zh-")){row.Region="Asia";return;}
   switch(language){
    case "chinese":case "schinese":case "tchinese":case "simplified chinese":case "traditional chinese":case "中文":case "简体中文":case "繁体中文":case "繁體中文":
    case "japanese":case "日语":case "日本語":case "ja":case "korean":case "韩语":case "한국어":case "ko":row.Region="Asia";break;
    case "russian":case "русский":case "俄语":case "ru":row.Region="Russia";break;
    case "german":case "deutsch":case "de":case "french":case "français":case "fr":case "italian":case "it":case "polish":case "pl":case "czech":case "cs":row.Region="Europe";break;
   }
  }
  internal static bool MatchesRegion(ServerRow row,string region){return region=="All"||row.Region==region;}
  internal static bool IsDamagedText(string text){return string.IsNullOrEmpty(text)||text.IndexOf('\uFFFD')>=0;}
  internal static void ApplyQueriedNames(ServerRow source,ServerRow target){
   string name;if(source.Rules.TryGetValue("GameHost",out name)&&!IsDamagedText(name))target.Name=name;
   string map;if(source.Rules.TryGetValue("LevelName",out map)&&!IsDamagedText(map))target.Map=map;
  }
  public static string SteamVersion(int code){if(code<10000||code>=20000)return "未知（"+code+"）";int major=code/100%100,minor=code%100;return major>=3?"V"+major+"."+minor/10+"."+minor%10:"V"+major+"."+minor;}
  public static string FullVersion(string value){var p=value.Split('.');int major,minor,build;if(p.Length!=4||!int.TryParse(p[1],out major)||!int.TryParse(p[2],out minor)||!int.TryParse(p[3],out build))return value;
   string prefix=p[0]=="Alpha"?"A":p[0];return prefix+major+"."+(p[0]=="V"&&major>=3?minor/10+"."+minor%10:minor.ToString())+" (b"+build+")";}
  public static void Parse(string text,ServerRow row){foreach(var part in text.Split(';')){int colon=part.IndexOf(':');if(colon<0)continue;string key=part.Substring(0,colon).Trim(),value=part.Substring(colon+1).Trim();row.Rules[key]=value;
   if(key=="Region"){int i=Array.FindIndex(RegionIds,r=>string.Equals(r,value,StringComparison.OrdinalIgnoreCase));row.Region=i>0&&i<RegionIds.Length-1?RegionIds[i]:"Unknown";RegisterRegion(row,row.Region);}
   else if(key=="GameHost"&&!IsDamagedText(value))row.Name=value;
   else if(key=="LevelName"&&!IsDamagedText(value))row.Map=value;
   else if(key=="ServerVersion")row.Version=FullVersion(value);
   else if(key=="ServerDescription")row.Description=value;
   else if(key=="Language")row.Language=value;
   else if(key=="WorldSize"){int size;if(int.TryParse(value,out size))row.WorldSize=size;}
   else if(key=="CurrentServerTime"){int time;if(int.TryParse(value,out time)&&time>=0)row.GameTime=time;}
   else if(key=="EACEnabled"||key=="AllowCrossplay"||key=="IsDedicated"){bool flag;if(bool.TryParse(value,out flag)){if(key=="EACEnabled")row.Eac=flag;else if(key=="AllowCrossplay")row.Crossplay=flag;else row.Dedicated=flag;}}
  }ApplyRegion(row);}
  static void ReadExact(Stream stream,byte[] bytes,int offset,int count){while(count>0){int n=stream.Read(bytes,offset,count);if(n<=0)throw new EndOfStreamException();offset+=n;count-=n;}}
  public static void Fetch(ServerRow row,CancellationToken token){
   token.ThrowIfCancellationRequested();string endpoint=Connection.Normalize(row.Address);int colon=endpoint.LastIndexOf(':');var latency=System.Diagnostics.Stopwatch.StartNew();
   using(var client=new TcpClient())using(token.Register(()=>client.Close())){
    var task=client.ConnectAsync(endpoint.Substring(0,colon),int.Parse(endpoint.Substring(colon+1)));if(!task.Wait(2000))return;task.GetAwaiter().GetResult();
    client.ReceiveTimeout=1500;using(var stream=client.GetStream()){
     int first=stream.ReadByte();if(first<0)return;int size;
     if(first>=48&&first<=57){var tail=new byte[6];ReadExact(stream,tail,0,6);if(tail[4]!=13||tail[5]!=10)throw new InvalidDataException();string length=((char)first)+Encoding.ASCII.GetString(tail,0,4);if(!int.TryParse(length,out size))throw new InvalidDataException();}
     else{int second=stream.ReadByte();if(second<0)return;size=(first<<8)|second;}
     if(size<1||size>32768)return;var bytes=new byte[size];ReadExact(stream,bytes,0,size);token.ThrowIfCancellationRequested();Parse(Encoding.UTF8.GetString(bytes),row);row.Ping=(int)Math.Min(int.MaxValue-1,latency.ElapsedMilliseconds);
    }
   }
  }
 }
}


