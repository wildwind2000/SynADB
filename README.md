# 文件同步工具

该工具使用 ADB（Android Debug Bridge）进行文件同步，解决直接使用 ADB 时部分中文路径解析错误的问题。

## 安装

请确保您已安装 .NET SDK 和 ADB 工具。

## 使用方法

### 命令

该工具支持以下命令：

- `push`：将本地文件或目录推送到设备。
- `pull`：从设备拉取文件或目录到本地。

### 参数

- `cmd`：命令类型，可选值为 `push` 或 `pull`。
- `paths`：源路径和目标路径。如果只有源路径，则目标路径为当前目录。
- `--force` 或 `-f`：强制复制，不管文件修改时间是否一致。

### 示例

1. **推送文件到设备**

   ```bash
   synadb push /path/to/local/file /path/to/device/directory
   ```


2. **从设备拉取文件到本地**

   ```bash
   synadb pull /path/to/device/file /path/to/local/directory
   ```

   如果目标路径未指定，则默认使用当前目录：

   ```bash
   synadb pull /path/to/device/file
   ```

3. **强制复制**

   使用 `--force` 或 `-f` 选项可以强制复制文件，不管文件修改时间是否一致：

   ```bash
   synadb push /path/to/local/file /path/to/device/directory --force
   ```

   或者

   ```bash
   synadb pull /path/to/device/file /path/to/local/directory -f
   ```

## 贡献

欢迎提交问题和拉取请求！请确保遵循项目的贡献指南。

## 许可证

该项目采用 MIT 许可证，详细信息请参见 LICENSE 文件。