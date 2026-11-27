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
    internal static readonly Color Yellow=Color.FromArgb(255,242,183);
    internal static readonly Color Ink=Color.FromArgb(39,48,63);
    internal static Button Button(string title,Action action) {
        var b=new Button{Text=title,AutoSize=true,Height=32,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Ink,Margin=new Padding(4)};
        b.FlatAppearance.BorderColor=Color.FromArgb(218,207,166);b.Click+=(_,_)=>action();return b;
    }
    internal static Label Label(string text)=>new(){Text=text,AutoSize=true,ForeColor=Ink,Margin=new Padding(5,8,5,5)};
    internal static void Drag(Form f) {Native.ReleaseCapture();Native.SendMessage(f.Handle,0x00A1,2,0);}
    internal static string Error(Exception e)=> e switch {
        ApiException or ArgumentException or InvalidOperationException=>e.Message,
        TaskCanceledException or OperationCanceledException=>"请求已取消或超时，可重试。",
        HttpRequestException=>"无法连接 API 服务，请检查网络和接口地址。",
        System.ComponentModel.Win32Exception=>"Windows 操作失败，请查看说明并重试。",
        _=>"操作失败，请重试；诊断日志只记录异常类型，不记录 Key 或聊天。"};
}
