using System.Diagnostics;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

/// <summary>
///     服务器进程服务默认实现
/// </summary>
public partial class ServerProcessService : IServerProcessService
{
    private readonly SemaphoreSlim _processGate = new(1, 1);
    private readonly IInstanceProfileService? _profileService;
    private readonly ILogger<ServerProcessService> _logger;
    private Process? _process;
    private InstanceProfile? _currentProfile;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private bool _canWriteStandardInput;

    private ServerRuntimeStatus _currentStatus = new();
    private int _onlinePlayers;

    public ServerProcessService()
        : this(null, NullLogger<ServerProcessService>.Instance)
    {
    }

    public ServerProcessService(
        IInstanceProfileService? profileService,
        ILogger<ServerProcessService>? logger = null)
    {
        _profileService = profileService;
        _logger = logger ?? NullLogger<ServerProcessService>.Instance;
    }

    /// <inheritdoc />
    public event EventHandler<string>? OutputReceived;

    /// <inheritdoc />
    public event EventHandler<ServerRuntimeStatus>? StatusChanged;

    /// <inheritdoc />
    public ServerRuntimeStatus GetCurrentStatus()
    {
        if (!_processGate.Wait(0))
            return _currentStatus;

        try
        {
            ClearTrackedProcessIfTerminated();

            if (_process is null)
            {
                TryAttachToExistingWorkspaceServerProcess(preferredProfile: null, emitOutput: false);
            }

            return _currentStatus;
        }
        finally
        {
            _processGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StartAsync(InstanceProfile profile, CancellationToken cancellationToken = default)
    {
        await _processGate.WaitAsync(cancellationToken);
        try
        {
            ClearTrackedProcessIfTerminated();

            if (_process is { HasExited: false })
                throw new InvalidOperationException("服务器已在运行中。");

            WorkspacePathHelper.EnsureWorkspace();

            if (TryAttachToExistingWorkspaceServerProcess(profile, emitOutput: true))
            {
                if (_currentProfile?.Id.Equals(profile.Id, StringComparison.OrdinalIgnoreCase) == true)
                    return;

                throw new InvalidOperationException(
                    $"检测到已有服务端进程正在运行（PID={_currentStatus.ProcessId}），已接管其状态。请先停止当前服务端后再启动其他档案。");
            }

            var installPath = WorkspacePathHelper.GetServerInstallPath(profile.Version);
            var serverExe = Path.Combine(installPath, "VintagestoryServer.exe");
            if (!File.Exists(serverExe))
                throw new InvalidOperationException($"未找到服务端程序：{serverExe}");

            _logger.LogInformation(
                "Starting Vintage Story server. ProfileId={ProfileId}, ProfileName={ProfileName}, Version={Version}, DataPath={DataPath}.",
                profile.Id,
                profile.Name,
                profile.Version,
                profile.DirectoryPath);

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
            _canWriteStandardInput = true;
            _onlinePlayers = 0;

            StartMonitorLoop();

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
            _logger.LogInformation("Vintage Story server process started. ProcessId={ProcessId}.", process.Id);
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
            ClearTrackedProcessIfTerminated();

            var process = _process;
            if (process is null || IsProcessTerminated(process))
            {
                if (!TryAttachToExistingWorkspaceServerProcess(preferredProfile: null, emitOutput: true))
                    return;

                process = _process;
                if (process is null || IsProcessTerminated(process))
                    return;
            }

            var trackedProcessId = TryGetProcessId(process);

            try
            {
                await SendCommandInternalAsync("/stop", cancellationToken);
            }
            catch (Exception ex)
            {
                // stdin 写入失败时，继续走强制终止兜底，避免出现“点击停止但进程仍存活”。
                OutputReceived?.Invoke(this, $"[system] 发送停服命令失败，将尝试强制终止：{ex.Message}");
                _logger.LogWarning(ex, "Failed to send graceful stop command to server process {ProcessId}.", trackedProcessId);
            }

            if (_canWriteStandardInput && !IsProcessTerminated(process))
            {
                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(gracefulTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : gracefulTimeout);
                    await process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Graceful 停止超时，继续进入强制终止。
                }
                catch (ObjectDisposedException)
                {
                    // 进程退出事件可能已释放 Process 对象，按已退出处理。
                }
            }

            if (!IsProcessTerminated(process))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken);
                    OutputReceived?.Invoke(this, "[system] 服务器未在超时时间内退出，已强制终止。");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"强制终止服务器进程失败：{ex.Message}", ex);
                }
            }

            _canWriteStandardInput = false;
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
        if (!_canWriteStandardInput)
            throw new InvalidOperationException("当前服务端进程不是由本次 VSSL 启动，无法发送控制台命令。请停止并由 VSSL 重新启动后再发送命令。");

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
                var process = _process;
                if (process is null || IsProcessTerminated(process))
                    break;

                var startedAt = _currentStatus.StartedAtUtc ?? DateTimeOffset.UtcNow;
                UpdateStatus(new ServerRuntimeStatus
                {
                    IsRunning = true,
                    ProcessId = TryGetProcessId(process),
                    StartedAtUtc = startedAt,
                    ProfileId = _currentProfile?.Id,
                    MemoryBytes = TryGetWorkingSet64(process),
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
        var previousProfileId = _currentProfile?.Id;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
        _monitorTask = null;
        _canWriteStandardInput = false;
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
        _logger.LogInformation("Vintage Story server process exited. PreviousProfileId={ProfileId}.", previousProfileId);

        if (_process is not null)
        {
            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.ErrorDataReceived -= OnOutputDataReceived;
            _process.Exited -= OnProcessExited;
            _process.Dispose();
            _process = null;
        }
    }

    private void StartMonitorLoop()
    {
        try
        {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _monitorCts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoopAsync(_monitorCts.Token), CancellationToken.None);
    }

    private void ClearTrackedProcessIfTerminated()
    {
        var process = _process;
        if (process is null || !IsProcessTerminated(process))
            return;

        var previousProfileId = _currentProfile?.Id;
        _canWriteStandardInput = false;
        _onlinePlayers = 0;

        try
        {
            process.OutputDataReceived -= OnOutputDataReceived;
            process.ErrorDataReceived -= OnOutputDataReceived;
            process.Exited -= OnProcessExited;
            process.Dispose();
        }
        catch
        {
            // ignore
        }

        _process = null;

        if (_currentStatus.IsRunning)
        {
            UpdateStatus(new ServerRuntimeStatus
            {
                IsRunning = false,
                ProcessId = null,
                StartedAtUtc = null,
                ProfileId = previousProfileId,
                MemoryBytes = 0,
                OnlinePlayers = 0
            });
        }
    }

    private void UpdateStatus(ServerRuntimeStatus status)
    {
        _currentStatus = status;
        StatusChanged?.Invoke(this, status);
    }

    private static bool IsProcessTerminated(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return null;
        }
    }

    private static long TryGetWorkingSet64(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTimeOffset? TryGetStartTimeUtc(Process process)
    {
        try
        {
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch
        {
            return null;
        }
    }

    private bool TryAttachToExistingWorkspaceServerProcess(InstanceProfile? preferredProfile, bool emitOutput)
    {
        var serversRoot = NormalizePath(WorkspacePathHelper.ServersRoot);
        if (string.IsNullOrWhiteSpace(serversRoot))
            return false;

        Process[] candidates;
        try
        {
            candidates = Process.GetProcessesByName("VintagestoryServer");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate VintagestoryServer processes.");
            return false;
        }

        var profiles = SafeGetProfiles();
        Process? selectedProcess = null;
        InstanceProfile? selectedProfile = null;
        var selectedScore = int.MinValue;
        var selectedStartedAt = DateTimeOffset.MinValue;

        foreach (var candidate in candidates)
        {
            var candidateSelected = false;
            try
            {
                var pid = TryGetProcessId(candidate);
                if (!pid.HasValue || IsProcessTerminated(candidate) || !IsWorkspaceServerProcess(candidate, serversRoot))
                    continue;

                var commandLine = TryReadCommandLine(pid.Value);
                var dataPath = TryExtractDataPath(commandLine);
                var version = TryResolveVersionFromExecutable(candidate, serversRoot);
                var profile = ResolveProfileForProcess(preferredProfile, profiles, dataPath, version);
                var score = ScoreProcessMatch(preferredProfile, profile, dataPath, version);
                var startedAt = TryGetStartTimeUtc(candidate) ?? DateTimeOffset.MinValue;

                if (score < selectedScore || score == selectedScore && startedAt <= selectedStartedAt)
                    continue;

                selectedProcess?.Dispose();
                selectedProcess = candidate;
                selectedProfile = profile;
                selectedScore = score;
                selectedStartedAt = startedAt;
                candidateSelected = true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to inspect server process.");
            }
            finally
            {
                if (!candidateSelected)
                    candidate.Dispose();
            }
        }

        if (selectedProcess is null)
            return false;

        try
        {
            AttachToExistingProcess(selectedProcess, selectedProfile, emitOutput);
            return true;
        }
        catch (Exception ex)
        {
            selectedProcess.Dispose();
            _logger.LogDebug(ex, "Failed to attach existing Vintage Story server process.");
            return false;
        }
    }

    private IReadOnlyList<InstanceProfile> SafeGetProfiles()
    {
        if (_profileService is null)
            return [];

        try
        {
            return _profileService.GetProfiles();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read profiles while attaching existing server process.");
            return [];
        }
    }

    private void AttachToExistingProcess(Process process, InstanceProfile? profile, bool emitOutput)
    {
        process.EnableRaisingEvents = true;
        process.Exited += OnProcessExited;

        _process = process;
        _currentProfile = profile;
        _canWriteStandardInput = false;
        _onlinePlayers = 0;
        StartMonitorLoop();

        var processId = TryGetProcessId(process);
        UpdateStatus(new ServerRuntimeStatus
        {
            IsRunning = true,
            ProcessId = processId,
            StartedAtUtc = TryGetStartTimeUtc(process) ?? DateTimeOffset.UtcNow,
            ProfileId = profile?.Id,
            MemoryBytes = TryGetWorkingSet64(process),
            OnlinePlayers = 0
        });

        var profileText = profile is null ? "未识别档案" : $"档案={profile.Name}";
        var message = $"[system] 检测到已在运行的服务端进程并接管状态，PID={processId}，{profileText}。";
        if (emitOutput)
            OutputReceived?.Invoke(this, message);

        _logger.LogInformation(
            "Attached existing Vintage Story server process. ProcessId={ProcessId}, ProfileId={ProfileId}.",
            processId,
            profile?.Id);
    }

    private InstanceProfile? ResolveProfileForProcess(
        InstanceProfile? preferredProfile,
        IReadOnlyList<InstanceProfile> profiles,
        string dataPath,
        string version)
    {
        var normalizedDataPath = NormalizePath(dataPath);
        if (!string.IsNullOrWhiteSpace(normalizedDataPath))
        {
            if (preferredProfile is not null &&
                NormalizePath(preferredProfile.DirectoryPath).Equals(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return preferredProfile;
            }

            var dataPathMatch = profiles.FirstOrDefault(profile =>
                NormalizePath(profile.DirectoryPath).Equals(normalizedDataPath, StringComparison.OrdinalIgnoreCase));
            if (dataPathMatch is not null)
                return dataPathMatch;
        }

        if (!string.IsNullOrWhiteSpace(version))
        {
            if (preferredProfile is not null &&
                preferredProfile.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
            {
                return preferredProfile;
            }

            var versionMatches = profiles
                .Where(profile => profile.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (versionMatches.Count == 1)
                return versionMatches[0];
        }

        return null;
    }

    private static int ScoreProcessMatch(
        InstanceProfile? preferredProfile,
        InstanceProfile? matchedProfile,
        string dataPath,
        string version)
    {
        if (preferredProfile is not null && matchedProfile is not null &&
            preferredProfile.Id.Equals(matchedProfile.Id, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(dataPath) ? 100 : 70;
        }

        if (matchedProfile is not null)
            return !string.IsNullOrWhiteSpace(dataPath) ? 90 : 50;

        return !string.IsNullOrWhiteSpace(version) ? 20 : 10;
    }

    private string TryReadCommandLine(int processId)
    {
        if (!OperatingSystem.IsWindows())
            return string.Empty;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            foreach (ManagementObject item in searcher.Get())
                return item["CommandLine"]?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read command line for process {ProcessId}.", processId);
        }

        return string.Empty;
    }

    private static string TryExtractDataPath(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return string.Empty;

        var match = DataPathArgumentPattern().Match(commandLine);
        return match.Success ? match.Groups["path"].Value : string.Empty;
    }

    private static string TryResolveVersionFromExecutable(Process process, string serversRoot)
    {
        try
        {
            var executablePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath))
                return string.Empty;

            var executableDirectory = NormalizePath(Path.GetDirectoryName(Path.GetFullPath(executablePath)));
            if (string.IsNullOrWhiteSpace(executableDirectory) ||
                !executableDirectory.StartsWith(serversRoot, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return Path.GetFileName(executableDirectory) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<int> StopOrphanWorkspaceServerProcessesAsync(
        CancellationToken cancellationToken,
        int? excludePid = null)
    {
        var serversRoot = NormalizePath(WorkspacePathHelper.ServersRoot);
        if (string.IsNullOrWhiteSpace(serversRoot))
            return 0;

        Process[] candidates;
        try
        {
            candidates = Process.GetProcessesByName("VintagestoryServer");
        }
        catch
        {
            return 0;
        }

        var killedCount = 0;
        foreach (var candidate in candidates)
        {
            using (candidate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pid = TryGetProcessId(candidate);
                if (!pid.HasValue)
                    continue;
                if (excludePid.HasValue && excludePid.Value == pid.Value)
                    continue;
                if (IsProcessTerminated(candidate))
                    continue;
                if (!IsWorkspaceServerProcess(candidate, serversRoot))
                    continue;

                try
                {
                    candidate.Kill(entireProcessTree: true);
                    await candidate.WaitForExitAsync(cancellationToken);
                    killedCount++;
                    OutputReceived?.Invoke(this, $"[system] 已清理孤立服务端进程，PID={pid.Value}。");
                }
                catch
                {
                    // 无法访问或终止时忽略，避免阻断主流程。
                }
            }
        }

        return killedCount;
    }

    private static bool IsWorkspaceServerProcess(Process process, string serversRoot)
    {
        try
        {
            var executablePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath))
                return false;

            var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath));
            var normalizedExecutableDirectory = NormalizePath(executableDirectory);
            if (string.IsNullOrWhiteSpace(normalizedExecutableDirectory))
                return false;

            return normalizedExecutableDirectory.StartsWith(serversRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return string.Empty;
        }
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

    [GeneratedRegex(@"--dataPath(?:=|\s+)(?:""(?<path>[^""]+)""|(?<path>\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataPathArgumentPattern();
}
