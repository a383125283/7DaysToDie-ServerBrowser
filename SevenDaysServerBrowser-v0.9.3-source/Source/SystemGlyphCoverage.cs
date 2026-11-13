using System;using System.Drawing;using System.Runtime.InteropServices;using System.Collections.Generic;
namespace SevenDaysBrowser {
 internal static class SystemGlyphCoverage {
  private static readonly Dictionary<string,byte[]> tables=new Dictionary<string,byte[]>();
  [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
  [DllImport("gdi32.dll")]private static extern IntPtr SelectObject(IntPtr hdc,IntPtr obj);
  [DllImport("gdi32.dll")]private static extern bool DeleteDC(IntPtr hdc);
  [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll")]private static extern uint GetFontData(IntPtr hdc,uint table,uint offset,byte[] buffer,uint length);
  private static int U16(byte[] b,int p){return b[p]*256+b[p+1];}
  private static uint U32(byte[] b,int p){return (uint)b[p]<<24|(uint)b[p+1]<<16|(uint)b[p+2]<<8|b[p+3];}
  private static byte[] Load(string name){byte[] data;if(tables.TryGetValue(name,out data))return data;data=new byte[0];using(var f=new Font(name,18))if(string.Equals(name,f.Name,StringComparison.OrdinalIgnoreCase)){
   IntPtr dc=CreateCompatibleDC(IntPtr.Zero),font=f.ToHfont(),old=SelectObject(dc,font);
   try{uint size=GetFontData(dc,0x70616d63,0,null,0);if(size!=uint.MaxValue&&size>4&&size<16*1024*1024){var bytes=new byte[size];if(GetFontData(dc,0x70616d63,0,bytes,size)==size)data=bytes;}}
   finally{SelectObject(dc,old);DeleteObject(font);DeleteDC(dc);}
  }tables[name]=data;return data;}
  private static bool HasGlyph(byte[] b,int cp){
   if(b.Length<4)return false;int n=U16(b,2);
   for(int i=0;i<n;i++){int rec=4+i*8;if(rec+8>b.Length)break;int platform=U16(b,rec),encoding=U16(b,rec+2);if(platform!=0&&(platform!=3||encoding!=1&&encoding!=10))continue;
    int p=(int)U32(b,rec+4);if(p<0||p+2>b.Length)continue;int format=U16(b,p);
    if(format==12&&p+16<=b.Length){int count=(int)U32(b,p+12),low=0,high=count-1;while(low<=high){int mid=low+(high-low)/2,pos=p+16+mid*12;if(pos<0||pos+12>b.Length)break;uint start=U32(b,pos),end=U32(b,pos+4);if(cp<start)high=mid-1;else if(cp>end)low=mid+1;else return U32(b,pos+8)+(uint)cp-start!=0;}}
    else if(format==4&&cp<=65535&&p+14<=b.Length){int segments=U16(b,p+6)/2,end=p+14,start=end+segments*2+2,delta=start+segments*2,range=delta+segments*2;if(range+segments*2>b.Length)continue;for(int j=0;j<segments;j++){int last=U16(b,end+j*2);if(cp>last)continue;int first=U16(b,start+j*2);if(cp<first)break;int d=U16(b,delta+j*2),r=U16(b,range+j*2);if(r==0)return (cp+d)%65536!=0;int at=range+j*2+r+2*(cp-first);return at>=0&&at+2<=b.Length&&U16(b,at)!=0&&(U16(b,at)+d)%65536!=0;}}
   }return false;
  }
  internal static bool Covers(string name,string text){var cmap=Load(name);for(int i=0;i<text.Length;i++){int cp=char.ConvertToUtf32(text,i);if(char.IsHighSurrogate(text[i]))i++;if(cp<32||cp==0x200d||cp>=0xfe00&&cp<=0xfe0f)continue;if(!HasGlyph(cmap,cp))return false;}return true;}
 }
}
