using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;

namespace AntigravityVideoDownloader.Models
{
    public enum DownloadStatus
    {
        Queued,
        Downloading,
        Paused,
        Completed,
        Error
    }

    public partial class DownloadItem : ObservableObject
    {
        [ObservableProperty]
        private string _id;

        [ObservableProperty]
        private string _url;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _resolution;

        [ObservableProperty]
        private string _formatId;

        [ObservableProperty]
        private string _extension;

        [ObservableProperty]
        private DownloadStatus _status;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _downloadSpeed;

        [ObservableProperty]
        private string _eta;

        [ObservableProperty]
        private string _destinationPath;

        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public DownloadItem()
        {
            Id = Guid.NewGuid().ToString();
            Status = DownloadStatus.Queued;
            Progress = 0;
            DownloadSpeed = "";
            Eta = "";
        }
    }
}
