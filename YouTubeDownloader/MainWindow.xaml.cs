using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;
using VideoLibrary;
using YouTubeDownloader;

namespace YouTubeDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal AccountModel account;

        public MainWindow()
        {
            InitializeComponent();
            Properties.Settings.Default.Reset();
            if (Properties.Settings.Default.IsLoggedIn)
                MainFrame.Content = new ContentPage();
            else
                MainFrame.Content = new LogInPage(); 
        }

    }
}