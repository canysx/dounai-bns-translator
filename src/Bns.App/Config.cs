using System.Text.Json;
using Bns.Core;
namespace Bns.App;
internal record Settings
{
    public string BaseUrl {get;init;}="https://api.deepseek.com";
    public string VisionModel {get;init;}="deepseek-flash";
    public string ReplyModel {get;init;}="deepseek-flash";
    public bool RememberKey {get;init;}=true;
    public uint Hotkey {get;init;}=0x77;
    public uint Modifiers {get;init;}=0;
    public int DelayMs {get;init;}=55;
    public ApiOptions Api=>new(BaseUrl,VisionModel,ReplyModel);
    public static readonly string DirectoryPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WZ","BNS_Translator");
    public static readonly string FilePath=Path.Combine(DirectoryPath,"settings.json");
    public static Settings Load() {
        if(!File.Exists(FilePath))return new();
        try{return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))??new();}
        catch {return new();}
    }
    public void Save() {
        Directory.CreateDirectory(DirectoryPath);
        var path=FilePath+".tmp";
        // Explicit whitelist: do not serialize API Key or derived credentials.
        File.WriteAllText(path,JsonSerializer.Serialize(new {BaseUrl,VisionModel,ReplyModel,RememberKey,Hotkey,Modifiers,DelayMs},new JsonSerializerOptions{WriteIndented=true}));
        File.Move(path,FilePath,true);
    }
    internal string CredentialId=>Native.CredentialTarget(ApiClient.Endpoint(BaseUrl));
}
internal static class Ui
{
    internal static readonly Color Canvas=Color.FromArgb(245,247,251);
    internal static readonly Color Ink=Color.FromArgb(30,39,59);
    internal static readonly Color Muted=Color.FromArgb(104,116,139);
    internal static readonly Color Accent=Color.FromArgb(83,87,222);
    internal static readonly Color Line=Color.FromArgb(224,229,239);
    internal static Button Button(string title,Action action) {
        var b=new SoftButton{Text=title,AutoSize=true,MinimumSize=new Size(76,36),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Ink,Margin=new Padding(4),Padding=new Padding(10,4,10,4),Cursor=Cursors.Hand};
        b.FlatAppearance.BorderColor=Line;b.FlatAppearance.MouseOverBackColor=Color.FromArgb(237,239,255);
        b.FlatAppearance.MouseDownBackColor=Color.FromArgb(223,226,252);b.Click+=(_,_)=>action();return b;
    }
    internal static Button Primary(Button b){b.BackColor=Accent;b.ForeColor=Color.White;b.FlatAppearance.BorderColor=Accent;b.FlatAppearance.MouseOverBackColor=Color.FromArgb(67,71,199);b.FlatAppearance.MouseDownBackColor=Color.FromArgb(54,58,177);return b;}
    internal static Label Label(string text)=>new(){Text=text,AutoSize=true,ForeColor=Muted,Margin=new Padding(5,10,5,7)};
    internal static Control Heading(string title,string subtitle){
        var area=new FlowLayoutPanel{FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0),Padding=new Padding(2,4,2,18)};
        area.Controls.Add(new Label{Text=title,AutoSize=true,Font=new Font("Microsoft YaHei UI",20,FontStyle.Bold),ForeColor=Ink,Margin=new Padding(0,0,0,6)});
        area.Controls.Add(new Label{Text=subtitle,AutoSize=true,ForeColor=Muted,Margin=new Padding(0)});return area;
    }
    internal static Surface Field(Control input,int height=0){
        var margin=input.Margin;var field=new Surface{Dock=DockStyle.Fill,Margin=margin,Padding=new Padding(10,7,10,7),MinimumSize=new Size(0,height>0?height:40)};
        input.Dock=DockStyle.Fill;input.Margin=new Padding(0);if(input is TextBox t)t.BorderStyle=BorderStyle.None;
        if(height>0){field.Height=height;field.AutoSize=false;}else{field.AutoSize=true;field.AutoSizeMode=AutoSizeMode.GrowAndShrink;}
        field.Controls.Add(input);input.Enter+=(_,_)=>field.Invalidate();input.Leave+=(_,_)=>field.Invalidate();return field;
    }
    internal static void Theme(Control root){
        foreach(Control c in root.Controls){
            if(c is TextBox t){t.BackColor=Color.White;t.ForeColor=Ink;t.BorderStyle=t.Parent is Surface?BorderStyle.None:BorderStyle.FixedSingle;}
            if(c is CheckBox check){check.ForeColor=Muted;check.Cursor=Cursors.Hand;}
            if(c is NumericUpDown number){number.BackColor=Color.White;number.ForeColor=Ink;number.BorderStyle=BorderStyle.FixedSingle;}
            Theme(c);
        }
    }
    internal static void Drag(Form f) {Native.ReleaseCapture();Native.SendMessage(f.Handle,0x00A1,2,0);}
    internal static string Error(Exception e)=> e switch {
        ApiException or ArgumentException or InvalidOperationException=>e.Message,
        TaskCanceledException or OperationCanceledException=>"请求已取消或超时，可重试。",
        HttpRequestException=>"无法连接 API 服务，请检查网络和接口地址。",
        System.ComponentModel.Win32Exception=>"Windows 操作失败，请查看说明并重试。",
        _=>"操作失败，请重试；诊断日志只记录异常类型，不记录 Key 或聊天。"};
}
