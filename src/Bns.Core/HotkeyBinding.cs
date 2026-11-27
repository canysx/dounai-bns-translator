namespace Bns.Core;

public static class HotkeyBinding
{
    // RegisterHotKey flags: Alt=1, Control=2, Shift=4.
    public static void Validate(uint key,uint modifiers)
    {
        if((modifiers&~7u)!=0)throw new ArgumentException("启动键支持 Ctrl、Alt、Shift 组合。");
        if(key==0x1B)throw new ArgumentException("Esc 用于停止输入，请选择其他启动键。");
        bool supported=key is 0x08 or 0x09 or 0x0D or 0x13 or 0x14 or 0x20
            or >=0x21 and <=0x28 or 0x2C or 0x2D or 0x2E
            or >=0x30 and <=0x39 or >=0x41 and <=0x5A
            or >=0x60 and <=0x6F or >=0x70 and <=0x87
            or 0x90 or 0x91 or >=0xBA and <=0xC0 or >=0xDB and <=0xDF or 0xE2;
        if(!supported)throw new ArgumentException("请按下一个键盘按键，可同时按住 Ctrl、Alt 或 Shift。");
    }
}
