using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[McpServerToolType, Description("Python server management for VR_Eat backend")]
internal sealed class PythonServerTool
{
    private const string ServerScriptName = "camera_server_yolo_keyboard_color_v5.py";
    private const string PythonFolderName = "Python";
    private const string PythonExecutablePath = @"C:\Users\nakaj\anaconda3\python.exe";
    private const string ServerBaseUrl = "http://127.0.0.1:5000";
    private const string ShutdownEndpoint = "/shutdown";
    private const int GracefulShutdownTimeoutMs = 3000;

    private static Process _serverProcess;

    [McpServerTool, Description("Start the Python Flask server for VR_Eat. Launches the YOLO camera server on localhost:5000.")]
    public async ValueTask<string> StartPythonServer()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            if (IsServerRunning())
            {
                return "Python server is already running.";
            }

            string scriptPath = FindServerScript();
            if (scriptPath == null)
            {
                return $"Error: {ServerScriptName} not found in project.";
            }

            string workingDirectory = Path.GetDirectoryName(scriptPath);

            string pythonPath = FindPythonExecutable();
            if (pythonPath == null)
            {
                return $"Error: Python executable not found at {PythonExecutablePath}.";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _serverProcess = new Process { StartInfo = startInfo };
            _serverProcess.OutputDataReceived += OnOutputReceived;
            _serverProcess.ErrorDataReceived += OnErrorReceived;
            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            Debug.Log($"[PythonServer] Started: {scriptPath} (PID: {_serverProcess.Id})");
            return $"Python server started (PID: {_serverProcess.Id}). Listening on http://127.0.0.1:5000";
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonServer] Failed to start: {e.Message}");
            throw;
        }
    }

    [McpServerTool, Description("Stop the running Python Flask server.")]
    public async ValueTask<string> StopPythonServer()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            if (!IsServerRunning())
            {
                _serverProcess = null;
                return "Python server is not running.";
            }

            int pid = _serverProcess.Id;
            ShutdownServer();
            return $"Python server stopped (PID: {pid}).";
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonServer] Failed to stop: {e.Message}");
            throw;
        }
    }

    [McpServerTool, Description("Check if the Python Flask server is currently running.")]
    public async ValueTask<string> GetPythonServerStatus()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            if (IsServerRunning())
            {
                return $"Python server is running (PID: {_serverProcess.Id}).";
            }

            _serverProcess = null;
            return "Python server is not running.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    private static void StartServerInternal()
    {
        string scriptPath = FindServerScript();
        if (scriptPath == null)
        {
            Debug.LogWarning($"[PythonServer] {ServerScriptName} not found in project.");
            return;
        }

        string pythonPath = FindPythonExecutable();
        if (pythonPath == null)
        {
            Debug.LogWarning($"[PythonServer] Python executable not found at {PythonExecutablePath}.");
            return;
        }

        string workingDirectory = Path.GetDirectoryName(scriptPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            _serverProcess = new Process { StartInfo = startInfo };
            _serverProcess.OutputDataReceived += OnOutputReceived;
            _serverProcess.ErrorDataReceived += OnErrorReceived;
            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            Debug.Log($"[PythonServer] Auto-started: {scriptPath} (PID: {_serverProcess.Id})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonServer] Failed to auto-start: {e.Message}");
        }
    }

    private static bool IsServerRunning()
    {
        if (_serverProcess == null) return false;

        try
        {
            return !_serverProcess.HasExited;
        }
        catch (InvalidOperationException)
        {
            _serverProcess = null;
            return false;
        }
    }

    private static string FindServerScript()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string pythonFolder = Path.Combine(projectRoot, PythonFolderName);
        string scriptPath = Path.Combine(pythonFolder, ServerScriptName);

        return File.Exists(scriptPath) ? scriptPath : null;
    }

    private static string FindPythonExecutable()
    {
        return File.Exists(PythonExecutablePath) ? PythonExecutablePath : null;
    }

    private static void ShutdownServer()
    {
        if (!IsServerRunning()) return;

        int pid = _serverProcess.Id;

        // グレースフルシャットダウンを試行（Flask側でカメラ解放等を実行）
        if (TrySendShutdownRequest())
        {
            try
            {
                _serverProcess.WaitForExit(GracefulShutdownTimeoutMs);
            }
            catch (InvalidOperationException) { /* already exited */ }
        }

        // まだ生きていれば強制終了
        if (IsServerRunning())
        {
            ForceKillProcessTree(_serverProcess);
        }

        _serverProcess = null;
        Debug.Log($"[PythonServer] Stopped (PID: {pid})");
    }

    private static bool TrySendShutdownRequest()
    {
        try
        {
            var request = (HttpWebRequest)WebRequest.Create(ServerBaseUrl + ShutdownEndpoint);
            request.Method = "POST";
            request.Timeout = GracefulShutdownTimeoutMs;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                return response.StatusCode == HttpStatusCode.OK;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void ForceKillProcessTree(Process process)
    {
        try
        {
            var killProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {process.Id} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            killProcess.Start();
            killProcess.WaitForExit();
        }
        catch
        {
            try { process.Kill(); }
            catch { /* already exited */ }
        }
    }

    private const string LogPrefix = "[PythonServer] ";

    private static bool IsFilteredMessage(string message)
    {
        return message.StartsWith("0:") || message.StartsWith("Speed:");
    }

    private static string StripDuplicatePrefix(string message)
    {
        return message.StartsWith(LogPrefix) ? message.Substring(LogPrefix.Length) : message;
    }

    private static void OnOutputReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data) && !IsFilteredMessage(e.Data))
        {
            Debug.Log($"{LogPrefix}{StripDuplicatePrefix(e.Data)}");
        }
    }

    private static void OnErrorReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data) && !IsFilteredMessage(e.Data))
        {
            Debug.Log($"{LogPrefix}{StripDuplicatePrefix(e.Data)}");
        }
    }

    [InitializeOnLoadMethod]
    private static void RegisterHooks()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.quitting += OnEditorQuitting;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                if (!IsServerRunning())
                {
                    StartServerInternal();
                }
                break;

            case PlayModeStateChange.ExitingPlayMode:
                if (IsServerRunning())
                {
                    Debug.Log("[PythonServer] Auto-stopping server on Play mode exit.");
                    ShutdownServer();
                }
                break;
        }
    }

    private static void OnEditorQuitting()
    {
        if (IsServerRunning())
        {
            Debug.Log("[PythonServer] Stopping server on Unity Editor quit.");
            ShutdownServer();
        }
    }
}
