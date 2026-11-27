using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace Bns.App;
internal static class Native
{
    [DllImport("user32.dll")]internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]internal static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern int GetWindowText(nint hwnd,StringBuilder text,int max);
    [DllImport("user32.dll")]internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")]internal static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")]internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)]internal static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]internal static extern bool UnregisterHotKey(nint window,int id);
    [DllImport("user32.dll")]internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")]internal static extern nint SendMessage(nint window,uint message,nint wparam,nint lparam);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]internal static extern nint GetWindowLongPtr(nint window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]internal static extern nint SetWindowLongPtr(nint window,int index,nint value);
    [DllImport("user32.dll")]internal static extern bool SetLayeredWindowAttributes(nint window,uint color,byte alpha,uint flags);
    [DllImport("user32.dll")]internal static extern bool SetWindowPos(nint window,nint after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,INPUT[] inputs,int size);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CredWrite(ref CREDENTIAL cred,uint flags);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CredRead(string target,uint type,uint flags,out nint result);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool CredDelete(string target,uint type,uint flags);
    [DllImport("advapi32.dll")]private static extern void CredFree(nint pointer);
    [StructLayout(LayoutKind.Sequential)]internal struct INPUT { public uint Type; public INPUTUNION Data; }
    [StructLayout(LayoutKind.Explicit)]internal struct INPUTUNION {
        [FieldOffset(0)]public KEYBDINPUT Keyboard;
        [FieldOffset(0)]public MOUSEINPUT Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]internal struct KEYBDINPUT {public ushort VirtualKey,Scan;public uint Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]internal struct MOUSEINPUT {public int X,Y;public uint MouseData,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct CREDENTIAL {
        public uint Flags,Type;[MarshalAs(UnmanagedType.LPWStr)]public string? TargetName;
        [MarshalAs(UnmanagedType.LPWStr)]public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;public nint Blob;public uint Persist,AttributeCount;public nint Attributes;
        [MarshalAs(UnmanagedType.LPWStr)]public string? Alias;
        [MarshalAs(UnmanagedType.LPWStr)]public string? UserName;
    }
    internal static string Title(nint hwnd) {var s=new StringBuilder(512);GetWindowText(hwnd,s,512);return s.ToString();}
    internal static string CredentialTarget(string endpoint)=>"WZ.BNSChat/"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));
    internal static string ReadKey(string target)
    {
        if(!CredRead(target,1,0,out var ptr)) {int e=Marshal.GetLastWin32Error(); if(e==1168)return "";throw new Win32Exception(e,"无法读取 Windows 凭据。");}
        try {var c=Marshal.PtrToStructure<CREDENTIAL>(ptr);return Marshal.PtrToStringUni(c.Blob,(int)c.BlobSize/2)??"";} finally {CredFree(ptr);}
    }
    internal static void WriteKey(string target,string key)
    {
        nint ptr=Marshal.StringToCoTaskMemUni(key);
        try {
            var c=new CREDENTIAL {Type=1,TargetName=target,UserName="DeepSeek API",Blob=ptr,BlobSize=(uint)Encoding.Unicode.GetByteCount(key),Persist=2};
            if(!CredWrite(ref c,0))throw new Win32Exception(Marshal.GetLastWin32Error(),"无法保存到 Windows 凭据管理器。");
        }finally {for(int i=0;i<key.Length*2;i++)Marshal.WriteByte(ptr,i,0);Marshal.FreeCoTaskMem(ptr);}
    }
    internal static void DeleteKey(string target) {if(!CredDelete(target,1,0)&&Marshal.GetLastWin32Error()!=1168)throw new Win32Exception(Marshal.GetLastWin32Error());}
    private static INPUT Key(ushort scan,bool up)=>new(){Type=1,Data=new(){Keyboard=new(){Scan=scan,Flags=(uint)(0x0008|(up?0x0002:0))}}};
    internal static void Tap(Bns.Core.KeyStroke stroke)
    {
        INPUT[] batch=stroke.Shift?[Key(0x2A,false),Key(stroke.Scan,false),Key(stroke.Scan,true),Key(0x2A,true)] : [Key(stroke.Scan,false),Key(stroke.Scan,true)];
        if(SendInput((uint)batch.Length,batch,Marshal.SizeOf<INPUT>())!=batch.Length) {
            // Partial insertion: release only keys this operation attempted.
            INPUT[] release=stroke.Shift?[Key(stroke.Scan,true),Key(0x2A,true)]:[Key(stroke.Scan,true)];
            SendInput((uint)release.Length,release,Marshal.SizeOf<INPUT>());
            throw new InvalidOperationException("Windows 未接受按键。请检查游戏和软件权限是否一致；游戏也可能不接受模拟输入。");
        }
    }
}
internal sealed class HotkeyWindow:NativeWindow,IDisposable
{
    internal Action? Start;
    internal Action? Stop;
    private uint key;
    private uint mods;
    private bool registered;
    private bool enabled=true;
    internal HotkeyWindow(){CreateHandle(new CreateParams {Caption="BNS Hotkeys"});}
    internal bool Configure(uint newKey,uint newModifiers)
    {
        if(registered&&newKey==key&&newModifiers==mods)return true;
        if(registered)Native.UnregisterHotKey(Handle,1);
        if(!Native.RegisterHotKey(Handle,1,newModifiers|0x4000,newKey)) {
            registered=enabled&&key!=0&&Native.RegisterHotKey(Handle,1,mods|0x4000,key);return false;
        }
        key=newKey;mods=newModifiers;registered=true;
        if(!enabled){Native.UnregisterHotKey(Handle,1);registered=false;}
        return true;
    }
    internal void SetStartEnabled(bool value)
    {
        enabled=value;
        if(!value&&registered){Native.UnregisterHotKey(Handle,1);registered=false;}
        else if(value&&!registered&&key!=0)registered=Native.RegisterHotKey(Handle,1,mods|0x4000,key);
    }
    internal bool EnableStop()=>Native.RegisterHotKey(Handle,2,0x4000,0x1B);
    internal void DisableStop()=>Native.UnregisterHotKey(Handle,2);
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x0312) {if(m.WParam==1)Start?.Invoke();else if(m.WParam==2)Stop?.Invoke();}
        base.WndProc(ref m);
    }
    public void Dispose(){Native.UnregisterHotKey(Handle,1);DisableStop();DestroyHandle();}
}
