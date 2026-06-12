using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GameLoop
{
    public class LLMManager
    {
        private readonly ILLMClient _client;
        private Process _process;
        private CancellationTokenSource _pollCts;
        private LLMStatus _status = LLMStatus.Offline;

        public LLMStatus Status => _status;
        public event Action<LLMStatus> OnStatusChanged;
        public event Action<string> OnProcessOutput;

        public LLMManager(ILLMClient client)
        {
            _client = client;
        }

        public bool Start(string pythonExe, string serverDir)
        {
            if (_process != null && !_process.HasExited) return false;

            try
            {
                SetStatus(LLMStatus.Starting);

                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = "-m llm_server.main",
                        WorkingDirectory = serverDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                _process.Exited += (sender, e) =>
                {
                    if (_status == LLMStatus.Online || _status == LLMStatus.Starting)
                        SetStatus(LLMStatus.Error);
                };
                _process.EnableRaisingEvents = true;
                _process.Start();

                // Read stdout/stderr asynchronously
                _ = Task.Run(() => ReadStream(_process.StandardOutput, "[LLM-OUT]"));
                _ = Task.Run(() => ReadStream(_process.StandardError, "[LLM-ERR]"));

                StartPolling();
                return true;
            }
            catch (Exception)
            {
                SetStatus(LLMStatus.Error);
                return false;
            }
        }

        public void Stop()
        {
            StopPolling();

            if (_process != null && !_process.HasExited)
            {
                try { _process.Kill(); } catch { }
                _process = null;
            }

            SetStatus(LLMStatus.Offline);
        }

        private void StartPolling()
        {
            StopPolling();
            _pollCts = new CancellationTokenSource();
            var token = _pollCts.Token;

            Task.Run(async () =>
            {
                int attempts = 0;
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(2000, token);
                    if (token.IsCancellationRequested) break;

                    if (_process == null || _process.HasExited)
                    {
                        SetStatus(LLMStatus.Error);
                        return;
                    }

                    var online = await _client.HealthCheckAsync();
                    if (online)
                    {
                        attempts = 0;
                        if (_status == LLMStatus.Starting)
                            SetStatus(LLMStatus.Online);
                    }
                    else if (_status == LLMStatus.Online)
                    {
                        attempts++;
                        if (attempts >= 15)  // 30s of consecutive failures
                        {
                            SetStatus(LLMStatus.Error);
                            return;
                        }
                    }

                    if (token.IsCancellationRequested) break;
                }
            }, token);
        }

        private void StopPolling()
        {
            _pollCts?.Cancel();
            _pollCts = null;
        }

        private void ReadStream(System.IO.StreamReader reader, string prefix)
        {
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    OnProcessOutput?.Invoke($"{prefix} {line}");
                }
            }
            catch { }
        }

        private void SetStatus(LLMStatus status)
        {
            if (_status != status)
            {
                _status = status;
                // Fire-and-forget to avoid blocking the caller
                Task.Run(() => OnStatusChanged?.Invoke(status));
            }
        }
    }
}
