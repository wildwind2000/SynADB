using System.Diagnostics;

namespace SYNADB.Services
{
    public abstract class AdbSyncService(bool force, CancellationToken cancellationToken)
    {
        public CancellationToken cancellationToken = cancellationToken;
        protected readonly string adbPath = "adb";
        protected readonly Dictionary<string, DateTime> existingFiles = [];
        protected readonly HashSet<string> existingDirs = [];
        protected bool hasLoadRemote = false;
        public bool force = force;
        public int updatedFileCount = 0;
        public int totalFileCount = 0;
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public abstract Task Sync(string sourcePath, string targetPath);

        public void OutputTotalMessage()
        {
            Console.WriteLine($"总共文件数: {AnsiColor.Color(totalFileCount.ToString(), AnsiColor.Green)}"); // 绿色
            Console.WriteLine($"更新文件数: {AnsiColor.Color(updatedFileCount.ToString(), AnsiColor.Green)}"); // 绿色
            Console.WriteLine($"执行耗时: {AnsiColor.Color(stopwatch.Elapsed.TotalSeconds.ToString("F2"), AnsiColor.Green)}秒"); // 绿色
        }

        /// <summary>
        /// 存储远程设备的目录中的文件和子目录信息
        /// </summary>
        /// <param name="remotePath"></param>
        /// <param name="wholeSubdir">是否包含子目录</param>
        /// <returns></returns>
        protected async Task<bool> LoadRemoteStructureAsync(string remotePath,bool wholeSubdir)
        {
            try
            {
                // 将空格替换为 \ ，防止命令执行失败
                if(!remotePath.EndsWith('/'))remotePath += "/";
                var cmdLine = $"shell ls -{(wholeSubdir ? "R":"")}ll \"{remotePath.Replace(" ", "\\ ")}\" 2>/dev/null";
                var (ExitCode, Output) = await ExecuteAdbCommandAsync(cmdLine);
                if (ExitCode != 0 || string.IsNullOrEmpty(Output)) return false;

                var currentDir = remotePath;
                var lines = Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();

                    // 检查是否是目录头部
                    if (line.EndsWith(':'))
                    {
                        currentDir = line.TrimEnd(':');
                        existingDirs.Add(currentDir);
                        continue;
                    }
                    AnalyseExist(line, currentDir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取远程目录结构失败: {ex.Message}");
                Environment.Exit(1); // 退出程序
            }
            return true;
        }

        private void AnalyseExist(string line,string currentDir)
        {
            // 跳过总计行
            if (line.StartsWith("total ")) return;

            // 解析 ls -ll 的输出
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 8) return; // 至少需要8个部分

            var permissions = parts[0];
            var datePart = $"{parts[5]} {parts[6]} {parts[7]}"; // 获取完整的日期和时间部分
            var name = string.Join(" ", parts.Skip(8)); // 文件名可能包含空格

            // 构建完整路径
            var fullPath = Path.Combine(currentDir, name).Replace("\\", "/");

            // 根据权限第一个字符判断类型：d 表示目录，- 表示文件
            if (permissions.StartsWith('d'))
            {
                existingDirs.Add(fullPath);
            }
            else if (permissions.StartsWith('-'))
            {
                var fileTime = ParseDateTime(datePart);
                existingFiles[fullPath] = fileTime; // 存储时间
            }
        }

        protected async Task<(int ExitCode, string Output)> ExecuteAdbCommandAsync(string command)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var output = new List<string>();
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.Add(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync();

            return (process.ExitCode, string.Join(Environment.NewLine, output));
        }

        protected static bool IsDirectoryPath(string Name) => (Name.EndsWith('/') || Name.EndsWith('\\'));

        protected static DateTime ParseDateTime(string dateTimeStr)
        {
            try
            {
                var parts = dateTimeStr.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) return DateTime.MinValue; // 至少需要6个部分

                // 解析日期和时间
                var datePart = parts[0]; // YYYY-MM-DD
                var timePart = parts[1]; // HH:MM:SS.NNNNNNNNN
                var timezonePart = parts[2]; // +ZZZZ

                // 处理时间部分，提取秒
                var timeParts = timePart.Split(':');
                if (timeParts.Length < 3) return DateTime.MinValue;

                var year = int.Parse(datePart[..4]);
                var month = int.Parse(datePart.Substring(5, 2));
                var day = int.Parse(datePart.Substring(8, 2));
                var hour = int.Parse(timeParts[0]);
                var minute = int.Parse(timeParts[1]);
                var second = int.Parse(timeParts[2].Split('.')[0]); // 只取秒，不需要纳秒

                return new DateTime(year, month, day, hour, minute, second);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析时间失败: {dateTimeStr}, {ex.Message}");
                return DateTime.MinValue; // 如果解析失败，返回一个很旧的时间，确保文件会被更新
            }
        }

        protected static DateTime GetLocalFileTime(string filePath)
        {
            var localTime = File.GetLastWriteTime(filePath);
            return new DateTime(localTime.Year, localTime.Month, localTime.Day, localTime.Hour, localTime.Minute, localTime.Second);
        }

        protected void CheckAdbConnection()
        {
            var (ExitCode, Output) = ExecuteAdbCommandAsync("devices").Result; // 执行 adb devices 命令
            var deviceCount = 0;
            if (ExitCode == 0)
            {
                var lines = Output.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
                deviceCount = lines.Select(line => line.Trim().Split(['\t'], StringSplitOptions.RemoveEmptyEntries))
                    .Count(parts => parts.Length == 2 && parts[1] == "device");
            }
            if (deviceCount == 0)
            {
                Console.WriteLine("没有检测到连接的设备，请确保设备已连接并启用 USB 调试。");
                Environment.Exit(1); // 退出程序
            }
            else if(deviceCount > 1){
                Console.WriteLine("USB 连接了多个设备，必须只能是一个设备");
                Environment.Exit(1); // 退出程序
            }
        }
    }
}