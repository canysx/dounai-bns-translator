namespace Bns.Core;
public static class InputRunner
{
    public static async Task Run(IReadOnlyList<KeyStroke> plan,int delayMs,Func<bool> mayContinue,Action<KeyStroke> tap,CancellationToken cancel)
    {
        foreach(var stroke in plan)
        {
            cancel.ThrowIfCancellationRequested();
            if(!mayContinue())throw new OperationCanceledException("目标窗口或按键状态已变化。");
            tap(stroke);
            await Task.Delay(Math.Clamp(delayMs,20,300),cancel).ConfigureAwait(false);
        }
    }
}
