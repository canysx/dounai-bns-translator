using Bns.Core;
namespace Bns.App;
internal sealed class ResultForm:Form
{
    internal Action? CaptureRequested,SettingsRequested,ReplyRequested,RetryRequested,BindRequested,CancelRequested;
    internal Action? ReplyEdited;
    internal readonly TextBox Chinese=new(){Multiline=true,Dock=DockStyle.Fill,MaxLength=1500,ScrollBars=ScrollBars.Vertical};
    internal readonly CheckBox Context=new(){Text="结合刚才聊天",AutoSize=true};
    internal readonly CheckBox Approved=new(){Text="已核对韩文及中文回译，允许热键输入",AutoSize=true};
    private readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    private readonly TextBox translation=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None};
    private readonly TextBox korean=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
    private readonly TextBox chineseBack=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
    private readonly TextBox sequence=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
    private readonly Label status=new(){AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(9),MaximumSize=new Size(470,0)};
    private readonly Label target=new(){AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(9,3,9,7),MaximumSize=new Size(470,0)};
    private readonly CheckBox original=new(){Text="同时显示韩文原文",AutoSize=true};
    private readonly Button generate,retry,cancel;
    private readonly List<ChatLine> chat=[];
    private bool closing,locked;
    internal ResultForm()
    {
        Text="豆奶剑灵翻译助手 · 翻译 / 回复 · v0.2.5";AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft YaHei UI",10);BackColor=Ui.Canvas;ForeColor=Ui.Ink;
        TopMost=true;Opacity=1;ClientSize=new Size(580,740);MinimumSize=new Size(440,440);StartPosition=FormStartPosition.Manual;Location=new Point(420,250);
        var header=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,BackColor=Ui.Canvas,Padding=new Padding(0,4,0,4)};
        header.Controls.AddRange([Ui.Primary(Ui.Button("＋ 截图翻译",()=>CaptureRequested?.Invoke())),Ui.Button("设置",()=>SettingsRequested?.Invoke()),Ui.Button("绑定游戏",()=>BindRequested?.Invoke()),Ui.Button("收起",Hide)]);
        var appearance=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new Padding(4)};
        var alpha=new TrackBar{Minimum=35,Maximum=100,Value=100,TickStyle=TickStyle.None,Width=95};alpha.ValueChanged+=(_,_)=>Opacity=alpha.Value/100.0;
        var size=new NumericUpDown{Minimum=12,Maximum=28,Value=14,Width=60};size.ValueChanged+=(_,_)=>{translation.Font=new Font(Font.FontFamily,(float)size.Value);korean.Font=translation.Font;chineseBack.Font=translation.Font;sequence.Font=new Font("Consolas",(float)size.Value);};
        var click=Ui.Button("穿透",()=>{locked=!locked;ApplyClickThrough();});
        appearance.Controls.AddRange([Ui.Label("不透明度"),alpha,Ui.Label("字号"),size,click]);
        var transTab=new TabPage("聊天翻译"){BackColor=Color.White,Padding=new Padding(16)};translation.BackColor=Color.White;translation.Font=new Font(Font.FontFamily,14);
        var transActions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(0,12,0,0)};
        retry=Ui.Button("重试翻译",()=>RetryRequested?.Invoke());cancel=Ui.Button("取消请求",()=>CancelRequested?.Invoke());
        transActions.Controls.AddRange([original,Ui.Button("复制中文",()=>Copy(string.Join(Environment.NewLine,chat.Select(x=>x.Translation)))),retry,cancel]);
        original.CheckedChanged+=(_,_)=>RenderChat();var reading=Ui.Field(translation,100);reading.Dock=DockStyle.Fill;transTab.Controls.Add(reading);transTab.Controls.Add(transActions);
        var replyTab=new TabPage("撰写回复"){BackColor=Color.White};
        // Keep natural row heights; scroll instead of compressing controls.
        var replyScroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.White};
        var grid=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=12,Padding=new Padding(16)};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int row=0;row<12;row++)grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        void Box(TextBox box,int height,float fontSize) {
            box.AutoSize=false;box.MinimumSize=new Size(0,height);box.Height=height;box.Dock=DockStyle.Fill;
            box.Margin=new Padding(3,2,3,8);box.BackColor=Color.White;box.ForeColor=Ui.Ink;
            box.Font=new Font("Microsoft YaHei UI",fontSize);box.BorderStyle=BorderStyle.FixedSingle;
        }
        Box(Chinese,82,11);Box(korean,76,14);Box(chineseBack,64,12);Box(sequence,110,12);
        sequence.Font=new Font("Consolas",12);
        var promptLabel=Ui.Label("你想说的中文");grid.Controls.Add(promptLabel,0,0);grid.Controls.Add(Ui.Field(Chinese,96),0,1);
        Context.Margin=new Padding(4,4,4,8);grid.Controls.Add(Context,0,2);
        generate=Ui.Primary(Ui.Button("生成韩文回复",()=>ReplyRequested?.Invoke()));
        var genRow=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,WrapContents=true};
        genRow.Controls.AddRange([generate,Ui.Button("复制韩文",()=>Copy(korean.Text))]);grid.Controls.Add(genRow,0,3);
        grid.Controls.Add(Ui.Label("韩文回复"),0,4);grid.Controls.Add(Ui.Field(korean,90),0,5);
        grid.Controls.Add(Ui.Label("中文回译"),0,6);grid.Controls.Add(Ui.Field(chineseBack,78),0,7);
        grid.Controls.Add(Ui.Label("按键顺序（标准韩文 두벌식）"),0,8);grid.Controls.Add(Ui.Field(sequence,124),0,9);
        Approved.Margin=new Padding(4,10,4,8);grid.Controls.Add(Approved,0,10);
        var help=Ui.Label("先打开游戏聊天输入框并切到韩文；Esc 停止，回车自行发送。");
        help.MaximumSize=new Size(430,0);grid.Controls.Add(help,0,11);
        replyScroll.Controls.Add(grid);replyTab.Controls.Add(replyScroll);tabs.TabPages.Add(transTab);tabs.TabPages.Add(replyTab);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=new Padding(0),Padding=new Padding(12)};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.Margin=target.Margin=status.Margin=appearance.Margin=new Padding(0);
        root.Controls.Add(header,0,0);root.Controls.Add(target,0,1);root.Controls.Add(tabs,0,2);root.Controls.Add(status,0,3);root.Controls.Add(appearance,0,4);
        Controls.Add(root);
        status.ForeColor=Ui.Muted;status.Font=new Font(Font.FontFamily,9);status.Padding=new Padding(8,10,8,4);
        target.ForeColor=Ui.Muted;target.Font=new Font(Font.FontFamily,9);target.Padding=new Padding(8,10,8,12);
        appearance.Padding=new Padding(4,8,4,4);
        tabs.DrawMode=TabDrawMode.OwnerDrawFixed;tabs.ItemSize=new Size(150,42);tabs.SizeMode=TabSizeMode.Fixed;
        tabs.DrawItem+=(_,e)=>{
            bool selected=e.Index==tabs.SelectedIndex;var rect=tabs.GetTabRect(e.Index);
            using var bg=new SolidBrush(selected?Color.White:Ui.Canvas);e.Graphics.FillRectangle(bg,rect);
            TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,Font,rect,selected?Ui.Accent:Ui.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            if(selected){using var pen=new Pen(Ui.Accent,3);e.Graphics.DrawLine(pen,rect.Left+18,rect.Bottom-3,rect.Right-18,rect.Bottom-3);}
        };
        Ui.Theme(this);translation.BorderStyle=BorderStyle.None;
        translation.Text="准备好阅读聊天了。\r\n\r\n点击「截图翻译」，框选游戏中的韩文聊天区域。";
        replyScroll.SizeChanged+=(_,_)=>{
            grid.Width=Math.Max(1,replyScroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth);
            int width=Math.Max(150,replyScroll.ClientSize.Width-45);
            help.MaximumSize=new Size(width,0);Approved.MaximumSize=new Size(width,0);
        };
        SizeChanged+=(_,_)=>{status.MaximumSize=target.MaximumSize=new Size(Math.Max(180,ClientSize.Width-8),0);};
        Shown+=(_,_)=>{
            var area=Screen.FromControl(this).WorkingArea;
            Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
            Location=new Point(Math.Clamp(Left,area.Left,Math.Max(area.Left,area.Right-Width)),Math.Clamp(Top,area.Top,Math.Max(area.Top,area.Bottom-Height)));
        };
        Chinese.TextChanged+=(_,_)=>{ClearReply();ReplyEdited?.Invoke();};
        Context.CheckedChanged+=(_,_)=>{ClearReply();ReplyEdited?.Invoke();};
        FormClosing+=(_,e)=>{if(!closing){e.Cancel=true;Hide();}};
        Status("就绪 · 点击悬浮按钮框选截图；右键悬浮按钮可打开设置。");SetTarget("未绑定游戏窗口");
    }
    private static void Copy(string value){if(!string.IsNullOrEmpty(value)){try{Clipboard.SetText(value);}catch{MessageBox.Show("剪贴板忙，请再试一次。");}}}
    internal void ShowPage(bool replyPage=false){tabs.SelectedIndex=replyPage?1:0;Show();Activate();}
    internal void Status(string value)=>status.Text=value;
    internal void SetTarget(string value)=>target.Text=value;
    internal void SetChat(List<ChatLine> lines){chat.Clear();chat.AddRange(lines);RenderChat();if(Context.Checked){ClearReply();ReplyEdited?.Invoke();}}
    private void RenderChat()=>translation.Text=chat.Count==0?"没有识别到聊天消息。":string.Join("\r\n\r\n",chat.Select(c=>original.Checked?c.Source+"\r\n"+c.Translation:c.Translation));
    internal void SetReply(Reply value,string keys){korean.Text=value.Korean;chineseBack.Text=value.Chinese;sequence.Text=keys;Approved.Checked=false;}
    internal void ClearReply(){Approved.Checked=false;korean.Clear();chineseBack.Clear();sequence.Clear();}
    internal void Busy(bool value){generate.Enabled=retry.Enabled=!value;cancel.Enabled=value;}
    internal void SetShortcut(string text)=>Approved.Text=$"已核对回复，允许 {text} 自动输入";
    internal void Unlock(){locked=false;ApplyClickThrough();}
    internal void Shutdown(){closing=true;Close();}
    private void ApplyClickThrough()
    {
        var style=Native.GetWindowLongPtr(Handle,-20).ToInt64();
        style=locked?style|0x20|0x80000:style&~0x20;
        Native.SetWindowLongPtr(Handle,-20,(nint)style);if(locked)Native.SetLayeredWindowAttributes(Handle,0,(byte)(Opacity*255),2);Native.SetWindowPos(Handle,(nint)(-1),0,0,0,0,0x0027);
        Status(locked?"已穿透：右键小悬浮按钮，选择「解锁翻译窗」恢复。":"翻译窗已解锁。");
    }
}
internal sealed class Bubble:Form
{
    private readonly Image avatar;
    internal Action? CaptureRequested;
    private Point down,position;
    private bool dragging;
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x00000080;return p;}}
    internal Bubble(ContextMenuStrip menu)
    {
        using var stream=typeof(Bubble).Assembly.GetManifestResourceStream("Bns.App.avatar.png")??throw new InvalidOperationException("找不到人物图标。");
        using var source=Image.FromStream(stream);avatar=new Bitmap(source);
        Text="豆奶剑灵翻译助手";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;Size=new Size(64,64);AutoScaleMode=AutoScaleMode.None;
        Location=new Point(320,600);BackColor=Color.White;ContextMenuStrip=menu;DoubleBuffered=true;Cursor=Cursors.Hand;
        using var path=new System.Drawing.Drawing2D.GraphicsPath();path.AddEllipse(0,0,64,64);Region=new Region(path);
        var tip=new ToolTip();tip.SetToolTip(this,"点击截图翻译 · 拖动移动 · 右键设置");
    }
    protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=(nint)3;return;}base.WndProc(ref m);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){down=Cursor.Position;position=Location;dragging=false;}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(e.Button==MouseButtons.Left){var delta=new Size(Cursor.Position.X-down.X,Cursor.Position.Y-down.Y);if(Math.Abs(delta.Width)+Math.Abs(delta.Height)>5)dragging=true;if(dragging)Location=position+delta;}}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left&&!dragging)CaptureRequested?.Invoke();}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.Clear(Color.White);
        e.Graphics.DrawImage(avatar,ClientRectangle);
        base.OnPaint(e);
    }
    protected override void Dispose(bool disposing){if(disposing)avatar.Dispose();base.Dispose(disposing);}
}
