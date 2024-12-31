namespace SYNADB.Services
{
    public static class AnsiColor
    {
        // ANSI 颜色代码
        public const string Reset = "\u001b[0m";
        public const string Black = "\u001b[30m";
        public const string Red = "\u001b[31m";
        public const string Green = "\u001b[32m";
        public const string Yellow = "\u001b[33m";
        public const string Blue = "\u001b[34m";
        public const string Magenta = "\u001b[35m";
        public const string Cyan = "\u001b[36m";
        public const string White = "\u001b[37m";

        // 输出带颜色的文本
        public static string Color(string text, string color)
        {
            return $"{color}{text}{Reset}";
        }
    }
} 