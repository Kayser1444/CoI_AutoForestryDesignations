// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
// Auto Forestry Designations - Runtime diagnostic level policy
using System;
using CoI.AutoHelpers.Logging;

namespace AutoForestryDesignations
{
    internal enum AfdDiagnosticLevel
    {
        Warning = 0,
        Info = 1,
        Debug = 2,
        Trace = 3,
    }

    internal static class AfdDiagnostics
    {
#if DEBUG
        internal const AfdDiagnosticLevel BuildDefaultLevel = AfdDiagnosticLevel.Debug;
#else
        internal const AfdDiagnosticLevel BuildDefaultLevel = AfdDiagnosticLevel.Info;
#endif

        private static AfdDiagnosticLevel s_level = BuildDefaultLevel;
        private static string s_configuredLevel = "Default";

        internal static AfdDiagnosticLevel Level => s_level;
        internal static string ConfiguredLevel => s_configuredLevel;
        internal static bool IsEnabled(AfdDiagnosticLevel level) => s_level >= level;

        internal static void ResetToBuildDefault()
        {
            s_configuredLevel = "Default";
            s_level = BuildDefaultLevel;
        }

        internal static bool TryApplyConfiguredLevel(string? value, out string error)
        {
            if (string.Equals(value?.Trim(), "default", StringComparison.OrdinalIgnoreCase))
            {
                ResetToBuildDefault();
                error = string.Empty;
                return true;
            }

            if (!TryParseLevel(value, out AfdDiagnosticLevel parsed))
            {
                error = "Use Default, Warning, Info, Debug, or Trace.";
                return false;
            }

            s_configuredLevel = parsed.ToString();
            s_level = parsed;
            error = string.Empty;
            return true;
        }

        internal static bool TrySetSessionLevel(string? value, out string error)
        {
            if (string.Equals(value?.Trim(), "default", StringComparison.OrdinalIgnoreCase))
            {
                s_level = GetConfiguredLevel();
                error = string.Empty;
                return true;
            }

            if (!TryParseLevel(value, out AfdDiagnosticLevel parsed))
            {
                error = "Use Default, Warning, Info, Debug, or Trace.";
                return false;
            }

            s_level = parsed;
            error = string.Empty;
            return true;
        }

        internal static string Describe()
            => $"active={s_level}, configured={s_configuredLevel}, buildDefault={BuildDefaultLevel}";

        internal static void Info(ModLogger logger, string message)
        {
            if (IsEnabled(AfdDiagnosticLevel.Info))
                logger.Info(message);
        }

        internal static void Debug(ModLogger logger, string message)
        {
            if (IsEnabled(AfdDiagnosticLevel.Debug))
                logger.Info(message);
        }

        internal static void Trace(ModLogger logger, string message)
        {
            if (IsEnabled(AfdDiagnosticLevel.Trace))
                logger.Info(message);
        }

        private static AfdDiagnosticLevel GetConfiguredLevel()
            => string.Equals(s_configuredLevel, "Default", StringComparison.OrdinalIgnoreCase)
                ? BuildDefaultLevel
                : Enum.TryParse(s_configuredLevel, true, out AfdDiagnosticLevel parsed)
                    ? parsed
                    : BuildDefaultLevel;

        private static bool TryParseLevel(string? value, out AfdDiagnosticLevel level)
        {
            if (Enum.TryParse(value?.Trim(), true, out level)
                && Enum.IsDefined(typeof(AfdDiagnosticLevel), level))
                return true;

            level = BuildDefaultLevel;
            return false;
        }
    }
}
