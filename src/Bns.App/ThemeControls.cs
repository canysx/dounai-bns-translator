using System.Drawing.Drawing2D;

namespace Bns.App;

internal static class Shapes
{
    internal static GraphicsPath Rounded(RectangleF r,float radius)
    {
        var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
        p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
}

// Keep native Button keyboard navigation and accessibility while painting the surface.
internal sealed class SoftButton:Button
{
    private bool hover,pressed;
    internal SoftButton(){DoubleBuffered=true;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??Ui.Canvas);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        bool primary=BackColor==Ui.Accent;
        Color fill=!Enabled?Ui.Canvas:pressed?FlatAppearance.MouseDownBackColor:hover?FlatAppearance.MouseOverBackColor:BackColor;
        float scale=DeviceDpi/96f;
        using var path=Shapes.Rounded(new RectangleF(1,1,Width-3,Height-3),7*scale);
        using var brush=new SolidBrush(fill);e.Graphics.FillPath(brush,path);
        using var border=new Pen(!Enabled?Ui.Line:primary?fill:Ui.Line,scale);e.Graphics.DrawPath(border,path);
        var bounds=Rectangle.Inflate(ClientRectangle,-Padding.Horizontal/2,-Padding.Vertical/2);
        TextRenderer.DrawText(e.Graphics,Text,Font,bounds,Enabled?ForeColor:Ui.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-6,-6),ForeColor,fill);
    }
}

internal sealed class Surface:Panel
{
    internal Surface(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(14);SetStyle(ControlStyles.ResizeRedraw,true);}
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??Ui.Canvas);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=Shapes.Rounded(new RectangleF(1,1,Width-3,Height-3),9*DeviceDpi/96f);
        using var fill=new SolidBrush(BackColor);e.Graphics.FillPath(fill,shape);
        using var pen=new Pen(ContainsFocus?Ui.Accent:Ui.Line,DeviceDpi/96f);e.Graphics.DrawPath(pen,shape);
    }
}
