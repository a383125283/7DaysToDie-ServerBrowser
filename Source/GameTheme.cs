using System.Drawing;
using System.Windows.Forms;
namespace SevenDaysBrowser {
 internal sealed class PagedServerGrid:DataGridView {
  internal System.Action<int> PageScroll;
  private int wheelDelta;
  protected override void OnMouseWheel(MouseEventArgs e){
   if(e.Delta==0||RowCount==0){base.OnMouseWheel(e);return;}
   int first=FirstDisplayedScrollingRowIndex;
   bool edge=e.Delta>0?first<=0:first>=0&&first+DisplayedRowCount(false)>=RowCount;
   if(!edge){wheelDelta=0;base.OnMouseWheel(e);return;}
   if(System.Math.Sign(wheelDelta)!=System.Math.Sign(e.Delta))wheelDelta=0;
   wheelDelta+=e.Delta;
   if(System.Math.Abs(wheelDelta)>=120){int direction=wheelDelta<0?1:-1;wheelDelta=0;if(PageScroll!=null)PageScroll(direction);}
  }
 }
 internal static class GameIcons {
  private static readonly System.Collections.Generic.Dictionary<string,Image> images=new System.Collections.Generic.Dictionary<string,Image>();
  internal static void Draw(Graphics graphics,string name,Rectangle bounds,Color tint){
   Image image;if(!images.TryGetValue(name,out image)){image=Image.FromFile(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"Assets",name+".png"));images[name]=image;}
   using(var attributes=new System.Drawing.Imaging.ImageAttributes()){
    var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix00=tint.R/255f;matrix.Matrix11=tint.G/255f;matrix.Matrix22=tint.B/255f;
    if(GameTheme.Light&&name=="ui_platform_pc"&&tint.GetBrightness()<0.9f){matrix.Matrix00=matrix.Matrix11=matrix.Matrix22=-1;matrix.Matrix40=matrix.Matrix41=matrix.Matrix42=1;}
    attributes.SetColorMatrix(matrix);
    graphics.DrawImage(image,bounds,0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
   }
  }
 }
 // Source: Data/Config/XUi_Common/styles.xml; XUi_Menu/templates.xml and windows.xml.
 internal static class GameTheme {
  internal static readonly Color Red=Color.FromArgb(228,18,21);
  internal static bool Light;
  internal static Color Background {get{return Light?Color.White:Color.Black;}}
  internal static Color Foreground {get{return Light?Color.FromArgb(25,25,25):Color.White;}}
  internal static Color Border {get{return Light?Color.FromArgb(180,180,180):Color.FromArgb(64,64,64);}}
  internal static readonly Color Hover=Color.FromArgb(96,96,96);
  internal static Color Disabled {get{return Light?Color.FromArgb(90,90,90):Color.FromArgb(160,160,160);}}
  internal static Color Icon {get{return Light?Color.FromArgb(80,80,80):Color.FromArgb(180,180,180);}}
  // listentry.background specifies red alpha 180; flatten over this tool's black backdrop.
  internal static readonly Color Selected=Color.FromArgb(161,13,15);
  internal static readonly Color Banner=Color.FromArgb(114,9,10);
  internal static Control WrapInput(TextBox input,int frameHeight=0){
   var margin=input.Margin;int width=input.Width,height=frameHeight>0?frameHeight:input.PreferredHeight+8;
   input.BorderStyle=BorderStyle.None;input.BackColor=Background;input.ForeColor=Foreground;input.Margin=Padding.Empty;input.Dock=DockStyle.Top;
   var frame=new Panel{Width=width+8,Height=height,BackColor=Background,Padding=new Padding(4,System.Math.Max(4,(height-input.PreferredHeight)/2),4,0),Margin=margin};
   frame.Paint+=(s,e)=>{using(var pen=new Pen(Border))e.Graphics.DrawRectangle(pen,0,0,frame.Width-1,frame.Height-1);};frame.Controls.Add(input);return frame;
  }
  internal static void StyleValue(Label label){label.BorderStyle=BorderStyle.None;label.Paint+=(s,e)=>{using(var pen=new Pen(Border))e.Graphics.DrawRectangle(pen,0,0,label.Width-1,label.Height-1);};}
 }
}

