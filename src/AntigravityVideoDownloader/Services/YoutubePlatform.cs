using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AntigravityVideoDownloader.Models;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace AntigravityVideoDownloader.Services
{
    public class YoutubePlatform : IVideoPlatform
    {
        private readonly YoutubeDL _ytdl;

        public YoutubePlatform()
        {
            _ytdl = new YoutubeDL
            {
                YoutubeDLPath = "yt-dlp.exe",
                FFmpegPath = "ffmpeg.exe"
            };
        }

        public async Task InitializeAsync()
        {
            if (!System.IO.File.Exists(_ytdl.YoutubeDLPath))
            {
                await YoutubeDLSharp.Utils.DownloadYtDlp();
            }
            if (!System.IO.File.Exists(_ytdl.FFmpegPath))
            {
                await YoutubeDLSharp.Utils.DownloadFFmpeg();
            }
        }

        public async Task<VideoInfoResult> GetVideoInfoAsync(string url)
        {
            var res = await _ytdl.RunVideoDataFetch(url);
            if (res.Success)
            {
                var availableResolutions = new List<VideoResolution>();
                var formats = res.Data.Formats
                    .Where(f => f.Height != null && !string.IsNullOrEmpty(f.Extension) && f.Extension != "none")
                    .GroupBy(f => f.Height)
                    .Select(g => g.OrderByDescending(f => f.Bitrate ?? 0).First())
                    .OrderByDescending(f => f.Height)
                    .ToList();

                foreach (var f in formats)
                {
                    availableResolutions.Add(new VideoResolution
                    {
                        FormatId = f.FormatId,
                        Resolution = $"{f.Height}p",
                        Extension = f.Extension ?? "mp4"
                    });
                }

                if (!availableResolutions.Any())
                {
                    availableResolutions.Add(new VideoResolution { FormatId = "best", Resolution = "Best available", Extension = "mp4" });
                }

                return new VideoInfoResult
                {
                    Success = true,
                    Title = res.Data.Title,
                    Formats = availableResolutions
                };
            }

            return new VideoInfoResult
            {
                Success = false,
                ErrorMessage = string.Join('\n', res.ErrorOutput)
            };
        }

        public async Task<DownloadResult> DownloadVideoAsync(DownloadItem item, IProgress<DownloadProgressData> progress, CancellationToken ct)
        {
            var ytProgress = new Progress<YoutubeDLSharp.DownloadProgress>(p =>
            {
                progress?.Report(new DownloadProgressData
                {
                    Progress = Math.Round(p.Progress * 100, 2),
                    DownloadSpeed = p.DownloadSpeed,
                    ETA = p.ETA
                });
            });

            _ytdl.OutputFolder = item.DestinationPath;

            var options = new OptionSet
            {
                Format = item.FormatId,
                MergeOutputFormat = DownloadMergeFormat.Mp4,
                ConcurrentFragments = 4
            };

            var res = await _ytdl.RunVideoDownload(
                item.Url,
                progress: ytProgress,
                ct: ct,
                overrideOptions: options
            );

            return new DownloadResult
            {
                Success = res.Success,
                ErrorMessage = res.Success ? string.Empty : string.Join('\n', res.ErrorOutput)
            };
        }
    }
}
