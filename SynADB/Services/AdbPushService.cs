using System.Diagnostics;
using System.Threading;

namespace SYNADB.Services
{
    public class AdbPushService(bool force, CancellationToken cancellationToken) : AdbSyncService(force, cancellationToken)
    {
        public override async Task Sync(string sourcePath, string targetPath)
        {
            CheckAdbConnection();

            if (!Directory.Exists(sourcePath) && !File.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"源路径不存在: {sourcePath}");
            }

            // 如果源路径是文件，直接复制
            if (File.Exists(sourcePath))
            {
                await PushFileIfNewerAsync(sourcePath, targetPath);
                return;
            }

            var childPaths = Directory.GetDirectories(sourcePath);
            var childNames = childPaths.Select(c => Path.GetFileName(c)).ToArray();
            await LoadRemoteStructureAsync(targetPath,childNames);

            await CopyDataAsync(sourcePath, targetPath);
        }
        /// <summary>
        /// 获取远程设备的目录和文件信息，首先只读取目标目录下的一级文件和目录（不包含子目录以下的信息），再读取指定的子目录下的文件和目录（包含子目录）。
        /// 该函数用于推送到目标设备的目录，防止推送到的目标目录包含太多其它目录，例如目标目录是根目录
        /// 
        /// </summary>
        /// <param name="remotePath"></param>
        /// <param name="childDirNames"></param>
        /// <returns></returns>
        private async Task LoadRemoteStructureAsync(string remotePath, string[] childDirNames)
        {
            await LoadRemoteStructureAsync(remotePath, false);
            foreach (var childName in childDirNames)
            {
                var childPath = $"{remotePath}{(remotePath.EndsWith('/')?"":"/")}{childName}";
                await LoadRemoteStructureAsync(childPath, true);
            }
        }


        private async Task CopyDataAsync(string sourcePath, string targetPath)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(sourcePath))
            {
                cancellationToken.ThrowIfCancellationRequested(); // 检查是否被取消

                var relativePath = Path.GetRelativePath(sourcePath, entry);
                var remoteTargetPath = Path.Combine(targetPath, relativePath).Replace("\\", "/");

                if (Directory.Exists(entry))
                {
                    if (!existingDirs.Contains(remoteTargetPath))
                    {
                        await CreateRemoteDirectoryAsync(remoteTargetPath);
                    }
                    await CopyDataAsync(entry, remoteTargetPath);
                }
                else
                {
                    await PushFileIfNewerAsync(entry, remoteTargetPath);
                }
            }
        }

        private async Task CreateRemoteDirectoryAsync(string remotePath)
        {
            var (ExitCode, _) = await ExecuteAdbCommandAsync($"shell mkdir -p \"{remotePath}\"");
            if (ExitCode != 0)
            {
                Console.WriteLine($"创建远程目录失败: {remotePath}");
                Environment.Exit(1); // 退出程序
            }
            existingDirs.Add(remotePath);
        }

        private async Task PushFileIfNewerAsync(string localFile, string remotePath)
        {
            try
            {
                totalFileCount++;
                // 使用 GetLocalFileTime 方法获取本地文件时间
                var localTime = GetLocalFileTime(localFile);

                if (!force && existingFiles.TryGetValue(remotePath, out var remoteFileInfo))
                {
                    // 获取远程文件的时间
                    var remoteTime = remoteFileInfo;

                    if (remoteTime == localTime)
                    {
                        return;
                    }
                }

                // 确保父目录存在
                var remoteDir = Path.GetDirectoryName(remotePath)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(remoteDir) && !existingDirs.Contains(remoteDir))
                {
                    await CreateRemoteDirectoryAsync(remoteDir);
                }

                // 复制文件
                Console.WriteLine($"{remotePath}");
                await ExecuteAdbCommandAsync($"push \"{localFile}\" \"{remotePath}\"");
                existingFiles[remotePath] = localTime; // 更新文件信息

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