using Bns.Core;
namespace Bns.App;
internal sealed class SettingsForm:Form
{
    private readonly TextBox address=new(),key=new(){UseSystemPasswordChar=true},vision=new(),reply=new();
    private readonly CheckBox remember=new(){Text="保存 Key 到 Windows 凭据管理器",AutoSize=true};
    private readonly HotkeyCapture hotkey=new(){Width=210};
    private readonly NumericUpDown speed=new(){Minimum=20,Maximum=300,Increment=5};
    private readonly Label status=new(){AutoSize=true,MaximumSize=new Size(580,0),Padding=new Padding(4,6,4,6)};
    private readonly Button save,test,delete;
    private readonly CancellationTokenSource lifetime=new();
    private bool initializing=true,testing;
    private readonly Action<Settings,string> commit;
    private readonly Func<string,string> readKey;
    private readonly Action<string> deleteKey;
    private readonly ApiClient api;
    internal SettingsForm(Settings settings,string activeKey,ApiClient api,Action<Settings,string> commit,Func<string,string> readKey,Action<string> deleteKey)
    {
        this.api=api;this.commit=commit;this.readKey=readKey;this.deleteKey=deleteKey;
        Text="豆奶剑灵翻译助手 · 设置 · v0.2.3";AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft YaHei UI",10);ClientSize=new Size(660,610);MinimumSize=new Size(480,360);StartPosition=FormStartPosition.CenterScreen;KeyPreview=true;
        BackColor=Color.FromArgb(248,249,252);ShowInTaskbar=true;TopMost=true;MaximizeBox=true;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(0)};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=new Padding(0)};
        var grid=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=8,Padding=new Padding(12)};
        for(int i=0;i<8;i++)grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,125));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        void Row(int row,string caption,Control control) {grid.Controls.Add(Ui.Label(caption),0,row);control.Dock=DockStyle.Fill;control.Margin=new Padding(4,5,4,5);grid.Controls.Add(control,1,row);}
        address.Text=settings.BaseUrl;key.Text=activeKey;vision.Text=settings.VisionModel;reply.Text=settings.ReplyModel;remember.Checked=settings.RememberKey;
        Row(0,"API 地址",address);
        var keyRow=new TableLayoutPanel{ColumnCount=2,AutoSize=true};keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        key.Dock=DockStyle.Fill;var show=new CheckBox{Text="显示",AutoSize=true,Margin=new Padding(8,3,3,3)};show.CheckedChanged+=(_,_)=>key.UseSystemPasswordChar=!show.Checked;
        keyRow.Controls.Add(key,0,0);keyRow.Controls.Add(show,1,0);Row(1,"API Key",keyRow);
        Row(2,"截图模型",vision);Row(3,"回复模型",reply);Row(4,"密钥保存",remember);
        hotkey.SetBinding(settings.Hotkey,settings.Modifiers);hotkey.Feedback=message=>status.Text=message;
        var hotrow=new FlowLayoutPanel{AutoSize=true};hotrow.Controls.Add(hotkey);hotrow.Controls.Add(Ui.Label("点击后按下新快捷键"));Row(5,"输入启动键",hotrow);
        speed.Value=Math.Clamp(settings.DelayMs,20,300);Row(6,"每键间隔(ms)",speed);
        var note=Ui.Label("改地址后需重新填写该地址的 Key。只有点击测试或保存后的实际请求才会使用它。\n连接测试会发送一张内置测试图片和一条测试文字，可能产生少量费用。");
        note.MaximumSize=new Size(430,0);Row(7,"提示",note);
        test=Ui.Button("测试两个模型",()=>_=Test());delete=Ui.Button("删除此地址的 Key",Delete);save=Ui.Button("保存",Save);
        save.Text="保存 (Ctrl+S)";save.BackColor=Color.FromArgb(212,235,255);
        var actions=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,WrapContents=true,Margin=new Padding(0)};
        actions.Controls.AddRange([save,Ui.Button("取消",Close),test,delete]);
        var footer=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(10),Margin=new Padding(0)};
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        status.Text="填写完成后点击保存；窗口较小时，上方表单可滚动。";
        footer.Controls.Add(status,0,0);footer.Controls.Add(actions,0,1);
        scroll.Controls.Add(grid);root.Controls.Add(scroll,0,0);root.Controls.Add(footer,0,1);Controls.Add(root);
        scroll.SizeChanged+=(_,_)=>{grid.Width=Math.Max(1,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth);note.MaximumSize=new Size(Math.Max(100,grid.Width-165),0);status.MaximumSize=new Size(Math.Max(100,ClientSize.Width-45),0);};
        Shown+=(_,_)=>{var area=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);};
        address.TextChanged+=(_,_)=>{if(!initializing){key.Clear();status.Text="地址已修改，请填写此地址的 Key；也可离开地址栏读取已保存的 Key。";}};
        address.Leave+=(_,_)=>{if(string.IsNullOrEmpty(key.Text)){try{key.Text=readKey(ApiClient.Endpoint(address.Text));}catch{}}};
        initializing=false;
        FormClosed+=(_,_)=>lifetime.Cancel();
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(hotkey.ContainsFocus)return base.ProcessCmdKey(ref msg,keyData);
        if(keyData==(Keys.Control|Keys.S)){if(!testing)Save();return true;}
        if(keyData==Keys.Escape){Close();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    private Settings Draft()
    {
        var endpoint=ApiClient.Endpoint(address.Text);
        if(key.Text.Any(char.IsControl))throw new ArgumentException("Key 不能包含换行或控制字符。");
        if(string.IsNullOrWhiteSpace(vision.Text)||string.IsNullOrWhiteSpace(reply.Text))throw new ArgumentException("请填写两个模型名称。");
        HotkeyBinding.Validate(hotkey.VirtualKey,hotkey.HotkeyModifiers);
        return new Settings{BaseUrl=endpoint,VisionModel=vision.Text.Trim(),ReplyModel=reply.Text.Trim(),RememberKey=remember.Checked,
            Hotkey=hotkey.VirtualKey,Modifiers=hotkey.HotkeyModifiers,DelayMs=(int)speed.Value};
    }
    private void Save(){try{var s=Draft();commit(s,key.Text.Trim());DialogResult=DialogResult.OK;Close();}catch(Exception e){status.Text=Ui.Error(e);}}
    private void Delete(){try{deleteKey(ApiClient.Endpoint(address.Text));key.Clear();status.Text="该地址的已保存 Key 已删除。";}catch(Exception e){status.Text=Ui.Error(e);}}
    private async Task Test()
    {
        if(testing)return;
        try {
            var s=Draft();string testKey=key.Text.Trim();if(string.IsNullOrWhiteSpace(testKey))throw new ArgumentException("请填写 API Key。");
            testing=true;test.Enabled=save.Enabled=delete.Enabled=false;address.Enabled=key.Enabled=vision.Enabled=reply.Enabled=false;status.Text="测试截图模型…";
            using var bitmap=new Bitmap(360,90);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);g.DrawString("안녕하세요",new Font("Malgun Gothic",24),Brushes.Black,10,15);}
            using var mem=new MemoryStream();bitmap.Save(mem,System.Drawing.Imaging.ImageFormat.Png);
            await api.Translate(s.Api,testKey,mem.ToArray(),lifetime.Token);
            if(IsDisposed)return;status.Text="截图模型通过，正在测试回复模型…";
            await api.MakeReply(s.Api,testKey,"你好",null,lifetime.Token);
            if(!IsDisposed)status.Text="截图模型与回复模型均已返回有效数据，可以保存。";
        }catch(Exception e){if(!IsDisposed)status.Text=Ui.Error(e);}
        finally {testing=false;if(!IsDisposed){test.Enabled=save.Enabled=delete.Enabled=true;address.Enabled=key.Enabled=vision.Enabled=reply.Enabled=true;}}
    }
}
