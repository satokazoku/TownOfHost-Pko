using System;

namespace TownOfHost.Modules
{
    class LogHandler : ILogHandler
    {
        public string Tag { get; }
        public bool EscapeCRLF { get; }
        public LogHandler(string tag, bool escapeCRLF = true)
        {
            Tag = tag;
            EscapeCRLF = escapeCRLF;
        }

        public void Info(string text)
            => Logger.Info(text, Tag, EscapeCRLF);
        public void Warn(string text)
            => Logger.Warn(text, Tag, EscapeCRLF);
        public void Error(string text)
            => Logger.Error(text, Tag, EscapeCRLF);
        public void Fatal(string text)
            => Logger.Fatal(text, Tag, EscapeCRLF);
        public void Msg(string text)
            => Logger.Msg(text, Tag, EscapeCRLF);
        public void Exception(Exception ex)
            => Logger.Exception(ex, Tag);
    }
}