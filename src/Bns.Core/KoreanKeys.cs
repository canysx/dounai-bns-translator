using System.Text;
namespace Bns.Core;

public readonly record struct KeyStroke(ushort Scan, bool Shift, string Label);
public static class KoreanKeys
{
    public static readonly string[] Initial = ["r","R","s","e","E","f","a","q","Q","t","T","d","w","W","c","z","x","v","g"];
    public static readonly string[] Vowel = ["k","o","i","O","j","p","u","P","h","hk","ho","hl","y","n","nj","np","nl","b","m","ml","l"];
    public static readonly string[] Final = ["","r","R","rt","s","sw","sg","e","f","fr","fa","fq","ft","fx","fv","fg","a","q","qt","t","T","d","w","c","z","x","v","g"];
    private const string CompatInitial = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private const string CompatVowel = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
    private const string CompatFinal = "\0ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
    private static readonly Dictionary<char,ushort> Scans = new()
    {
        ['q']=0x10,['w']=0x11,['e']=0x12,['r']=0x13,['t']=0x14,['y']=0x15,['u']=0x16,['i']=0x17,['o']=0x18,['p']=0x19,
        ['a']=0x1E,['s']=0x1F,['d']=0x20,['f']=0x21,['g']=0x22,['h']=0x23,['j']=0x24,['k']=0x25,['l']=0x26,
        ['z']=0x2C,['x']=0x2D,['c']=0x2E,['v']=0x2F,['b']=0x30,['n']=0x31,['m']=0x32,
        ['1']=0x02,['2']=0x03,['3']=0x04,['4']=0x05,['5']=0x06,['6']=0x07,['7']=0x08,['8']=0x09,['9']=0x0A,['0']=0x0B,
        [' ']=0x39,['-']=0x0C,['=']=0x0D,['[']=0x1A,[']']=0x1B,[';']=0x27,['\'']=0x28,['`']=0x29,['\\']=0x2B,[',']=0x33,['.']=0x34,['/']=0x35
    };
    private const string ShiftSymbols = "!@#$%^&*()_+{}:\"~|<>?";
    private const string BaseSymbols = "1234567890-=[];'`\\,./";
    public static List<KeyStroke> Convert(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("韩文回复为空。");
        if (value.Length > 600) throw new ArgumentException("回复过长，请缩短到600字以内。");
        List<KeyStroke> keys=[];
        foreach(char raw in value.Normalize(NormalizationForm.FormC))
        {
            char c = raw switch { '，'=>',','。'=>'.','！'=>'!','？'=>'?','：'=>':','；'=>';','（'=>'(','）'=>')','“' or '”'=>'"','‘' or '’'=>'\'', _=>raw };
            int n=c-0xAC00;
            if(n>=0 && n<11172) { Add(Initial[n/588]); Add(Vowel[n%588/28]); Add(Final[n%28]); continue; }
            int i=CompatInitial.IndexOf(c); if(i>=0) {Add(Initial[i]);continue;}
            i=CompatVowel.IndexOf(c); if(i>=0) {Add(Vowel[i]);continue;}
            i=CompatFinal.IndexOf(c); if(i>0) {Add(Final[i]);continue;}
            if(c>=0x1100 && c<=0x1112) {Add(Initial[c-0x1100]);continue;}
            if(c>=0x1161 && c<=0x1175) {Add(Vowel[c-0x1161]);continue;}
            if(c>=0x11A8 && c<=0x11C2) {Add(Final[c-0x11A7]);continue;}
            if(char.IsAsciiLetter(c)) throw new ArgumentException("回复含英文字母，韩文输入状态下会产生歧义。请让回复改用韩文名称，或复制韩文手动输入。");
            if(Scans.TryGetValue(c,out var scan)) {keys.Add(new(scan,false,c==' '?"[空格]":c.ToString()));continue;}
            i=ShiftSymbols.IndexOf(c); if(i>=0) {keys.Add(new(Scans[BaseSymbols[i]],true,"Shift+"+BaseSymbols[i]));continue;}
            // Never map Enter, Tab or other command/control keys.
            throw new ArgumentException($"自动按键不支持字符「{c}」。可复制韩文手动输入，或重新生成。");
        }
        return keys;
        void Add(string letters) { foreach(char k in letters) keys.Add(new(Scans[char.ToLowerInvariant(k)],char.IsUpper(k),char.IsUpper(k)?"Shift+"+char.ToLowerInvariant(k):k.ToString())); }
    }
    public static string Describe(IEnumerable<KeyStroke> keys)=>string.Join(" → ",keys.Select(k=>k.Label));
}
