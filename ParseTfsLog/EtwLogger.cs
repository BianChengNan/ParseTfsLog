using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.Tracing;

public class EtwLogger : EventSource
{
    public static EtwLogger Instance = new EtwLogger();

    public class Keywords
    {
        public const EventKeywords Loop = (EventKeywords)1;
        public const EventKeywords Method = (EventKeywords)2;
        public const EventKeywords Message = (EventKeywords)4;
    }

    [Event(1, Level = EventLevel.Verbose, Keywords = Keywords.Loop, Message = "Loop {0} iteration {1}")]
    public void LoopIteration(string loopTitle, int iteration)
    {
        WriteEvent(1, loopTitle, iteration);
    }

    [Event(2, Level = EventLevel.Informational, Keywords = Keywords.Loop,
    Message = "Loop {0} begin")]
    public void LoopBegin(string loopTitle)
    {
        WriteEvent(2, loopTitle);
    }

    [Event(3, Level = EventLevel.Informational, Keywords = Keywords.Loop,
    Message = "Loop {0} done")]
    public void LoopDone(string loopTitle)
    {
        WriteEvent(3, loopTitle);
    }

    [Event(4, Level = EventLevel.Informational, Keywords = Keywords.Method, Message = "Method {0} done")]
    public void MethodDone([System.Runtime.CompilerServices.CallerMemberName] string methodName = null)
    {
        WriteEvent(4, methodName);
    }

    [Event(5, Level = EventLevel.Informational, Keywords = Keywords.Message, Message = "Method {0} done")]
    public void TraceMessage(string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
    {
        var msg = string.Format("{0}, [{1}], [{2}:{3}]", message, memberName, sourceFilePath, sourceLineNumber);
        WriteEvent(5, msg);
    }
}
