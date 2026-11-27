using System.Drawing.Imaging;
namespace Bns.App;
internal sealed class CaptureForm:Form
{
    private readonly Bitmap desktop;
    private Rectangle selection;
    private Point anchor;
    private bool drawing;
    private readonly FlowLayoutPanel toolbar=new(){AutoSize=true,BackColor=Color.FromArgb(255,247,218),Visible=false};
    internal byte[]? Png {get;private set;}
    internal CaptureForm(Bitmap image,Rectangle screen)
    {
        desktop=image;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=screen;
        TopMost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;DoubleBuffered=true;KeyPreview=true;AutoScaleMode=AutoScaleMode.None;
        toolbar.Controls.AddRange([Ui.Button("重新框选",()=>{selection=Rectangle.Empty;toolbar.Hide();Invalidate();}),Ui.Button("取消",()=>{DialogResult=DialogResult.Cancel;Close();}),Ui.Button("翻译",Accept)]);
        Controls.Add(toolbar);
    }
    private void Accept()
    {
        if(selection.Width<8||selection.Height<8)return;
        using var crop=desktop.Clone(selection,PixelFormat.Format32bppArgb);
        using var mem=new MemoryStream();crop.Save(mem,ImageFormat.Png);Png=mem.ToArray();DialogResult=DialogResult.OK;Close();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;anchor=e.Location;drawing=true;toolbar.Hide();selection=Rectangle.Empty;Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(!drawing)return;
        selection=Rectangle.Intersect(new Rectangle(Math.Min(anchor.X,e.X),Math.Min(anchor.Y,e.Y),Math.Abs(e.X-anchor.X),Math.Abs(e.Y-anchor.Y)),ClientRectangle);
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);if(!drawing)return;drawing=false;
        if(selection.Width<8||selection.Height<8){selection=Rectangle.Empty;Invalidate();return;}
        toolbar.Location=new Point(Math.Clamp(selection.Left,0,Math.Max(0,ClientSize.Width-340)),Math.Clamp(selection.Bottom+8,0,Math.Max(0,ClientSize.Height-48)));
        toolbar.Show();toolbar.BringToFront();Invalidate();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {base.OnKeyDown(e);if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(desktop,0,0);
        using var dim=new SolidBrush(Color.FromArgb(135,0,0,0));
        if(selection.IsEmpty)e.Graphics.FillRectangle(dim,ClientRectangle);
        else {
            var region=new Region(ClientRectangle);region.Exclude(selection);e.Graphics.FillRegion(dim,region);region.Dispose();
            using var pen=new Pen(Color.FromArgb(88,208,255),2);e.Graphics.DrawRectangle(pen,selection);
        }
        using var font=new Font("Microsoft YaHei UI",12);
        e.Graphics.DrawString("拖动框选聊天内容 · 松开后点击「翻译」 · Esc 取消",font,Brushes.White,20,20);
        base.OnPaint(e);
    }
    // Bitmap belongs to caller, disposed after modal selector closes.
}
