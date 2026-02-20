using System;
using System.Text.Json.Serialization;

namespace AntigravityVideoDownloader.Models
{
    public class AppConfig
    {
        [JsonPropertyName("downloadPath")]
        public string DownloadPath { get; set; } = string.Empty;
    }
}
