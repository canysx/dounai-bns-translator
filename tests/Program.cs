using Bns.Core;
using System.Net;
using System.Text;
using System.Text.Json;
int checks=0;
void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
void Throws(Action f,string name){checks++;try{f();}catch(ArgumentException){return;}throw new Exception("Expected validation: "+name);}
// User-defined start keys: preserve full virtual-key values and all supported modifier flags.
foreach(uint key in new uint[]{0x41,0x30,0x70,0x7B,0x87,0x20,0xBD,0x60})
    for(uint mods=0;mods<8;mods++){HotkeyBinding.Validate(key,mods);Check(true,"Custom key accepted");}
foreach(uint key in new uint[]{0,0x1B,0x10,0x11,0x12,0x5B,0x5C,0xA0,0xFF})
    Throws(()=>HotkeyBinding.Validate(key,0),"Reserved/unsupported key");
Throws(()=>HotkeyBinding.Validate(0x41,8),"Win modifier rejected");
Throws(()=>HotkeyBinding.Validate(0x41,0x4000),"Internal registration flag rejected in user settings");
string Letters(string text)=>string.Concat(KoreanKeys.Convert(text).Select(k=>k.Shift?k.Label.Replace("Shift+","").ToUpperInvariant():k.Label=="[空格]"?" ":k.Label));
foreach(var pair in new Dictionary<string,string>{["안녕하세요"]="dkssudgktpdy",["감사합니다"]="rkatkgkqslek",["홍문수"]="ghdanstn",["수호수"]="tnghtn",["광혼석"]="rhkdghstjr",["값"]="rkqt",["까"]="Rk",["예"]="dP",["얘"]="dO",["괜찮아요"]="rhoscksgdkdy"})
    Check(Letters(pair.Key)==pair.Value,"Wrong keyboard mapping: "+pair.Key);
