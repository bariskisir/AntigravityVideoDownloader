using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AntigravityVideoDownloader.Models;

namespace AntigravityVideoDownloader.Services
{
    public interface IVideoPlatform
    {
        Task<VideoInfoResult> GetVideoInfoAsync(string url);
        Task<DownloadResult> DownloadVideoAsync(DownloadItem item, IProgress<DownloadProgressData> progress, CancellationToken ct);
    }

    public class VideoInfoResult
    {
        public bool Success { get; set; }
        public string Title { get; set; }
        public List<VideoResolution> Formats { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class DownloadResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class DownloadProgressData
    {
        public double Progress { get; set; }
        public string DownloadSpeed { get; set; }
        public string ETA { get; set; }
    }
}
