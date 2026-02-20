namespace AntigravityVideoDownloader.Models
{
    public class VideoResolution
    {
        public string FormatId { get; set; }
        public string Resolution { get; set; }
        public string Extension { get; set; }
        public string DisplayName => $"{Resolution} ({Extension})";
    }
}
