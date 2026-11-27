using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using Bns.Core;
namespace Bns.App;
internal static class Program
{
    internal static readonly string LogPath=Path.Combine(Settings.DirectoryPath,"diagnostics.log");
    [STAThread]static void Main()
    {
        try {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException+=(_,e)=>Failure(e.Exception);
            AppDomain.CurrentDomain.UnhandledException+=(_,e)=>{if(e.ExceptionObject is Exception ex)Failure(ex);};
            Application.Run(new Controller());
        }catch(Exception ex){Failure(ex);}
    }
    internal static void Failure(Exception ex)
    {
        try{Directory.CreateDirectory(Settings.DirectoryPath);File.AppendAllText(LogPath,$"{DateTimeOffset.Now:O} {ex.GetType().FullName}\n{ex.StackTrace}\n");}catch{}
        MessageBox.Show($"操作发生错误，诊断信息已记录：\n{LogPath}\n\n日志不包含 API Key 和聊天截图。","豆奶剑灵翻译助手",MessageBoxButtons.OK,MessageBoxIcon.Error);
    }
}
internal sealed class Controller:ApplicationContext
{
    private Settings settings=Settings.Load();
    private string key="";
    private readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(90)};
    private readonly ApiClient api;
    private readonly HotkeyWindow hotkeys=new();
    private readonly ResultForm result=new();
    private readonly Bubble bubble;
    private readonly NotifyIcon tray;
    private SettingsForm? settingsWindow;
    private byte[]? screenshot;
    private List<ChatLine> chat=[];
    private List<KeyStroke>? strokes;
    private CancellationTokenSource? requestCancel,inputCancel;
    private bool busy,capturing,binding,exiting;
    private nint game;
    private uint gamePid;
    private readonly System.Windows.Forms.Timer foregroundMonitor=new(){Interval=40};
    private bool inputRunning;
    private readonly Icon icon;
    internal Controller()
    {
        api=new ApiClient(http);icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath)??SystemIcons.Application;
        result.Icon=icon;
        var menu=new ContextMenuStrip();
        menu.Items.Add("截图翻译",null,(_,_)=>_=Capture());
        menu.Items.Add("显示翻译 / 解锁",null,(_,_)=>{result.Unlock();result.ShowPage();});
        menu.Items.Add("中文回复",null,(_,_)=>result.ShowPage(true));
        menu.Items.Add("绑定游戏窗口（3秒后）",null,(_,_)=>_=Bind());
        menu.Items.Add("选择图片翻译…",null,(_,_)=>_=LoadPicture());
        menu.Items.Add("设置 API 与热键",null,(_,_)=>OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出",null,(_,_)=>Shutdown());
        bubble=new Bubble(menu){Icon=icon};bubble.CaptureRequested=()=>_=Capture();
        tray=new NotifyIcon{Icon=icon,Text="豆奶剑灵翻译助手 · 右键设置",Visible=true,ContextMenuStrip=menu};
        tray.DoubleClick+=(_,_)=>{result.Unlock();result.ShowPage();};
        result.CaptureRequested=()=>_=Capture();result.SettingsRequested=OpenSettings;
        result.ReplyRequested=()=>_=GenerateReply();result.RetryRequested=()=>_=Translate();
        result.BindRequested=()=>_=Bind();result.CancelRequested=()=>requestCancel?.Cancel();
        result.ReplyEdited=()=>{strokes=null;inputCancel?.Cancel();};
        hotkeys.Start=()=>_=TypeReply();hotkeys.Stop=()=>inputCancel?.Cancel();
        foregroundMonitor.Tick+=(_,_)=>{
            var foreground=Native.GetForegroundWindow();
            if(inputRunning){if(foreground!=game)inputCancel?.Cancel();}
            else hotkeys.SetStartEnabled(GetPid(foreground)!=(uint)Environment.ProcessId);
        };foregroundMonitor.Start();
        try{if(settings.RememberKey)key=Native.ReadKey(settings.CredentialId);}catch{result.Status("读取已保存 Key 失败，可在设置中重新填写。");}
        if(!hotkeys.Configure(settings.Hotkey,settings.Modifiers))result.Status("启动热键被占用，请在设置中修改。");
        result.SetShortcut(Shortcut(settings));bubble.Show();
        if(string.IsNullOrWhiteSpace(key))bubble.BeginInvoke(OpenSettings);
    }
    private static string Shortcut(Settings s)=>HotkeyCapture.Describe(s.Hotkey,s.Modifiers);
    private void Status(string s){if(!exiting&&!result.IsDisposed)result.Status(s);}
    private void OpenSettings()
    {
        if(inputRunning){Status("请先等待输入完成或按 Esc 停止。");return;}
        hotkeys.SetStartEnabled(false);
        if(settingsWindow is {IsDisposed:false}){settingsWindow.Show();settingsWindow.Activate();return;}
        settingsWindow=new SettingsForm(settings,key,api,CommitSettings,
            endpoint=>Native.ReadKey(Native.CredentialTarget(endpoint)),endpoint=>{
                Native.DeleteKey(Native.CredentialTarget(endpoint));if(endpoint==ApiClient.Endpoint(settings.BaseUrl))key="";
            });
        settingsWindow.Icon=icon;settingsWindow.Show();
    }
    private void CommitSettings(Settings draft,string newKey)
    {
        if(newKey.Length>2000)throw new ArgumentException("Key 太长。");
        HotkeyBinding.Validate(draft.Hotkey,draft.Modifiers);
        if(!hotkeys.Configure(draft.Hotkey,draft.Modifiers))throw new InvalidOperationException("这个启动热键已被占用，请选其他组合。");
        try {
            if(draft.RememberKey&&!string.IsNullOrEmpty(newKey))Native.WriteKey(draft.CredentialId,newKey);
            else Native.DeleteKey(draft.CredentialId);
            draft.Save();settings=draft;key=newKey;
        }catch {hotkeys.Configure(settings.Hotkey,settings.Modifiers);throw;}
        result.SetShortcut(Shortcut(settings));Status("设置已保存，下次请求生效。");
    }
    private bool ReadyForRequest()
    {
        if(busy){Status("正在处理上一请求，可点击取消请求。");return false;}
        if(string.IsNullOrWhiteSpace(key)){OpenSettings();Status("请先设置 API Key。");return false;}
        return true;
    }
    private async Task Capture()
    {
        if(capturing||busy||binding||inputRunning){Status("请等待当前操作结束。");return;}
        capturing=true;
        bool visible=result.Visible;
        try {
            bubble.Hide();result.Hide();if(settingsWindow is {Visible:true})settingsWindow.Hide();
            await Task.Delay(180);if(exiting)return;
            var bounds=SystemInformation.VirtualScreen;
            using var bitmap=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);
            using var selector=new CaptureForm(bitmap,bounds);
            if(selector.ShowDialog()==DialogResult.OK&&selector.Png is not null){screenshot=selector.Png;bubble.Show();result.ShowPage();await Translate();}
            else if(visible)result.ShowPage();
        }catch(Exception e){Status(Ui.Error(e));result.ShowPage();}
        finally {capturing=false;if(!exiting){bubble.Show();if(settingsWindow is {IsDisposed:false})settingsWindow.Show();}}
    }
    private async Task LoadPicture()
    {
        if(capturing||busy||inputRunning)return;
        using var file=new OpenFileDialog{Filter="聊天截图|*.png;*.jpg;*.jpeg;*.bmp",Title="选择后发送到配置的 API 识别翻译"};
        if(file.ShowDialog()!=DialogResult.OK)return;
        try {
            using var img=Image.FromFile(file.FileName);if(img.Width>8192||img.Height>8192)throw new ArgumentException("图片太大，请裁剪聊天区域。");
            using var mem=new MemoryStream();img.Save(mem,ImageFormat.Png);screenshot=mem.ToArray();result.ShowPage();await Translate();
        }catch(Exception e){Status(Ui.Error(e));}
    }
    private CancellationToken StartRequest(string status)
    {
        busy=true;requestCancel?.Dispose();requestCancel=new();result.Busy(true);Status(status);return requestCancel.Token;
    }
    private void FinishRequest(){busy=false;if(!exiting)result.Busy(false);}
    private async Task Translate()
    {
        if(screenshot is null){Status("还没有截图，请点击悬浮按钮框选。");return;}
        if(!ReadyForRequest())return;
        var token=StartRequest("正在将截图发送到 API 识别翻译…");
        try {var lines=await api.Translate(settings.Api,key,screenshot,token);if(exiting)return;chat=lines;result.SetChat(lines);Status("翻译完成 · "+lines.Count+" 条消息");}
        catch(Exception e){Status(Ui.Error(e)+" 截图已保留，可点重试。");}
        finally {FinishRequest();}
    }
    private async Task GenerateReply()
    {
        if(!ReadyForRequest())return;
        var chinese=result.Chinese.Text;
        if(string.IsNullOrWhiteSpace(chinese)){Status("先输入你想说的中文。");return;}
        if(result.Context.Checked&&chat.Count==0){Status("还没有聊天上下文，请先截图翻译或取消勾选。");return;}
        result.ClearReply();strokes=null;
        var context=result.Context.Checked?JsonSerializer.Serialize(chat):null;
        var token=StartRequest("正在生成礼貌、简短的韩文回复…");
        try {
            var response=await api.MakeReply(settings.Api,key,chinese,context,token);if(exiting)return;
            // If user edited the prompt/context while awaiting API, do not arm stale output.
            if(result.Chinese.Text!=chinese|| (result.Context.Checked?JsonSerializer.Serialize(chat):null)!=context){Status("输入或上下文已变化，请重新生成。");return;}
            try {var converted=KoreanKeys.Convert(response.Korean);strokes=converted;result.SetReply(response,KoreanKeys.Describe(converted));Status("请核对韩文与中文回译，再勾选允许热键输入。");}
            catch(ArgumentException e){result.SetReply(response,e.Message);Status("韩文已生成，但不能自动输入："+e.Message);}
        }catch(Exception e){Status(Ui.Error(e));}
        finally {FinishRequest();}
    }
    private async Task Bind()
    {
        if(binding||capturing||inputRunning||busy)return;
        binding=true;
        try {
            result.Unlock();result.ShowPage(true);
            for(int n=3;n>0;n--){Status($"{n} 秒后绑定当前前台窗口，请切换到游戏。");await Task.Delay(1000);if(exiting)return;}
            var hwnd=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(hwnd,out var pid);
            if(hwnd==0||pid==(uint)Environment.ProcessId||!Native.IsWindow(hwnd))throw new InvalidOperationException("没有选到游戏，请重试并在倒计时内切到游戏。");
            game=hwnd;gamePid=pid;string name;try{name=Process.GetProcessById((int)pid).ProcessName;}catch{name="窗口";}
            result.SetTarget($"已绑定：{Native.Title(hwnd)} · {name}");Status("游戏窗口已绑定，本次运行有效。先开聊天框、切到韩文，再按启动热键。");
        }catch(Exception e){Status(Ui.Error(e));}
        finally {binding=false;}
    }
    private bool TargetIsForeground()=>game!=0&&Native.IsWindow(game)&&Native.GetForegroundWindow()==game&&GetPid(game)==gamePid;
    private static uint GetPid(nint hwnd){Native.GetWindowThreadProcessId(hwnd,out var pid);return pid;}
    private async Task TypeReply()
    {
        if(inputRunning||busy||capturing||binding)return;
        if(strokes is not {Count:>0}||!result.Approved.Checked){Status("请先生成回复、核对内容，并勾选允许热键输入。");return;}
        if(!TargetIsForeground()){Status("请先绑定游戏窗口，再切到这个游戏的聊天输入框。");return;}
        var thread=Native.GetWindowThreadProcessId(game,out _);long layout=Native.GetKeyboardLayout(thread).ToInt64();
        if((layout&0x3FF)!=0x12){Status("当前窗口未使用韩语输入法，请切到韩语 두벌식 并开启韩文模式。");return;}
        if(!hotkeys.EnableStop()){Status("无法注册 Esc 停止键，未开始输入。");return;}
        inputRunning=true;inputCancel?.Dispose();inputCancel=new();var token=inputCancel.Token;
        var plan=strokes.ToArray();int delay=Math.Clamp(settings.DelayMs,20,300);
        try {
            // Single-letter start keys must not consume the simulated Korean keystrokes.
            hotkeys.SetStartEnabled(false);
            Status("等待松开启动热键…");
            // Do not synthesize while a user's shortcut/modifier is still held.
            for(int i=0;;i++){
                token.ThrowIfCancellationRequested();if(!TargetIsForeground())throw new OperationCanceledException();
                bool held=new[]{(int)settings.Hotkey,0x10,0x11,0x12,0x5B,0x5C}.Any(k=>(Native.GetAsyncKeyState(k)&0x8000)!=0);
                if(!held)break;if(i>=100)throw new InvalidOperationException("请先松开启动键及 Ctrl/Alt/Shift 后重试。");await Task.Delay(20,token);
            }
            Status("正在输入… Esc 可立即停止");
            await Task.Run(()=>InputRunner.Run(plan,delay,
                ()=>TargetIsForeground()&&new[]{0x1B,0x10,0x11,0x12,0x5B,0x5C}.All(k=>(Native.GetAsyncKeyState(k)&0x8000)==0),
                Native.Tap,token),token);
            Status("已输入，尚未发送。请在游戏中检查后手动按回车。");
        }catch(OperationCanceledException){Status("输入已停止，已输入的文字保留；请检查游戏聊天框。");}
        catch(Exception e){Status(Ui.Error(e));}
        finally {inputRunning=false;hotkeys.DisableStop();if(!exiting){hotkeys.SetStartEnabled(GetPid(Native.GetForegroundWindow())!=(uint)Environment.ProcessId);if(!result.IsDisposed)result.Approved.Checked=false;}}
    }
    private void Shutdown()
    {
        if(exiting)return;exiting=true;requestCancel?.Cancel();inputCancel?.Cancel();
        foregroundMonitor.Stop();foregroundMonitor.Dispose();tray.Visible=false;tray.Dispose();hotkeys.Dispose();
        settingsWindow?.Close();result.Shutdown();bubble.Close();http.Dispose();icon.Dispose();ExitThread();
    }
}
