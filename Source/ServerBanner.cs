using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace SevenDaysBrowser {
 internal sealed class ServerBanner:Panel {
  public ServerBanner(){DoubleBuffered=true;}
  protected override void OnPaintBackground(PaintEventArgs e){
   if(Width<1||Height<1)return;
   using(var brush=new SolidBrush(GameTheme.Banner))e.Graphics.FillRectangle(brush,ClientRectangle);
   using(var pen=new Pen(GameTheme.Red,2)){e.Graphics.DrawLine(pen,0,1,Width,1);e.Graphics.DrawLine(pen,0,Height-2,Width,Height-2);}
  }
 }
}

