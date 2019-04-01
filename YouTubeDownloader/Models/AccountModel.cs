using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeDownloader
{
    [Serializable]
    public class AccountModel
    {   
        public string Name { get; private set; }
        public string Email { get; private set; }
        public string RefreshToken { get; private set; }
        public string AccessToken { get; private set; }
        public DateTime AccessTokenCreationDate { get; private set; }
        public readonly int AccessTokenLifespan = 3600;
        public string PictureUrl { get; private set; }

        public AccountModel(string name, string email, string refreshToken, string accessToken, DateTime accessTokenCreationDate, string pictureUrl)
        {
            this.Name = name;
            this.Email = email;
            this.RefreshToken = refreshToken;
            this.AccessToken = accessToken;
            this.AccessTokenCreationDate = accessTokenCreationDate;
            this.PictureUrl = pictureUrl;
        }
    }
}
