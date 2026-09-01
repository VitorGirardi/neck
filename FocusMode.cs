using System;

namespace Neck
{
    internal sealed class FocusModeResult
    {
        public string ProcessName = string.Empty;
        public int TurboProcessesChanged;
        public int AdaptiveProcessesChanged;
        public int AccessErrors;
    }

    internal static class FocusModeManager
    {
        private static readonly object SyncRoot = new object();
        private static FocusModeSession _session;

        public static bool IsActive
        {
            get { lock (SyncRoot) return _session != null && TurboModeManager.IsActive; }
        }

        public static bool IsTarget(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            lock (SyncRoot)
            {
                return _session != null && TurboModeManager.IsActive &&
                       string.Equals(_session.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string ActiveDisplayName
        {
            get { lock (SyncRoot) return _session == null ? string.Empty : _session.DisplayName; }
        }

        public static string ActiveProcessName
        {
            get { lock (SyncRoot) return _session == null ? string.Empty : _session.ProcessName; }
        }

        public static TimeSpan Remaining { get { return TurboModeManager.Remaining; } }

        public static string GetStateLabel(string processName)
        {
            if (!IsTarget(processName)) return "Disponível";
            if (TurboModeManager.IsForeground)
                return FocusShieldManager.ActiveCount > 0 ? "Mais rápido + escudo" : "Mais rápido agora";
            AdaptiveModeState adaptive = EfficiencyModeManager.GetState(processName);
            return adaptive == AdaptiveModeState.Optimized ? "Economizando memória" : "Pronto para acelerar";
        }

        public static FocusModeResult Start(string processName, string displayName, int durationMinutes)
        {
            FocusModeResult result = NewResult(processName);
            if (!EfficiencyModeManager.CanTarget(processName)) return result;
            AutopilotProtectionManager.Stop();
            lock (SyncRoot)
            {
                StopCore(result);
                bool adaptiveAlreadyActive = EfficiencyModeManager.IsActive(processName);
                if (!adaptiveAlreadyActive)
                {
                    EfficiencyModeResult adaptive = EfficiencyModeManager.Apply(processName);
                    result.AdaptiveProcessesChanged += adaptive.ProcessesChanged;
                    result.AccessErrors += adaptive.AccessErrors;
                }
                TurboModeResult turbo = TurboModeManager.Start(processName, displayName, durationMinutes);
                result.TurboProcessesChanged += turbo.ProcessesChanged;
                result.AccessErrors += turbo.AccessErrors;
                if (!TurboModeManager.IsActive)
                {
                    if (!adaptiveAlreadyActive) EfficiencyModeManager.Restore(processName);
                    return result;
                }
                _session = new FocusModeSession
                {
                    ProcessName = processName,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? SystemInfo.FriendlyProcessName(processName) : displayName,
                    OwnsAdaptive = !adaptiveAlreadyActive
                };
            }
            return result;
        }

        public static FocusModeResult Stop()
        {
            FocusModeResult result = NewResult(string.Empty);
            lock (SyncRoot) StopCore(result);
            return result;
        }

        public static void Refresh()
        {
            lock (SyncRoot)
            {
                if (_session == null) return;
                TurboModeManager.Refresh();
                if (!TurboModeManager.IsActive) FinishSession(NewResult(_session.ProcessName));
                else FocusShieldManager.Refresh(_session.ProcessName, TurboModeManager.IsForeground);
            }
        }

        private static void StopCore(FocusModeResult result)
        {
            TurboModeResult turbo = TurboModeManager.Stop();
            result.TurboProcessesChanged += turbo.ProcessesChanged;
            result.AccessErrors += turbo.AccessErrors;
            if (_session != null) FinishSession(result);
        }

        private static void FinishSession(FocusModeResult result)
        {
            FocusModeSession session = _session;
            _session = null;
            FocusShieldResult shield = FocusShieldManager.Stop();
            result.AdaptiveProcessesChanged += shield.ProcessesChanged;
            result.AccessErrors += shield.AccessErrors;
            if (session == null || !session.OwnsAdaptive) return;
            EfficiencyModeResult adaptive = EfficiencyModeManager.Restore(session.ProcessName);
            result.ProcessName = session.ProcessName;
            result.AdaptiveProcessesChanged += adaptive.ProcessesChanged;
            result.AccessErrors += adaptive.AccessErrors;
        }

        private static FocusModeResult NewResult(string processName)
        {
            return new FocusModeResult { ProcessName = processName ?? string.Empty };
        }

        private sealed class FocusModeSession
        {
            public string ProcessName;
            public string DisplayName;
            public bool OwnsAdaptive;
        }
    }
}
