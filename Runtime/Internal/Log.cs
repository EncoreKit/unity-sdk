using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace EncoreKit.Internal
{
    // The SDK's own logs, gated by EncoreOptions.LogLevel. At LogLevel.None the SDK logs nothing of its own.
    internal static class Log
    {
        internal static LogLevel Level = LogLevel.None;

        internal static void Error(string message)
        {
            if (Level >= LogLevel.Error) Debug.LogError("[Encore] " + message);
        }

        internal static void Warn(string message)
        {
            if (Level >= LogLevel.Warn) Debug.LogWarning("[Encore] " + message);
        }

        internal static void Info(string message)
        {
            if (Level >= LogLevel.Info) Debug.Log("[Encore] " + message);
        }

        // Every native or bridge failure the C# layer catches goes through here.
        // reportTo is for a platform not yet set on Encore, as during Configure.
        internal static void Contained(string where, Exception e, IEncorePlatform reportTo = null)
        {
            Warn($"{where} failed: {e.Message}");
            Report(where, e, reportTo ?? Encore.Platform);
        }

        // To the native SDK's error reports: the call name and the exception type only, never the message, which can
        // carry user data. A report that fails is dropped, not reported again.
        private static void Report(string where, Exception e, IEncorePlatform platform)
        {
            if (platform == null) return;
            try
            {
                platform.ReportWrapperError(where, e.GetType().Name);
            }
            catch (Exception)
            {
            }
        }

        // Runs a platform call, and never lets its exception reach the game. False when it threw.
        internal static bool Contain(string where, Action call, Func<IEncorePlatform> reportTo = null)
        {
            try
            {
                call();
                return true;
            }
            catch (Exception e)
            {
                Contained(where, e, reportTo?.Invoke());
                return false;
            }
        }

        // Every native callback goes through here: a throw that unwinds into the native frame that called back crashes the game.
        internal static void Deliver(string where, Action<string> handler, IntPtr json) =>
            Contain(where, () => handler?.Invoke(Marshal.PtrToStringUTF8(json)));

        internal static void Deliver(string where, Action<string> handler, string json) => Contain(where, () => handler?.Invoke(json));
    }
}
