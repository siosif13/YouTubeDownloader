using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeDownloader.Models
{
    public class VideoModel
    {
        public string ImageUrl { get; set; }
        public string Title { get; set; }
        public string VideoId { get; set; }
        public string Channel { get; set; }
        public string Date { get; set; }

        public VideoModel(string imageUrl, string title, string videoId, string channel, string date)
        {
            this.ImageUrl = imageUrl;
            this.Title = title;
            this.VideoId = videoId;
            this.Channel = channel;
            this.Date = date;
        }
    }
}
