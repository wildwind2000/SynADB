using System.CommandLine;
using SYNADB.Services;

namespace SYNADB
{
    class Program
    {
        enum CommandType
        {
            Push,
            Pull
        }
        static async Task<int> Main(string[] args)
        {
            // 设置控制台输出编码为 UTF-8
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var rootCommand = new RootCommand("文件同步工具，使用 adb 工具进行文件同步。解决直接使用 adb 时部分中文路径解析错误的问题。");
            var cmdArg = new Argument<CommandType>("cmd", "命令类型，可选值为 push 或 pull");
            var pathArgs = new Argument<string[]>("paths", "源路径和目标路径，如果只有源路径，则目标路径为当前目录") { Arity = ArgumentArity.OneOrMore };
            var forceOption = new Option<bool>(["--force","-f"], "强制复制，不管文件修改时间是否一致");

            rootCommand.AddArgument(cmdArg);
            rootCommand.AddArgument(pathArgs);
            rootCommand.AddOption(forceOption);

            rootCommand.SetHandler(async (CommandType cmd, string[] paths, bool force) =>
            {
                await ExecuteWithErrorHandling(async (cancellationToken) =>
                {
                    AdbSyncService service = cmd == CommandType.Push ? new AdbPushService(force, cancellationToken) : new AdbPullService(force, cancellationToken);
                    var source = paths[0];
                    if(cmd == CommandType.Push && (paths.Length==1 || !paths[1].StartsWith('/')))
                        throw new Exception("push 情况下必须有目标路径，并且目标路径以 / 开头");
                    var target = paths.Length > 1 ? paths[1] : ".";
                    await service.Sync(source, target);
                    service.OutputTotalMessage();
                });
            }, cmdArg, pathArgs, forceOption);
            return await rootCommand.InvokeAsync(args);
        }

        private static async Task ExecuteWithErrorHandling(Func<CancellationToken, Task> action)
        {
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("正在退出程序...");
                cts.Cancel();
                e.Cancel = true;
            };

            try
            {
                await action(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("操作已取消");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Console.WriteLine(AnsiColor.Color($"错误: {ex.Message}", AnsiColor.Red));
                Environment.Exit(1);
            }
        }
    }
}
