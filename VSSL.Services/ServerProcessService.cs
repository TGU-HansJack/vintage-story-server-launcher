using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     服务器进程服务默认实现
/// </summary>
public partial class ServerProcessService : IServerProcessService
{
    private readonly SemaphoreSlim _processGate = new(1, 1);
    private Process? _process;
    private InstanceProfile? _currentProfile;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    private ServerRuntimeStatus _currentStatus = new();
    private int _onlinePlayers;

    /// <inheritdoc />
    public event EventHandler<string>? OutputReceived;

    /// <inheritdoc />
    public event EventHandler<ServerRuntimeStatus>? StatusChanged;

    /// <inheritdoc />
    public ServerRuntimeStatus GetCurrentStatus()
    {
        return _currentStatus;
    }

    /// <inheritdoc />
    public async Task StartAsync(InstanceProfile profile, CancellationToken cancellationToken = default)
    {
        await _processGate.WaitAsync(cancellationToken);
        try
        {
            if (_process is { HasExited: false })
                throw new InvalidOperationException("服务器已在运行中。");

            WorkspacePathHelper.EnsureWorkspace();

            var installPath = WorkspacePathHelper.GetServerInstallPath(profile.Version);
            var serverExe = Path.Combine(installPath, "VintagestoryServer.exe");
            if (!File.Exists(serverExe))
                throw new InvalidOperationException($"未找到服务端程序：{serverExe}");

            Directory.CreateDirectory(profile.DirectoryPath);
            var logsPath = WorkspacePathHelper.GetProfileLogsPath(profile.DirectoryPath);
            Directory.CreateDirectory(logsPath);

            // 自动修复旧版 Launcher 生成的极简配置（会导致 suplayer 组缺失并秒退）。
            ServerConfigBootstrapper.EnsureGenerated(installPath, profile.DirectoryPath);
            PrepareSaveFileForStart(profile);
            SqliteConnection.ClearAllPools();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = serverExe,
                    WorkingDirectory = installPath,
                    Arguments = $"--dataPath \"{profile.DirectoryPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += OnOutputDataReceived;
            process.ErrorDataReceived += OnOutputDataReceived;
            process.Exited += OnProcessExited;

            if (!process.Start())
                throw new InvalidOperationException("启动服务端失败。");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _process = process;
            _currentProfile = profile;
            _onlinePlayers = 0;

            _monitorCts?.Cancel();
            _monitorCts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoopAsync(_monitorCts.Token), CancellationToken.None);

            UpdateStatus(new ServerRuntimeStatus
            {
                IsRunning = true,
                ProcessId = process.Id,
                StartedAtUtc = DateTimeOffset.UtcNow,
                ProfileId = profile.Id,
                MemoryBytes = process.WorkingSet64,
                OnlinePlayers = 0
            });

            OutputReceived?.Invoke(this, $"[system] 服务器进程已启动，PID={process.Id}");
        }
        finally
        {
            _processGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        await _processGate.WaitAsync(cancellationToken);
        try
        {
            if (_process is null || _process.HasExited)
                return;

            try
            {
                await SendCommandInternalAsync("/stop", cancellationToken);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(gracefulTimeout);
                await _process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(cancellationToken);
                OutputReceived?.Invoke(this, "[system] 服务器未在超时时间内退出，已强制终止。");
            }
        }
        finally
        {
            _processGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        await _processGate.WaitAsync(cancellationToken);
        try
        {
            await SendCommandInternalAsync(command, cancellationToken);
        }
        finally
        {
            _processGate.Release();
        }
    }

    private async Task SendCommandInternalAsync(string command, CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited)
            throw new InvalidOperationException("服务器未运行。");

        var normalized = string.IsNullOrWhiteSpace(command) ? string.Empty : command.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("命令不能为空。");
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        await _process.StandardInput.WriteLineAsync(normalized.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);
        OutputReceived?.Invoke(this, $"[cmd] {normalized}");
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_process is null || _process.HasExited)
                    break;

