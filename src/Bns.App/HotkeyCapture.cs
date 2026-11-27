using Bns.Core;
namespace Bns.App;

internal sealed class HotkeyCapture:TextBox
{
    internal uint VirtualKey {get;private set;}
    internal uint HotkeyModifiers {get;private set;}
    internal Action<string>? Feedback;
    internal HotkeyCapture()
    {
        ReadOnly=true;ShortcutsEnabled=false;Cursor=Cursors.Hand;
        AccessibleName="自定义输入启动键";
        Enter+=(_,_)=>Feedback?.Invoke("现在按下想使用的单键或组合键；按 Tab 可切到下一项。Esc 保留为停止键。");
    }
    internal void SetBinding(uint key,uint modifiers)
    {
        VirtualKey=key;HotkeyModifiers=modifiers;Text=Describe(key,modifiers);
    }
    internal static string Describe(uint key,uint modifiers)=>
        ((modifiers&2)!=0?"Ctrl+":"")+((modifiers&1)!=0?"Alt+":"")+((modifiers&4)!=0?"Shift+":"")+((Keys)key).ToString();
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        // Keep ordinary Tab available for keyboard navigation; combinations can use Tab.
        if(keyData==Keys.Tab||keyData==(Keys.Shift|Keys.Tab))return base.ProcessCmdKey(ref msg,keyData);
        uint key=(uint)(keyData&Keys.KeyCode);
        if(((Native.GetAsyncKeyState(0x5B)|Native.GetAsyncKeyState(0x5C))&0x8000)!=0){Feedback?.Invoke("请选择 Ctrl、Alt、Shift 组合，Win 组合由系统使用。");return true;}
        if(key is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5)return true;
        uint modifiers=((keyData&Keys.Control)!=0?2u:0u)|((keyData&Keys.Alt)!=0?1u:0u)|((keyData&Keys.Shift)!=0?4u:0u);
        try {HotkeyBinding.Validate(key,modifiers);SetBinding(key,modifiers);Feedback?.Invoke("已选择 "+Text+"，点击保存后生效。");}
        catch(ArgumentException e){Feedback?.Invoke(e.Message);}
        return true;
    }
}
