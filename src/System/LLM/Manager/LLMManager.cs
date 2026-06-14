using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GameLoop
{
    /// <summary>
    /// Manages the LLM server child process with watchdog-based health monitoring
    /// and automatic restart on crash.
    ///
    /// Lifecycle:
    ///   Offline → Starting → Online ↔ (watchdog with auto-restart) → Error (max retries exhausted)
    /// </summary>
    public class LLMManager
    {
        private readonly ILLMClient _client;
        private Process _process;
        private CancellationTokenSource _watchdogCts;
        private LLMStatus _status = LLMStatus.Offline;
        private readonly int _port;

        // Watchdog / restart config
        private int _restartCount;
        private DateTime _lastRestartTime = DateTime.MinValue;
        private const int MaxRestarts = 3;
        private static readonly TimeSpan RestartCooldown = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(180);
        private const int MaxConsecutiveFailures = 10;

        // Stored for restart
        private string _pythonExe;
        private string _serverDir;

        public LLMStatus Status => _status;
        public event Action<LLMStatus> OnStatusChanged;
        public event Action<string> OnProcessOutput;

        public LLMManager(ILLMClient client, int port = 8000)
        {
            _client = client;
            _port = port;
        }

        // ================================================================
        // Public API
        // ================================================================

        public async Task<bool> StartAsync(string pythonExe, string serverDir)
        {
            if (_status == LLMStatus.Online) return true;
            if (_status == LLMStatus.Starting) return false;

            _pythonExe = pythonExe;
            _serverDir = serverDir;
            _restartCount = 0;

            // Retry initial startup up to MaxRestarts times
            for (int attempt = 0; attempt <= MaxRestarts; attempt++)
            {
                if (attempt > 0)
                {
                    _restartCount = attempt;
                    OnProcessOutput?.Invoke($"[LLM] Initial startup retry {attempt}/{MaxRestarts}...");
                    await Task.Delay(3000); // brief cooldown between retries
                }

                bool ok = await LaunchAndWaitAsync();
                if (ok) return true;

                // If process exited (not timeout), it's worth retrying
                // If timeout, the model might just be loading slowly
                if (attempt < MaxRestarts)
                    OnProcessOutput?.Invoke($"[LLM] Startup failed, will retry ({attempt + 1}/{MaxRestarts})");
            }

            OnProcessOutput?.Invoke($"[LLM] All startup attempts exhausted");
            SetStatus(LLMStatus.Error);
            return false;
        }

        public void Stop()
        {
            StopWatchdog();
            KillProcess();
            SetStatus(LLMStatus.Offline);
            _restartCount = 0;
        }

        /// <summary>
        /// Mark the server as already running (no process to manage).
        /// Starts the watchdog to monitor the external server.
        /// Stores pythonExe/serverDir so the watchdog can restart if needed.
        /// </summary>
        public void AttachToExisting(string pythonExe, string serverDir)
        {
            _pythonExe = pythonExe;
            _serverDir = serverDir;
            SetStatus(LLMStatus.Online);
            StartWatchdog();
            OnProcessOutput?.Invoke("[LLM] Attached to existing server");
        }

        // ================================================================
        // Orphan cleanup
        // ================================================================

        public bool KillOrphanedServers()
        {
            bool cleaned = false;
            try
            {
                var netstat = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c netstat -ano | findstr \":{_port} \" | findstr LISTENING",
                        UseShellExecute = false, RedirectStandardOutput = true,
                        RedirectStandardError = true, CreateNoWindow = true
                    }
                };
                netstat.Start();
                var output = netstat.StandardOutput.ReadToEnd();
                netstat.WaitForExit(3000);

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5 && int.TryParse(parts[^1], out int pid) && pid > 0)
                    {
                        try
                        {
                            var proc = Process.GetProcessById(pid);
                            if (proc.ProcessName.Contains("python", StringComparison.OrdinalIgnoreCase))
                            {
                                OnProcessOutput?.Invoke($"[LLM] Killing orphaned Python PID={pid} on port {_port}");
                                proc.Kill(); proc.WaitForExit(5000); cleaned = true;
                            }
                        }
                        catch { }
                    }
                }

                if (!cleaned)
                {
                    foreach (var proc in Process.GetProcessesByName("python"))
                    {
                        try
                        {
                            var cmdLine = GetProcessCommandLine(proc.Id);
                            if (cmdLine != null && cmdLine.Contains("llm_server"))
                            {
                                OnProcessOutput?.Invoke($"[LLM] Killing orphaned llm_server PID={proc.Id}");
                                proc.Kill(); proc.WaitForExit(5000); cleaned = true;
                            }
                        }
                        catch { }
                    }
                }

                if (cleaned)
                    for (int i = 0; i < 10; i++) { if (!IsPortInUse()) break; Thread.Sleep(500); }
            }
            catch (Exception ex) { OnProcessOutput?.Invoke($"[LLM] Orphan cleanup error: {ex.Message}"); }
            return cleaned;
        }

        // ================================================================
        // Core: Launch + Wait for Ready
        // ================================================================

        private async Task<bool> LaunchAndWaitAsync()
        {
            KillOrphanedServers();
            StopWatchdog();

            if (!SpawnProcess()) return false;

            SetStatus(LLMStatus.Starting);
            bool ready = await WaitForReadyAsync(StartupTimeout);

            if (ready)
            {
                SetStatus(LLMStatus.Online);
                StartWatchdog();
                OnProcessOutput?.Invoke($"[LLM] Server ready on port {_port}");
                return true;
            }
            else if (_process == null || _process.HasExited)
            {
                OnProcessOutput?.Invoke($"[LLM] Process exited during startup (exit code={_process?.ExitCode ?? -1})");
                KillProcess();
            }
            else
            {
                OnProcessOutput?.Invoke("[LLM] Startup timeout — server did not become healthy within time limit");
                KillProcess();
            }
            return false;
        }

        private bool SpawnProcess()
        {
            try
            {
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonExe,
                        Arguments = "-m llm_server.main",
                        WorkingDirectory = _serverDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };
                _process.Start();

                _ = Task.Run(() => ReadStream(_process.StandardOutput, "[LLM-OUT]"));
                _ = Task.Run(() => ReadStream(_process.StandardError, "[LLM-ERR]"));

                return true;
            }
            catch (Exception ex)
            {
                OnProcessOutput?.Invoke($"[LLM] Failed to spawn process: {ex.Message}");
                SetStatus(LLMStatus.Error);
                return false;
            }
        }

        private async Task<bool> WaitForReadyAsync(TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            int attempt = 0;

            while (sw.Elapsed < timeout)
            {
                if (_process == null || _process.HasExited)
                {
                    OnProcessOutput?.Invoke("[LLM] Process exited during startup");
                    return false;
                }

                if (await HealthCheckWithTimeout(TimeSpan.FromSeconds(3)))
                    return true;

                attempt++;
                int delayMs = Math.Min(5000, 1000 + (attempt * attempt * 250));
                await Task.Delay(delayMs);
            }
            return false;
        }

        // ================================================================
        // Watchdog: Runtime Health + Auto-Restart
        // ================================================================

        private void StartWatchdog()
        {
            StopWatchdog();
            _watchdogCts = new CancellationTokenSource();
            var token = _watchdogCts.Token;
            Task.Run(async () => await WatchdogLoopAsync(token), token);
        }

        private void StopWatchdog()
        {
            _watchdogCts?.Cancel();
            _watchdogCts = null;
        }

        private async Task WatchdogLoopAsync(CancellationToken token)
        {
            int failCount = 0;

            while (!token.IsCancellationRequested)
            {
                int intervalMs = failCount switch { 0 => 5000, 1 => 2000, 2 => 5000, _ => 10000 };
                await Task.Delay(intervalMs, token);
                if (token.IsCancellationRequested) break;

                // Process exited? Only if we own the process (not external attach)
                if (_process != null && _process.HasExited)
                {
                    int exitCode = _process.ExitCode;
                    OnProcessOutput?.Invoke($"[LLM] Process exited (code={exitCode}), attempting restart...");
                    if (!await TryRestartAsync(token)) break;
                    failCount = 0;
                    continue;
                }

                // Health check
                bool healthy = await HealthCheckWithTimeout(TimeSpan.FromSeconds(3));
                if (healthy)
                {
                    if (failCount > 0)
                        OnProcessOutput?.Invoke($"[LLM] Health restored after {failCount} failures");
                    failCount = 0;
                }
                else
                {
                    failCount++;
                    OnProcessOutput?.Invoke($"[LLM] Health check failed ({failCount}/{MaxConsecutiveFailures})");

                    if (failCount >= MaxConsecutiveFailures)
                    {
                        OnProcessOutput?.Invoke("[LLM] Max consecutive failures — killing and restarting...");
                        KillProcess();
                        if (!await TryRestartAsync(token)) break;
                        failCount = 0;
                    }
                }
            }
        }

        private async Task<bool> TryRestartAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested) return false;

            var sinceLast = DateTime.UtcNow - _lastRestartTime;
            if (sinceLast < RestartCooldown)
            {
                int waitMs = (int)(RestartCooldown - sinceLast).TotalMilliseconds;
                OnProcessOutput?.Invoke($"[LLM] Restart cooldown — waiting {waitMs / 1000}s");
                await Task.Delay(waitMs, token);
            }

            if (_restartCount >= MaxRestarts)
            {
                OnProcessOutput?.Invoke($"[LLM] Max restarts ({MaxRestarts}) exhausted — giving up");
                SetStatus(LLMStatus.Error);
                return false;
            }

            _restartCount++;
            _lastRestartTime = DateTime.UtcNow;
            OnProcessOutput?.Invoke($"[LLM] Restart attempt {_restartCount}/{MaxRestarts}");

            return await LaunchAndWaitAsync();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private async Task<bool> HealthCheckWithTimeout(TimeSpan timeout)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                return await _client.HealthCheckAsync(cts.Token);
            }
            catch { return false; }
        }

        private void KillProcess()
        {
            if (_process == null) return;
            try { if (!_process.HasExited) { _process.Kill(); _process.WaitForExit(5000); } } catch { }
            _process = null;
        }

        private static string GetProcessCommandLine(int pid)
        {
            try
            {
                var wmic = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wmic", Arguments = $"process where ProcessId={pid} get CommandLine /format:list",
                        UseShellExecute = false, RedirectStandardOutput = true,
                        RedirectStandardError = true, CreateNoWindow = true
                    }
                };
                wmic.Start();
                var output = wmic.StandardOutput.ReadToEnd();
                wmic.WaitForExit(3000);
                return output;
            }
            catch { return null; }
        }

        private bool IsPortInUse()
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect("127.0.0.1", _port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                if (!success) return false;
                try { client.EndConnect(result); return true; } catch { return false; }
            }
            catch { return false; }
        }

        private void ReadStream(System.IO.StreamReader reader, string prefix)
        {
            try { string line; while ((line = reader.ReadLine()) != null) OnProcessOutput?.Invoke($"{prefix} {line}"); } catch { }
        }

        private void SetStatus(LLMStatus status)
        {
            if (_status != status)
            {
                _status = status;
                Task.Run(() => OnStatusChanged?.Invoke(status));
            }
        }
    }
}
