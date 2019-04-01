using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VideoLibrary;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Model;
using YouTubeDownloader.Models;

namespace YouTubeDownloader
{
    /// <summary>
    /// Interaction logic for ContentPage.xaml
    /// </summary>
    public partial class ContentPage : Page
    {
        public AccountModel account;
        public Dictionary<string, string> playlistIdCollection = new Dictionary<string, string>();

        public List<PlaylistModel> playlists;

        public ContentPage()
        {
            if (Properties.Settings.Default.IsLoggedIn == true)
            {
                account = new AccountModel(
                    Properties.Settings.Default.AccountName,
                    Properties.Settings.Default.AccountEmail,
                    Properties.Settings.Default.RefreshToken,
                    Properties.Settings.Default.AccessToken,
                    Properties.Settings.Default.AccessTokenCreationDate,
                    Properties.Settings.Default.AccountPicture
                    );
            }
            else
                account = ((MainWindow)System.Windows.Application.Current.MainWindow).account;
            InitializeComponent();
            initializePlaylistTitles();                                       
            ProfileImage.Source = new BitmapImage(new Uri(account.PictureUrl));
            ProfileName.Content = account.Name;

        }

        private dynamic requestPlaylistTitles()
        {
            if (Properties.Settings.Default.AccessTokenCreationDate.AddSeconds(3900) < DateTime.Now)
                Authenticator.RefreshToken();
            // add token refresh logic
            string requestPlaylistUri = "https://www.googleapis.com/youtube/v3/playlists?part=snippet&mine=true";

            HttpWebRequest requestPlaylist = (HttpWebRequest)WebRequest.Create(requestPlaylistUri);
            requestPlaylist.Method = "GET";
            requestPlaylist.Headers.Add(string.Format("Authorization: Bearer {0}", Properties.Settings.Default.AccessToken));
            requestPlaylist.ContentType = "application/x-www-form-urlencoded";
            requestPlaylist.Accept = "Accept=text/html,application/xhtml+xml,applicaton/xml;q=0.9*/*;q=0.8";

            try
            {
                WebResponse playlistResponse = requestPlaylist.GetResponse();
                using (StreamReader userInfoResponseReader = new StreamReader(playlistResponse.GetResponseStream()))
                {
                    // reads response body
                    string userInfoResponseText = userInfoResponseReader.ReadToEnd();
                    var JsonResponse = JsonConvert.DeserializeObject<dynamic>(userInfoResponseText);
                    return JsonResponse;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("You don't have any playlists.");
                return null;

            }
        }

        private void initializePlaylistTitles()
        {
            playlists = new List<PlaylistModel>();
            var json = requestPlaylistTitles();

            foreach (var item in json.items)
            {
                PlaylistModel model = new PlaylistModel();
                model.Name = item.snippet.title;
                model.PlaylistId = item.id;
                playlists.Add(model);
            }
            json = RequestLikedVideos();
            playlists.Add(new PlaylistModel() { Name = "Liked videos", PlaylistId = json });
            PlaylistComboBox.ItemsSource = playlists;
            
        }

        private async Task RenderElementsInListAsync(PageFlagEnum flag)
        {
            PlaylistModel playlist = (PlaylistModel) PlaylistComboBox.SelectedItem;

                var json = await RequestPlaylistItemsAsync(playlist, flag);

            System.Diagnostics.Debug.WriteLine((string)Convert.ToString(json));

            // Dinamically generate the ListBox Elements based on the api response
            playlist.Videos = new List<VideoModel>();
                foreach (var x in json.items)
                {
                    if (Convert.ToString(x.snippet.title) != "Private video") {    
                        playlist.Videos.Add(new VideoModel(
                            Convert.ToString(x.snippet.thumbnails.medium.url),
                            Convert.ToString(x.snippet.title),
                            Convert.ToString(x.snippet.resourceId.videoId),
                            Convert.ToString(x.snippet.channelTitle),
                            Convert.ToString(x.snippet.publishedAt)));
                    }
                }

            if (!string.IsNullOrEmpty(Convert.ToString(json.nextPageToken)))
            {
                playlist.NextPageToken = Convert.ToString(json.nextPageToken);
                NextQueryButton.Visibility = Visibility.Visible;
            }
            else
            {
                playlist.NextPageToken = "";
                NextQueryButton.Visibility = Visibility.Hidden;
            }
            if (!string.IsNullOrEmpty(Convert.ToString(json.prevPageToken)))
            {
                playlist.PrevPageToken = Convert.ToString(json.prevPageToken);
                PrevQueryButton.Visibility = Visibility.Visible;
            }
            else
            {
                playlist.PrevPageToken = "";
                PrevQueryButton.Visibility = Visibility.Hidden;
            }

            ListBox.ItemsSource = playlist.Videos;

        }

        private string RequestLikedVideos()
        {
            if (Properties.Settings.Default.AccessTokenCreationDate.AddSeconds(3900) < DateTime.Now)
               Authenticator.RefreshToken();
            string requestUri = string.Format("https://www.googleapis.com/youtube/v3/channels?part=contentDetails&mine=true");

            HttpWebRequest requestPlaylist = (HttpWebRequest)WebRequest.Create(requestUri);
            requestPlaylist.Method = "GET";
            requestPlaylist.Headers.Add(string.Format("Authorization: Bearer {0}", Properties.Settings.Default.AccessToken));

            System.Diagnostics.Debug.WriteLine("++++++++++++++++++++");
            System.Diagnostics.Debug.WriteLine(Properties.Settings.Default.AccessToken);
            System.Diagnostics.Debug.WriteLine("++++++++++++++++++++");

            requestPlaylist.ContentType = "application/x-www-form-urlencoded";
            requestPlaylist.Accept = "Accept=text/html,application/xhtml+xml,applicaton/xml;q=0.9*/*;q=0.8";

            WebResponse playlistResponse = requestPlaylist.GetResponse();
            using (StreamReader userInfoResponseReader = new StreamReader(playlistResponse.GetResponseStream()))
            {
                // reads response body
                string userInfoResponseText = userInfoResponseReader.ReadToEnd();
                var JsonResponse = JsonConvert.DeserializeObject<dynamic>(userInfoResponseText);
                return Convert.ToString(JsonResponse.items[0].contentDetails.relatedPlaylists.likes);
                // return the ID of the Liked Videos playlist
            }

        }

        private async Task<dynamic> RequestPlaylistItemsAsync(PlaylistModel playlist, PageFlagEnum flag)
        {
            if (Properties.Settings.Default.AccessTokenCreationDate.AddSeconds(3900) < DateTime.Now)
                Authenticator.RefreshToken();
            // add token refresh logic
            string pageToken = "";
            switch (flag)
            {
                case PageFlagEnum.Current:
                    break;
                case PageFlagEnum.Next:
                    pageToken = playlist.NextPageToken;
                    break;
                case PageFlagEnum.Prev:
                    pageToken = playlist.PrevPageToken;
                    break;
            }
           string requestPlaylistUri = string.Format("https://www.googleapis.com/youtube/v3/playlistItems?part=snippet&maxResults=5&playlistId={0}&pageToken={1}",
               playlist.PlaylistId, pageToken);


            HttpWebRequest requestPlaylist = (HttpWebRequest)WebRequest.Create(requestPlaylistUri);
            requestPlaylist.Method = "GET";
            requestPlaylist.Headers.Add(string.Format("Authorization: Bearer {0}", Properties.Settings.Default.AccessToken));
            requestPlaylist.ContentType = "application/x-www-form-urlencoded";
            requestPlaylist.Accept = "Accept=text/html,application/xhtml+xml,applicaton/xml;q=0.9*/*;q=0.8";

            WebResponse playlistResponse = await requestPlaylist.GetResponseAsync();
            using (StreamReader userInfoResponseReader = new StreamReader(playlistResponse.GetResponseStream()))
            {
                // reads response body
                string userInfoResponseText = await userInfoResponseReader.ReadToEndAsync();
                var JsonResponse = JsonConvert.DeserializeObject<dynamic>(userInfoResponseText);

                return JsonResponse;
            }

        }

        private async void NextQueryButton_Click(object sender, RoutedEventArgs e)
        {
             await RenderElementsInListAsync(PageFlagEnum.Next);
        }

        private async void PrevQueryButton_Click(object sender, RoutedEventArgs e)
        {
             await RenderElementsInListAsync(PageFlagEnum.Prev);
        }

        private void ChangeDownloadDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                DownloadDirectoryLabel.Content = dialog.SelectedPath;
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (Convert.ToString(DownloadDirectoryLabel.Content).Equals("Please choose a download directory"))
            {
                MessageBox.Show("Please choose a download directory first.");
            }
            else if (ListBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please select an item from the playlist.");
            }
            else
            {
                var videoModel = (VideoModel)ListBox.SelectedItem;

                using (var service = Client.For(YouTube.Default))
                {
                    var video = service.GetVideoAsync(string.Format("https://www.youtube.com/watch?v={0}", videoModel.VideoId));

                    DownloadStatusLabel.Content = "Downloading...";
                    var temp = await video;
                    string path = System.IO.Path.Combine(Convert.ToString(DownloadDirectoryLabel.Content), temp.FullName);
                    File.WriteAllBytes(path, temp.GetBytes());

                    if (Mp4Radio.IsChecked == true)
                        DownloadStatusLabel.Content = "Download finished.";
                    else if (Mp3Radio.IsChecked == true)
                    {
                        await FFmpeg.GetLatestVersion();
                        string output = Path.ChangeExtension(path, Xabe.FFmpeg.Enums.FileExtensions.Mp3);
                        IConversionResult result = await Conversion.ExtractAudio(path, output).Start();
                        DownloadStatusLabel.Content = "Download finished.";
                    }
                }
            }
        }

        private void Mp4Radio_Checked(object sender, RoutedEventArgs e)
        { 
            this.Mp4Radio.IsChecked = true;
        }

        private void Mp3Radio_Checked(object sender, RoutedEventArgs e)
        {
            this.Mp3Radio.IsChecked = true;
        }

        private async void PlaylistComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tmp = (PlaylistModel)PlaylistComboBox.SelectedItem;
            await RenderElementsInListAsync(PageFlagEnum.Current);
        }

        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Reset();
            ((MainWindow)System.Windows.Application.Current.MainWindow).MainFrame.Content = new LogInPage();
        }
    }
}
