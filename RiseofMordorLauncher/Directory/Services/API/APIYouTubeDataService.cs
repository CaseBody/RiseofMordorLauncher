using System;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;

namespace RiseofMordorLauncher
{
    public class APIYouTubeDataService : IYouTubeDataService
    {
        public async Task<YouTubeData> GetYouTubeData()
        {
            try
            {
                var youtube_api = new YoutubeClient();
                var rom_channel_videos = await youtube_api.Channels.GetUploadsAsync("UCangGj6TUjUb9ri8CXcxQuw");

                var data = new YouTubeData();
                PlaylistVideo firstNonShortVideo = null;

                foreach (var item in rom_channel_videos)
                {
                    if (item.Duration.HasValue == false)
                    {
                        continue;
                    }

                    if (item.Duration.Value > TimeSpan.FromSeconds(90))
                    {
                        firstNonShortVideo = item;
                        break;
                    }
                }

                //data.ThumbnailUrl = firstNonShortVideo.Thumbnails.ElementAt(0).Url;
                data.ThumbnailUrl = $"https://img.youtube.com/vi/{firstNonShortVideo.Id}/maxresdefault.jpg";
                data.VideoUrl = $"https://youtube.com/embed/{firstNonShortVideo.Id}";
                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                var data = new YouTubeData();
                return data;
            }

        }

    }
}
