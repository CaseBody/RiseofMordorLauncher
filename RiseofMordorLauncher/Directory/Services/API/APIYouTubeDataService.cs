using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

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
                data.ThumbnailUrl = rom_channel_videos.ElementAt(0).Thumbnails.ElementAt(0).Url;
                data.VideoUrl = $"https://youtube.com/embed/{rom_channel_videos.ElementAt(0).Id}";
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