                var startedAt = _currentStatus.StartedAtUtc ?? DateTimeOffset.UtcNow;
                UpdateStatus(new ServerRuntimeStatus
                {
                    IsRunning = true,
                    ProcessId = _process.Id,
                    StartedAtUtc = startedAt,
                    ProfileId = _currentProfile?.Id,
                    MemoryBytes = _process.WorkingSet64,
                    OnlinePlayers = _onlinePlayers
                });

                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(1200, cancellationToken);
            }
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data)) return;

        var line = e.Data;
        TryUpdatePlayerCountByLine(line);
        OutputReceived?.Invoke(this, line);
    }

    private void TryUpdatePlayerCountByLine(string line)
    {
        if (PlayerJoinPattern().IsMatch(line))
        {
            _onlinePlayers = Math.Max(0, _onlinePlayers + 1);
            PublishPlayerCountOnly();
            return;
        }

        if (PlayerLeavePattern().IsMatch(line))
        {
            _onlinePlayers = Math.Max(0, _onlinePlayers - 1);
            PublishPlayerCountOnly();
            return;
        }

        var onlineMatch = OnlineCountPattern().Match(line);
        if (onlineMatch.Success)
        {
            var countCapture = onlineMatch.Groups["count"].Captures;
            var rawCount = countCapture.Count > 0 ? countCapture[^1].Value : onlineMatch.Groups["count"].Value;
            if (int.TryParse(rawCount, out var count))
            {
                _onlinePlayers = Math.Max(0, count);
                PublishPlayerCountOnly();
            }
        }
    }

    private void PublishPlayerCountOnly()
    {
        if (!_currentStatus.IsRunning) return;

        UpdateStatus(new ServerRuntimeStatus
        {
            IsRunning = true,
            ProcessId = _currentStatus.ProcessId,
            StartedAtUtc = _currentStatus.StartedAtUtc,
            ProfileId = _currentStatus.ProfileId,
            MemoryBytes = _currentStatus.MemoryBytes,
            OnlinePlayers = _onlinePlayers
        });
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
        _monitorTask = null;

        var previousProfileId = _currentProfile?.Id;
        _onlinePlayers = 0;
        UpdateStatus(new ServerRuntimeStatus
        {
            IsRunning = false,
            ProcessId = null,
            StartedAtUtc = null,
            ProfileId = previousProfileId,
            MemoryBytes = 0,
            OnlinePlayers = 0
        });

        OutputReceived?.Invoke(this, "[system] 服务器进程已退出。");

        if (_process is not null)
        {
            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.ErrorDataReceived -= OnOutputDataReceived;
            _process.Exited -= OnProcessExited;
            _process.Dispose();
            _process = null;
        }
    }

    private void UpdateStatus(ServerRuntimeStatus status)
    {
        _currentStatus = status;
        StatusChanged?.Invoke(this, status);
    }

    private void PrepareSaveFileForStart(InstanceProfile profile)
    {
        var configuredSavePath = TryReadSaveFileLocation(profile.DirectoryPath);
        var savePath = string.IsNullOrWhiteSpace(configuredSavePath)
            ? profile.ActiveSaveFile
            : configuredSavePath;
        if (string.IsNullOrWhiteSpace(savePath))
            return;

        string fullSavePath;
        try
        {
            fullSavePath = Path.GetFullPath(savePath);
        }
        catch
        {
            return;
        }

        profile.ActiveSaveFile = fullSavePath;
        profile.SaveDirectory = Path.GetDirectoryName(fullSavePath) ?? profile.SaveDirectory;

        var saveDirectory = Path.GetDirectoryName(fullSavePath);
        if (!string.IsNullOrWhiteSpace(saveDirectory))
            Directory.CreateDirectory(saveDirectory);

        if (!File.Exists(fullSavePath))
            return;

        var saveFileInfo = new FileInfo(fullSavePath);
        if (saveFileInfo.Length == 0)
        {
            File.Delete(fullSavePath);
            OutputReceived?.Invoke(this, $"[system] 检测到空存档文件，已删除并允许服务器重新生成：{fullSavePath}");
            return;
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = fullSavePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                Cache = SqliteCacheMode.Private
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();

            var tables = ReadTables(connection);
            var hasChunks = tables.Contains("chunks", StringComparer.OrdinalIgnoreCase);
            var hasChunk = tables.Contains("chunk", StringComparer.OrdinalIgnoreCase);

            // 兼容旧的错误迁移：曾将 chunk 表改名为 chunks，导致 VS 服务器无法写入存档。
            // 这里仅在检测到 chunks 存在、chunk 缺失时回迁；不再执行 chunk -> chunks 的迁移。
            if (hasChunks && !hasChunk)
            {
                var backupPath = $"{fullSavePath}.bak-fix-{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(fullSavePath, backupPath, overwrite: false);

                using var renameCommand = connection.CreateCommand();
                renameCommand.CommandText = "ALTER TABLE chunks RENAME TO chunk;";
                renameCommand.ExecuteNonQuery();

                OutputReceived?.Invoke(this,
                    $"[system] 已自动修复存档表名 chunks -> chunk，并创建备份：{backupPath}");
            }
        }
        catch (SqliteException ex)
        {
            OutputReceived?.Invoke(this, $"[system] 存档预检查跳过（SQLite）：{ex.Message}");
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke(this, $"[system] 存档预检查跳过：{ex.Message}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private static HashSet<string> ReadTables(SqliteConnection connection)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static string TryReadSaveFileLocation(string profileDirectoryPath)
    {
        try
        {
            var configPath = WorkspacePathHelper.GetProfileConfigPath(profileDirectoryPath);
            if (!File.Exists(configPath))
                return string.Empty;

            using var stream = File.OpenRead(configPath);
            using var json = JsonDocument.Parse(stream);

            if (!json.RootElement.TryGetProperty("WorldConfig", out var worldConfigElement) ||
                worldConfigElement.ValueKind != JsonValueKind.Object)
                return string.Empty;

            if (!worldConfigElement.TryGetProperty("SaveFileLocation", out var saveFileElement) ||
                saveFileElement.ValueKind != JsonValueKind.String)
                return string.Empty;

            return saveFileElement.GetString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    [GeneratedRegex(@"\[(?:Server\s+)?Event\].*\s+joins\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlayerJoinPattern();

    [GeneratedRegex(@"\[(?:Server\s+)?Event\].*(left\.|leaves\.)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlayerLeavePattern();

    [GeneratedRegex(@"(?:\b(?:online|players)\b\D+(?<count>\d+))|(?:(?<count>\d+)\D*player(?:s|\(s\))?\D*online)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OnlineCountPattern();
}
