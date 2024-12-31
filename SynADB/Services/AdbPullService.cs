using System.Diagnostics;
using System.Threading;

namespace SYNADB.Services
{
    public class AdbPullService(bool force, CancellationToken cancellationToken) : AdbSyncService(force, cancellationToken)
    {
        public override async Task Sync(string sourcePath, string targetPath)
        {
            CheckAdbConnection();

            // 先获取源目录（远程）的结构
            if (!hasLoadRemote)
            {
                if (!await LoadRemoteStructureAsync(sourcePath,true))
                {
                    Console.WriteLine("找不到源目录或源文件");
                    Environment.Exit(1);
                }
            }

            if (existingFiles.ContainsKey(sourcePath)) //文件
            {
                if (Directory.Exists(targetPath) || IsDirectoryPath(targetPath))
                {
                    await PullFileIfNewerAsync(sourcePath, Path.Combine(targetPath, Path.GetFileName(sourcePath)));
                }
                else
                {
                    await PullFileIfNewerAsync(sourcePath, targetPath);
                }
                return;
            }

            // 创建本地目标目录
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            // 遍历远程文件和目录
            foreach (var entry in existingFiles.Keys.Union(existingDirs))
            {
                cancellationToken.ThrowIfCancellationRequested(); // 检查是否被取消

                if (!entry.StartsWith(sourcePath)) continue;

                var relativePath = Path.GetRelativePath(sourcePath, entry);
                var localTargetPath = Path.Combine(targetPath, relativePath);

                if (existingDirs.Contains(entry))
                {
                    // 如果是目录，创建本地目录
                    if (!Directory.Exists(localTargetPath))
                    {
                        Directory.CreateDirectory(localTargetPath);
                    }
                }
                else
                {
                    // 如果是文件，检查时间戳后复制
                    await PullFileIfNewerAsync(entry, localTargetPath);
                }
            }
        }

        private async Task PullFileIfNewerAsync(string remotePath, string localFile)
        {
            try
            {
                totalFileCount++;
                var remoteTime = existingFiles[remotePath];
                if (!force && File.Exists(localFile))
                {
                    // 使用 GetLocalFileTime 方法获取本地文件时间
                    var localTime = GetLocalFileTime(localFile);

                    // 比较时间
                    if (localTime == remoteTime) return;
                }

                // 确保目标目录存在
                var localDir = Path.GetDirectoryName(localFile);
                if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                {
                    Directory.CreateDirectory(localDir);
                }

                // 复制文件
                Console.WriteLine($"{localFile}");
                await ExecuteAdbCommandAsync($"pull \"{remotePath}\" \"{localFile}\"");

                // 设置本地文件时间与远程文件一致
                File.SetLastWriteTime(localFile, remoteTime);

                // 增加更新计数
                updatedFileCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"复制文件失败: {ex.Message}");
                throw;
            }
        }
    }
} 