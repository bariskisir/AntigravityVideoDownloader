using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AntigravityVideoDownloader.Models;
using AntigravityVideoDownloader.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AntigravityVideoDownloader.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _videoUrl;

        [ObservableProperty]
        private string _videoTitle;

        [ObservableProperty]
        private string _downloadFolder;

        [ObservableProperty]
        private ObservableCollection<VideoResolution> _availableResolutions;

        [ObservableProperty]
        private VideoResolution _selectedResolution;

        [ObservableProperty]
        private ObservableCollection<DownloadItem> _downloadQueue;

        [ObservableProperty]
        private bool _isChecking;

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private bool _isSingleUrlMode;

        [ObservableProperty]
        private string _totalDownloadSpeed;

        private string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public MainViewModel()
        {
            TotalDownloadSpeed = "";
            string defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AntigravityVideos");

            string savedFolder = defaultFolder;

            if (File.Exists(ConfigPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(ConfigPath).Trim();
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        var config = JsonSerializer.Deserialize<AppConfig>(jsonContent);
                        if (config != null && !string.IsNullOrWhiteSpace(config.DownloadPath))
                        {
                            savedFolder = config.DownloadPath;
                        }
                    }
                }
                catch
                {
                    savedFolder = defaultFolder;
                }
            }

            if (!Directory.Exists(savedFolder))
            {
                try
                {
                    Directory.CreateDirectory(savedFolder);
                }
                catch
                {
                    savedFolder = defaultFolder;
                    Directory.CreateDirectory(savedFolder);
                }
            }

            SaveConfig(savedFolder);

            DownloadFolder = savedFolder;
            AvailableResolutions = new ObservableCollection<VideoResolution>();
            DownloadQueue = new ObservableCollection<DownloadItem>();

            StatusMessage = "Ready";
            CheckDependencies();
        }

        private void SaveConfig(string folderPath)
        {
            try
            {
                var config = new AppConfig { DownloadPath = folderPath };
                var jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, jsonContent);
            }
            catch
            {
            }
        }

        [RelayCommand]
        private void ClearQueue()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to clear the entire download queue?",
                "Clear Queue",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                foreach (var item in DownloadQueue.ToList())
                {
                    if (item.Status == DownloadStatus.Downloading)
                    {
                        PauseDownload(item);
                    }
                }
                DownloadQueue.Clear();
                StatusMessage = "Download queue cleared.";
            }
        }

        partial void OnVideoUrlChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsSingleUrlMode = false;
                return;
            }
            IsSingleUrlMode = !value.Contains('\n') && !value.Contains('\r');
            if (!IsSingleUrlMode)
            {
                AvailableResolutions.Clear();
                SelectedResolution = null;
                VideoTitle = "";
            }
        }

        private async void CheckDependencies()
        {
            StatusMessage = "Checking dependencies...";
            await PlatformFactory.GetPlatformAsync("youtube.com");
            StatusMessage = "Ready";
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = DownloadFolder
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadFolder = dialog.SelectedPath;
                SaveConfig(DownloadFolder);
            }
        }

        [RelayCommand]
        private async Task CheckOrAddVideo()
        {
            if (string.IsNullOrWhiteSpace(VideoUrl))
            {
                System.Windows.MessageBox.Show("Please enter valid URL(s).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var urls = VideoUrl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .Where(u => !string.IsNullOrWhiteSpace(u))
                               .Select(u => u.Trim())
                               .ToList();

            if (!urls.Any()) return;

            IsChecking = true;
            StatusMessage = "Processing URLs...";

            try
            {
                if (urls.Count == 1)
                {
                    var platform = await PlatformFactory.GetPlatformAsync(urls[0]);
                    var res = await platform.GetVideoInfoAsync(urls[0]);

                    if (res.Success)
                    {
                        VideoTitle = res.Title;
                        AvailableResolutions.Clear();

                        foreach (var f in res.Formats)
                        {
                            AvailableResolutions.Add(f);
                        }

                        if (AvailableResolutions.Any())
                        {
                            SelectedResolution = AvailableResolutions.FirstOrDefault(r => r.Resolution == "1080p")
                                               ?? AvailableResolutions.First();
                        }

                        StatusMessage = "Video info loaded. Select resolution and ADD TO QUEUE.";
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Could not retrieve info: {res.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        StatusMessage = "Error fetching video.";
                    }
                }
                else
                {
                    foreach (var url in urls)
                    {
                        var item = new DownloadItem
                        {
                            Url = url,
                            Title = "Fetching info...",
                            Resolution = "Best",
                            FormatId = "bestvideo+bestaudio/best",
                            Extension = "mp4",
                            DestinationPath = DownloadFolder
                        };
                        DownloadQueue.Add(item);
                        _ = FetchTitleAndSave(item);
                        _ = StartDownload(item);
                    }
                    StatusMessage = $"{urls.Count} videos added to queue.";
                    VideoUrl = string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsChecking = false;
            }
        }

        private async Task FetchTitleAndSave(DownloadItem item)
        {
            var platform = await PlatformFactory.GetPlatformAsync(item.Url);
            var res = await platform.GetVideoInfoAsync(item.Url);

            if (res.Success)
            {
                item.Title = res.Title;
            }
            else
            {
                item.Title = "Unknown Title";
            }
        }

        [RelayCommand]
        private void AddSingleToQueue()
        {
            if (string.IsNullOrWhiteSpace(VideoUrl) || SelectedResolution == null) return;
            var urls = VideoUrl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (urls.Count != 1) return;

            var item = new DownloadItem
            {
                Url = urls[0],
                Title = VideoTitle,
                Resolution = SelectedResolution.Resolution,
                FormatId = $"{SelectedResolution.FormatId}+bestaudio/best",
                Extension = SelectedResolution.Extension,
                DestinationPath = DownloadFolder
            };

            DownloadQueue.Add(item);

            _ = StartDownload(item);

            StatusMessage = "Added to queue & Starting.";
            VideoUrl = string.Empty;
            AvailableResolutions.Clear();
            SelectedResolution = null;
            VideoTitle = string.Empty;
        }

        [RelayCommand]
        private async Task StartDownload(DownloadItem item)
        {
            if (item == null || item.Status == DownloadStatus.Downloading || item.Status == DownloadStatus.Completed) return;

            item.Status = DownloadStatus.Downloading;
            item.Eta = "--:--";
            item.DownloadSpeed = "0 MB/s";

            item.CancellationTokenSource = new CancellationTokenSource();

            var progress = new Progress<DownloadProgressData>(p =>
            {
                item.Progress = p.Progress;
                item.DownloadSpeed = p.DownloadSpeed;
                item.Eta = p.ETA;
                UpdateTotalSpeed();
            });

            try
            {
                var platform = await PlatformFactory.GetPlatformAsync(item.Url);
                var res = await platform.DownloadVideoAsync(item, progress, item.CancellationTokenSource.Token);

                if (res.Success)
                {
                    item.Status = DownloadStatus.Completed;
                    item.Progress = 100;
                    item.Eta = "00:00";
                    item.DownloadSpeed = "Complete";
                }
                else if (item.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    item.Status = DownloadStatus.Paused;
                    item.Eta = "";
                    item.DownloadSpeed = "";
                }
                else
                {
                    item.Status = DownloadStatus.Error;
                    System.Windows.MessageBox.Show($"Download failed for {item.Title}:\n{res.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                item.Status = DownloadStatus.Paused;
            }
            catch (Exception)
            {
                item.Status = DownloadStatus.Error;
            }
            finally
            {
                item.CancellationTokenSource?.Dispose();
                item.CancellationTokenSource = null;
                UpdateTotalSpeed();
            }
        }

        private void UpdateTotalSpeed()
        {
            double totalBytesPerSec = 0;
            foreach (var dl in DownloadQueue)
            {
                if (dl.Status == DownloadStatus.Downloading && !string.IsNullOrWhiteSpace(dl.DownloadSpeed))
                {
                    totalBytesPerSec += ParseYtdlSpeed(dl.DownloadSpeed);
                }
            }

            if (totalBytesPerSec <= 0)
            {
                TotalDownloadSpeed = "";
                return;
            }

            if (totalBytesPerSec > 1024 * 1024)
                TotalDownloadSpeed = $"Total Speed: {(totalBytesPerSec / (1024 * 1024)).ToString("F2")} MiB/s";
            else if (totalBytesPerSec > 1024)
                TotalDownloadSpeed = $"Total Speed: {(totalBytesPerSec / 1024).ToString("F2")} KiB/s";
            else
                TotalDownloadSpeed = $"Total Speed: {totalBytesPerSec.ToString("F2")} B/s";
        }

        private double ParseYtdlSpeed(string speedStr)
        {
            var clean = speedStr.Trim().Replace("s", "").Replace("/", "").ToLower();
            double multiplier = 1;

            if (clean.EndsWith("kib")) { multiplier = 1024; clean = clean.Replace("kib", ""); }
            else if (clean.EndsWith("mib")) { multiplier = 1024 * 1024; clean = clean.Replace("mib", ""); }
            else if (clean.EndsWith("gib")) { multiplier = 1024 * 1024 * 1024; clean = clean.Replace("gib", ""); }

            if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                return val * multiplier;

            if (double.TryParse(clean.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val2))
                return val2 * multiplier;

            return 0;
        }

        [RelayCommand]
        private void PauseDownload(DownloadItem item)
        {
            if (item != null && item.Status == DownloadStatus.Downloading && item.CancellationTokenSource != null)
            {
                item.CancellationTokenSource.Cancel();
                item.Status = DownloadStatus.Paused;
            }
        }

        [RelayCommand]
        private void RemoveDownload(DownloadItem item)
        {
            if (item == null) return;
            if (item.Status == DownloadStatus.Downloading)
            {
                PauseDownload(item);
            }
            DownloadQueue.Remove(item);
        }
    }
}
