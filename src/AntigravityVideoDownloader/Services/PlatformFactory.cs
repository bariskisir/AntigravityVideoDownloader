using System.Threading.Tasks;

namespace AntigravityVideoDownloader.Services
{
    public static class PlatformFactory
    {
        private static YoutubePlatform _youtubePlatform;

        public static async Task<IVideoPlatform> GetPlatformAsync(string url)
        {
            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                if (_youtubePlatform == null)
                {
                    _youtubePlatform = new YoutubePlatform();
                    await _youtubePlatform.InitializeAsync();
                }
                return _youtubePlatform;
            }

            // Fallback to youtube-dl which supports many sites anyway
            if (_youtubePlatform == null)
            {
                _youtubePlatform = new YoutubePlatform();
                await _youtubePlatform.InitializeAsync();
            }
            return _youtubePlatform;
        }
    }
}
