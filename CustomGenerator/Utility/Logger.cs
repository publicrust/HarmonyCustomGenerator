using System;
using System.IO;
using UnityEngine;

namespace CustomGenerator.Utilities
{
    public static class Logging
    {
        private static readonly string LogFolder = "HarmonyConfig/logs";
        private static readonly string LogFile;
        private static bool isInitialized;

        static Logging()
        {
            try
            {
                if (!Directory.Exists(LogFolder))
                    Directory.CreateDirectory(LogFolder);

                LogFile = Path.Combine(LogFolder, $"cgen_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                File.WriteAllText(LogFile, $"=== CustomGenerator Log Started at {DateTime.Now} ===\n");
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CGen Logger] Failed to initialize: {ex.Message}");
                isInitialized = false;
            }
        }
        public static void StartingMessage() {
            Logging.Info($"CustomGenerator by [aristocratos]");
            Debug.Log(new string('-', 30));
            Debug.Log("USE ONLY FOR MAP GENERATING!");
            Debug.Log("NOT FOR LIVE SERVER!!!");
            Debug.Log($"Config version: {ExtConfig.Config.Version}");
            Debug.Log(new string('-', 30));
        }
        public static void Info(string message)
        {
            string formattedMessage = $"[INFO] {DateTime.Now:HH:mm:ss} | {message}";
            Debug.Log($"[CGen] {message}");
            WriteToFile(formattedMessage);
        }

        public static void Warning(string message)
        {
            string formattedMessage = $"[WARN] {DateTime.Now:HH:mm:ss} | {message}";
            Debug.LogWarning($"[CGen] {message}");
            WriteToFile(formattedMessage);
        }

        public static void Error(string message, Exception ex = null)
        {
            string formattedMessage = $"[ERROR] {DateTime.Now:HH:mm:ss} | {message}";
            if (ex != null)
                formattedMessage += $"\n{ex.GetType()}: {ex.Message}\n{ex.StackTrace}";
            
            Debug.LogError($"[CGen] {message}");
            WriteToFile(formattedMessage);
        }

        public static void Generation(string message)
        {
            string formattedMessage = $"[GEN] {DateTime.Now:HH:mm:ss} | {message}";
            Debug.Log($"[CGen Gen] {message}");
            WriteToFile(formattedMessage);
        }

        public static void Config(string message)
        {
            string formattedMessage = $"[CFG] {DateTime.Now:HH:mm:ss} | {message}";
            Debug.Log($"[CGen Config] {message}");
            WriteToFile(formattedMessage);
        }

        private static void WriteToFile(string message)
        {
            if (!isInitialized) return;

            try
            {
                File.AppendAllText(LogFile, message + "\n");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CGen Logger] Failed to write to log file: {ex.Message}");
            }
        }

        public static void ClearOldLogs(int daysToKeep = 2)
        {
            try
            {
                if (!Directory.Exists(LogFolder)) return;

                var files = Directory.GetFiles(LogFolder, "cgen_*.log");
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CGen Logger] Failed to clear old logs: {ex.Message}");
            }
        }
    }
} 