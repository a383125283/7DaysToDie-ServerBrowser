using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
namespace SevenDaysBrowser {
 internal sealed class StatisticButton:Button {
  internal int Kind, Count;
  internal bool Active;
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(BackColor);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color color=Active?GameTheme.Red:GameTheme.Disabled;
   GameIcons.Draw(g,new[]{"server_stock","server_peer","server_friends","server_history","server_lan"}[Kind],new Rectangle(3,(Height-32)/2,32,32),color);
   using(var countFont=new Font(Font.FontFamily,17,FontStyle.Bold))TextRenderer.DrawText(g,Count.ToString(),countFont,new Rectangle(38,0,Math.Max(10,Width-38),Height),GameTheme.Foreground,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
  }
 }
 public sealed partial class BrowserForm {
  private const int PageSize=15;
  private int pageIndex,pageCount=1,selectedCategory=-1;
  private readonly StatisticButton[] statisticButtons=new StatisticButton[5];
  private readonly Label pageLabel=new Label();
  private Button previousPage,nextPage;
  private readonly ToolTip statisticsTips=new ToolTip();
  private Control BuildStatisticsBar(){
   var bar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=6,RowCount=1,Margin=Padding.Empty};
   bar.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
   for(int i=0;i<5;i++)bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,162));
   string[] names={"专用服务器","主机联机","好友服务器","收藏与历史","局域网服务器"};
   for(int i=0;i<5;i++){int kind=i;var button=new StatisticButton{Kind=i,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=GameTheme.Foreground,Margin=Padding.Empty};button.FlatAppearance.BorderSize=0;button.Click+=(s,e)=>{selectedCategory=selectedCategory==kind?-1:kind;pageIndex=0;RefreshRows();};statisticButtons[i]=button;statisticsTips.SetToolTip(button,names[i]+"；再次点击取消分类");bar.Controls.Add(button,i,0);}
   var pager=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};pager.RowStyles.Add(new RowStyle(SizeType.Absolute,54));pager.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,32));pager.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));pager.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,32));
   previousPage=Button("◀",()=>{if(pageIndex>0){pageIndex--;RefreshRows();}});nextPage=Button("▶",()=>{if(pageIndex<pageCount-1){pageIndex++;RefreshRows();}});
   foreach(var button in new[]{previousPage,nextPage}){button.AutoSize=false;button.Padding=Padding.Empty;button.Margin=Padding.Empty;button.Dock=DockStyle.Fill;button.FlatAppearance.BorderSize=0;button.Font=new Font(Font.FontFamily,15);}
   pageLabel.Dock=DockStyle.Fill;pageLabel.Text="1 / 1";pageLabel.TextAlign=ContentAlignment.MiddleCenter;pageLabel.BackColor=GameTheme.Background;pageLabel.ForeColor=GameTheme.Foreground;pageLabel.Font=new Font(Font.FontFamily,18,FontStyle.Bold);pageLabel.Margin=new Padding(0,8,0,8);
   pager.Controls.Add(previousPage,0,0);pager.Controls.Add(pageLabel,1,0);pager.Controls.Add(nextPage,2,0);bar.Controls.Add(pager,5,0);return bar;
  }
  private void UpdateStatistics(List<ServerRow> rows){int[] counts={rows.Count(r=>r.Dedicated==true),rows.Count(r=>r.Dedicated==false),rows.Count(r=>r.IsFriend),rows.Count(r=>r.IsHistory||favorites.Contains(r.Address)),rows.Count(r=>r.IsLan)};
   for(int i=0;i<5;i++){if(statisticButtons[i]==null)continue;statisticButtons[i].Count=counts[i];statisticButtons[i].Active=selectedCategory==i;statisticButtons[i].Invalidate();}}
  private bool MatchesCategory(ServerRow row){switch(selectedCategory){case 0:return row.Dedicated==true;case 1:return row.Dedicated==false;case 2:return row.IsFriend;case 3:return row.IsHistory||favorites.Contains(row.Address);case 4:return row.IsLan;default:return true;}}
  private void UpdatePager(){pageLabel.Text=(pageIndex+1)+" / "+pageCount;if(previousPage!=null)previousPage.Enabled=pageIndex>0;if(nextPage!=null)nextPage.Enabled=pageIndex<pageCount-1;}
 }
}