Check(Letters("안녕하세요".Normalize(NormalizationForm.FormD))=="dkssudgktpdy","NFD input");
Check(KoreanKeys.Describe(KoreanKeys.Convert("안녕 100개? ")).Contains("[空格]"),"Explicit spaces");
Check(KoreanKeys.Convert("?").Single().Scan==0x35&&KoreanKeys.Convert("?").Single().Shift,"Question mark is Shift+slash");
Check(KoreanKeys.Convert("，。！？").Count==4,"Full-width punctuation");
Check(KoreanKeys.Convert("ㄳㄵㅘㅢ").Count==8,"Compound compatibility jamo");
Throws(()=>KoreanKeys.Convert("hello"),"Latin under Korean IME");
Throws(()=>KoreanKeys.Convert("안녕\n"),"Never Enter");
Throws(()=>KoreanKeys.Convert("안녕\t"),"Never Tab");
Throws(()=>KoreanKeys.Convert("你好"),"CJK not silently dropped");
Throws(()=>KoreanKeys.Convert("안녕🙂"),"Emoji rejected");
Throws(()=>KoreanKeys.Convert(""),"Empty reply");
Throws(()=>KoreanKeys.Convert(new string('가',601)),"Length bound");
for(int i=0;i<11172;i++){
    var keys=KoreanKeys.Convert(((char)(0xAC00+i)).ToString());
    Check(keys.Count>=2&&keys.Count<=7,"All modern syllables representable");
    Check(keys.All(k=>k.Scan!=0x1C&&k.Scan!=0x0F&&k.Scan!=0x1D&&k.Scan!=0x38),"Never Enter/Tab/Ctrl/Alt in syllable");
}
Check(ApiClient.Endpoint("https://api.deepseek.com/")=="https://api.deepseek.com/chat/completions","Base URL");
Check(ApiClient.Endpoint("https://example.com/v1")=="https://example.com/v1/chat/completions","Custom OpenAI-compatible base");
Check(ApiClient.Endpoint("https://example.com/v1/chat/completions/")=="https://example.com/v1/chat/completions","Full endpoint");
foreach(var bad in new[]{"http://example.com","https://u:p@example.com","https://example.com?a=1","https://example.com/#frag","garbage"})Throws(()=>ApiClient.Endpoint(bad),"Bad endpoint "+bad);
var handler=new Stub();using var http=new HttpClient(handler);var api=new ApiClient(http);var opts=new ApiOptions();
handler.Body="{\"messages\":[{\"source\":\"딜리 홍문수 4000 ㅅㅅ\",\"translation\":\"收洪门水4000金\"}]}";
var png=new byte[]{137,80,78,71};
var lines=await api.Translate(opts,"not-a-real-key",png,CancellationToken.None);
Check(lines.Count==1&&lines[0].Translation=="收洪门水4000金","Vision response");
using(var request=JsonDocument.Parse(handler.Request!)){
    var image=request.RootElement.GetProperty("messages")[1].GetProperty("content")[1].GetProperty("image_url");
    Check(image.GetProperty("url").GetString()=="data:image/png;base64,"+Convert.ToBase64String(png),"PNG sent directly");
    Check(image.GetProperty("detail").GetString()=="original","Original image resolution");
    Check(request.RootElement.GetProperty("thinking").GetProperty("type").GetString()=="disabled","Thinking off");
}
Check(handler.Address=="https://api.deepseek.com/chat/completions","Destination");
handler.Body="{\"korean\":\"감사합니다\",\"chinese\":\"谢谢\"}";
var reply=await api.MakeReply(opts,"not-a-real-key","谢谢","홍문수 4000",CancellationToken.None);
Check(reply.Korean=="감사합니다"&&reply.Chinese=="谢谢","Reply JSON parsing");
Check(handler.Request!.Contains("chat_context"),"Context encoded as data");
handler.Body="bad json";
try{await api.MakeReply(opts,"not-a-real-key","你好",null,CancellationToken.None);throw new Exception("Expected malformed response failure");}catch(ApiException){checks++;}
handler.Code=HttpStatusCode.Unauthorized;
try{await api.MakeReply(opts,"not-a-real-key","你好",null,CancellationToken.None);throw new Exception("Expected 401");}catch(ApiException e){Check(e.Message.Contains("401")&&!e.Message.Contains("not-a-real-key"),"Sanitized auth error");}
handler.Code=HttpStatusCode.OK;handler.Body="{\"korean\":\"안녕\",\"chinese\":\"你好\"}";
handler.Length=true;
try{await api.MakeReply(opts,"not-a-real-key","你好",null,CancellationToken.None);throw new Exception("Expected truncated response failure");}catch(ApiException){checks++;}
// A focus switch permits no subsequent key-down operation.
int sent=0;
try{await InputRunner.Run(KoreanKeys.Convert("안녕"),20,()=>sent==0,_=>sent++,CancellationToken.None);throw new Exception("Expected focus stop");}
catch(OperationCanceledException){Check(sent==1,"Stops after target focus changes");}
using(var cancel=new CancellationTokenSource()){
    sent=0;
    try{await InputRunner.Run(KoreanKeys.Convert("안녕"),20,()=>true,_=>{sent++;cancel.Cancel();},cancel.Token);throw new Exception("Expected Esc cancellation");}
    catch(OperationCanceledException){Check(sent==1,"Cancellation prevents subsequent keys");}
}
sent=0;
await InputRunner.Run(KoreanKeys.Convert("가"),20,()=>true,_=>sent++,CancellationToken.None);
Check(sent==2,"Each decomposed physical stroke sent once");
Console.WriteLine($"PASS: {checks} checks (all 11,172 Hangul syllables, known phrases, Shift/space/punctuation, unsafe characters, direct PNG request, reply/context, endpoint validation, API errors)");
class Stub:HttpMessageHandler
{
    public string Body="{}",Request="",Address="";public HttpStatusCode Code=HttpStatusCode.OK;public bool Length;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
        Request=await request.Content!.ReadAsStringAsync(token);Address=request.RequestUri!.ToString();
        var outer=JsonSerializer.Serialize(new {choices=new[]{new {finish_reason=Length?"length":"stop",message=new {content=Body}}}});
        return new HttpResponseMessage(Code){Content=new StringContent(outer)};
    }
}
