using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StudyGuard
{
    public class ViolationLogEntry
    {
        public DateTimeOffset Timestamp { get; set; }

        public string Domain { get; set; } = "";

        public string Action { get; set; } = "BLOCKED";
    }

    public class ViolationLogService
    {
        private readonly string logDirectory;
        private readonly string logFilePath;

        private readonly object fileLock = new();

        public ViolationLogService()
        {
            logDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "logs"
            );

            logFilePath = Path.Combine(
                logDirectory,
                "violations.jsonl"
            );

            Directory.CreateDirectory(logDirectory);
        }

        public void LogViolation(string url)
        {
            string domain = ExtractDomain(url);

            ViolationLogEntry entry = new()
            {
                Timestamp = DateTimeOffset.Now,
                Domain = domain,
                Action = "BLOCKED"
            };

            string json = JsonSerializer.Serialize(entry);

            lock (fileLock)
            {
                File.AppendAllText(
                    logFilePath,
                    json + Environment.NewLine
                );
            }
        }

        public List<ViolationLogEntry> GetAll()
        {
            List<ViolationLogEntry> logs = new();

            if (!File.Exists(logFilePath))
                return logs;

            string[] lines;

            lock (fileLock)
            {
                lines = File.ReadAllLines(logFilePath);
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    ViolationLogEntry? entry =
                        JsonSerializer.Deserialize<ViolationLogEntry>(line);

                    if (entry != null)
                    {
                        logs.Add(entry);
                    }
                }
                catch
                {
                    // Bozuk veya eksik log satırı varsa atla.
                }
            }

            return logs
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }

        private string ExtractDomain(string url)
        {
            try
            {
                Uri uri = new Uri(url);

                return uri.Host.ToLowerInvariant();
            }
            catch
            {
                return url;
            }
        }
    }
}