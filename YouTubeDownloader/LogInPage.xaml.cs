using System;
using System.Collections.Generic;
using System.Linq;
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

namespace YouTubeDownloader
{
    /// <summary>
    /// Interaction logic for LogInPage.xaml
    /// </summary>
    public partial class LogInPage : Page
    {
        public LogInPage()
        {
            InitializeComponent();
            ((MainWindow)System.Windows.Application.Current.MainWindow).MainFrame.NavigationService.RemoveBackEntry();
            ((MainWindow)System.Windows.Application.Current.MainWindow).MainFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
        }

        private async void LogInButton_Click(object sender, RoutedEventArgs e)
        {
            Authenticator auth = new Authenticator();
            ((MainWindow)System.Windows.Application.Current.MainWindow).account = await auth.GetAccountAccessAsync();
            ((MainWindow)System.Windows.Application.Current.MainWindow).MainFrame.Content = new ContentPage();
            ((MainWindow)System.Windows.Application.Current.MainWindow).Activate();
        }
    }
}
