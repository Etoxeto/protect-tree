using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProtectTree.Runtime.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class BuildLogViewer : MonoBehaviour
    {
        private const int MaxEntries = 300;
        private const KeyCode ToggleKey = KeyCode.F12;

        private static readonly List<LogEntry> Entries =
            new List<LogEntry>(MaxEntries);

        private static BuildLogViewer _instance;

        private Rect _windowRect;
        private Vector2 _scroll;
        private bool _isVisible;
        private bool _showStack = true;
        private bool _scrollToBottom;
        private GUIStyle _entryStyle;
        private GUIStyle _stackStyle;
        private GUIStyle _pathStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeForWindowsPlayer()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            EnsureInstance();
#endif
        }

        public static BuildLogViewer EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject owner = new GameObject("[ProtectTree] Build Log Viewer");
            DontDestroyOnLoad(owner);
            _instance = owner.AddComponent<BuildLogViewer>();
            return _instance;
        }

        private static string PlayerLogPath =>
            Path.Combine(Application.persistentDataPath, "Player.log");

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += OnLogMessageReceived;

            AddEntry(
                LogType.Log,
                "Build log viewer ready. Press F12 to toggle.",
                null);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                _isVisible = !_isVisible;
                _scrollToBottom = _isVisible;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }

            EnsureStyles();
            if (_windowRect.width <= 0f)
            {
                float width = Mathf.Min(Screen.width - 40f, 1120f);
                float height = Mathf.Min(Screen.height - 40f, 680f);
                _windowRect = new Rect(
                    20f,
                    20f,
                    Mathf.Max(360f, width),
                    Mathf.Max(260f, height));
            }

            _windowRect = GUI.Window(
                932710,
                _windowRect,
                DrawWindow,
                "Protect Tree Build Logs (F12)");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear", GUILayout.Width(80f)))
            {
                Entries.Clear();
            }

            if (GUILayout.Button("Copy All", GUILayout.Width(90f)))
            {
                GUIUtility.systemCopyBuffer = BuildLogText();
            }

            if (GUILayout.Button("Open Log Folder", GUILayout.Width(130f)))
            {
                OpenLogFolder();
            }

            _showStack = GUILayout.Toggle(
                _showStack,
                "Show stack",
                GUILayout.Width(100f));

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{Entries.Count}/{MaxEntries}");
            GUILayout.EndHorizontal();

            GUILayout.Label("Player.log: " + PlayerLogPath, _pathStyle);

            _scroll = GUILayout.BeginScrollView(_scroll);
            // 日志窗口只展示运行时镜像，真正的完整日志仍由 Unity 写入 Player.log。
            foreach (LogEntry entry in Entries)
            {
                Color previousColor = GUI.color;
                GUI.color = GetLogColor(entry.Type);
                GUILayout.Label(
                    $"[{entry.Time}] [{entry.Type}] {entry.Message}",
                    _entryStyle);
                GUI.color = previousColor;

                if (_showStack && ShouldShowStack(entry))
                {
                    GUILayout.TextArea(entry.StackTrace, _stackStyle);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.Label("F12: toggle   Drag title bar to move");
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
            if (_scrollToBottom && Event.current.type == EventType.Repaint)
            {
                _scroll.y = float.MaxValue;
                _scrollToBottom = false;
            }
        }

        private void OnLogMessageReceived(
            string condition,
            string stackTrace,
            LogType type)
        {
            AddEntry(type, condition, stackTrace);
        }

        private static void AddEntry(
            LogType type,
            string message,
            string stackTrace)
        {
            if (Entries.Count >= MaxEntries)
            {
                Entries.RemoveAt(0);
            }

            Entries.Add(new LogEntry(
                DateTime.Now.ToString("HH:mm:ss.fff"),
                type,
                message ?? string.Empty,
                stackTrace ?? string.Empty));

            if (_instance != null)
            {
                _instance._scrollToBottom = true;
            }
        }

        private void EnsureStyles()
        {
            _entryStyle ??= new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = 13
            };

            _stackStyle ??= new GUIStyle(GUI.skin.textArea)
            {
                wordWrap = true,
                fontSize = 11
            };

            _pathStyle ??= new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = 12
            };
        }

        private static bool ShouldShowStack(LogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.StackTrace))
            {
                return false;
            }

            return entry.Type == LogType.Error
                || entry.Type == LogType.Assert
                || entry.Type == LogType.Exception;
        }

        private static Color GetLogColor(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return new Color(1f, 0.85f, 0.25f);
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return new Color(1f, 0.35f, 0.35f);
                default:
                    return Color.white;
            }
        }

        private static string BuildLogText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Player.log: " + PlayerLogPath);
            foreach (LogEntry entry in Entries)
            {
                builder.Append('[')
                    .Append(entry.Time)
                    .Append("] [")
                    .Append(entry.Type)
                    .Append("] ")
                    .AppendLine(entry.Message);

                if (ShouldShowStack(entry))
                {
                    builder.AppendLine(entry.StackTrace);
                }
            }

            return builder.ToString();
        }

        private static void OpenLogFolder()
        {
            string directory = Path.GetDirectoryName(PlayerLogPath);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            Application.OpenURL("file:///" + directory.Replace("\\", "/"));
        }

        private readonly struct LogEntry
        {
            public LogEntry(
                string time,
                LogType type,
                string message,
                string stackTrace)
            {
                Time = time;
                Type = type;
                Message = message;
                StackTrace = stackTrace;
            }

            public string Time { get; }

            public LogType Type { get; }

            public string Message { get; }

            public string StackTrace { get; }
        }
    }
}
