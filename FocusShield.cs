using System;
using System.Collections.Generic;
using System.Linq;

namespace Neck
{
    internal sealed class FocusShieldResult
    {
        public int ApplicationsShielded;
        public int ApplicationsChanged;
        public int ProcessesChanged;
        public int AccessErrors;
    }

    internal static class FocusShieldManager
    {
        private const long Megabyte = 1024L * 1024L;
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> OwnedApplications =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SensitiveApplications =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "audiodg", "Discord", "ms-teams", "obs64", "PhoneExperienceHost", "Skype",
                "slack", "Spotify", "Streamlabs OBS", "Teams", "vlc", "WhatsApp", "WhatsApp.Root",
                "wmplayer", "Zoom", "ZoomIt", "Taskmgr"
            };
        private static DateTime _lastEvaluationUtc = DateTime.MinValue;
        private static bool _suspended;

        public static int ActiveCount
        {
            get { lock (SyncRoot) return OwnedApplications.Count; }
        }

        public static string ActiveSummary
        {
            get
            {
                lock (SyncRoot)
                {
                    if (OwnedApplications.Count == 0) return string.Empty;
                    return string.Join(", ", OwnedApplications.OrderBy(name => name).Take(3).Select(SystemInfo.FriendlyProcessName));
                }
            }
        }

        public static FocusShieldResult Refresh(string targetProcessName, bool targetForeground)
        {
            lock (SyncRoot)
            {
                if (_suspended) targetForeground = false;
            }
            if (!targetForeground) return RestoreAll();
            DateTime now = DateTime.UtcNow;
            lock (SyncRoot)
            {
                if (now - _lastEvaluationUtc < TimeSpan.FromSeconds(30))
                    return CurrentResult();
            }
            return RefreshCore(targetProcessName, SosInspector.GetFocusShieldCandidates(), SystemInfo.GetMemoryStatus(), now);
        }

        internal static FocusShieldResult RefreshForTesting(string targetProcessName, bool targetForeground,
            IEnumerable<SosCandidate> candidates, MemoryStatus memory, DateTime utcNow)
        {
            return targetForeground
                ? RefreshCore(targetProcessName, candidates, memory, utcNow)
                : RestoreAll();
        }

        internal static List<SosCandidate> SelectCandidates(IEnumerable<SosCandidate> candidates,
            string targetProcessName, MemoryStatus memory)
        {
            if (candidates == null)
                return new List<SosCandidate>();
            long minimumBytes = memory.PercentUsed >= 85 ? 192L * Megabyte : 384L * Megabyte;
            return candidates
                .Where(item => item != null && item.VisibleWindows > 0 &&
                    ((memory.PercentUsed >= 70 && item.MemoryBytes >= minimumBytes) || item.CpuPercent >= 12d))
                .Where(item => !string.Equals(item.ProcessName, targetProcessName, StringComparison.OrdinalIgnoreCase))
                .Where(item => EfficiencyModeManager.CanTarget(item.ProcessName) && !SensitiveApplications.Contains(item.ProcessName))
                .OrderByDescending(item => item.CpuPercent >= 12d ? 100000d + item.CpuPercent : item.MemoryBytes / (double)Megabyte)
                .Take(3)
                .ToList();
        }

        public static FocusShieldResult Stop()
        {
            return RestoreAll();
        }

        public static void SetSuspended(bool suspended)
        {
            lock (SyncRoot) _suspended = suspended;
            if (suspended) RestoreAll();
        }

        private static FocusShieldResult RefreshCore(string targetProcessName, IEnumerable<SosCandidate> candidates,
            MemoryStatus memory, DateTime utcNow)
        {
            List<SosCandidate> selected = SelectCandidates(candidates, targetProcessName, memory);
            HashSet<string> selectedNames = new HashSet<string>(selected.Select(item => item.ProcessName), StringComparer.OrdinalIgnoreCase);
            FocusShieldResult result = new FocusShieldResult();
            lock (SyncRoot)
            {
                _lastEvaluationUtc = utcNow;
                foreach (string owned in OwnedApplications.ToList())
                {
                    if (selectedNames.Contains(owned)) continue;
                    EfficiencyModeResult restored = EfficiencyModeManager.Restore(owned);
                    result.ProcessesChanged += restored.ProcessesChanged;
                    result.AccessErrors += restored.AccessErrors;
                    OwnedApplications.Remove(owned);
                    result.ApplicationsChanged++;
                }
                foreach (SosCandidate candidate in selected)
                {
                    if (OwnedApplications.Contains(candidate.ProcessName) || EfficiencyModeManager.IsActive(candidate.ProcessName)) continue;
                    EfficiencyModeResult applied = EfficiencyModeManager.Apply(candidate.ProcessName);
                    result.ProcessesChanged += applied.ProcessesChanged;
                    result.AccessErrors += applied.AccessErrors;
                    if (!applied.HasChanges) continue;
                    OwnedApplications.Add(candidate.ProcessName);
                    result.ApplicationsChanged++;
                }
                result.ApplicationsShielded = OwnedApplications.Count;
            }
            return result;
        }

        private static FocusShieldResult RestoreAll()
        {
            FocusShieldResult result = new FocusShieldResult();
            lock (SyncRoot)
            {
                foreach (string processName in OwnedApplications.ToList())
                {
                    EfficiencyModeResult restored = EfficiencyModeManager.Restore(processName);
                    result.ProcessesChanged += restored.ProcessesChanged;
                    result.AccessErrors += restored.AccessErrors;
                    result.ApplicationsChanged++;
                }
                OwnedApplications.Clear();
                _lastEvaluationUtc = DateTime.MinValue;
            }
            return result;
        }

        private static FocusShieldResult CurrentResult()
        {
            return new FocusShieldResult { ApplicationsShielded = OwnedApplications.Count };
        }
    }
}
