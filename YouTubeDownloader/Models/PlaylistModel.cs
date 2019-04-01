using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeDownloader.Models
{
    public class PlaylistModel
    {
        public string Name { get; set; }
        public string PlaylistId { get; set; }
        public List<VideoModel> Videos { get; set; }
        public string PrevPageToken { get; set; }
        public string NextPageToken { get; set; } 
    }
}
