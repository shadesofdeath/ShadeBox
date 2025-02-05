using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using iNKORE.UI.WPF.Modern.Controls;
using ShadeBox.Pages;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace ShadeBox
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient httpClient;

        public MainWindow()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            CheckApiStatus();
        }

        private async Task<bool> CheckApiStatus()
        {
            try
            {
                string url = "https://ythls.kekikakademi.org/sinewix/movies/10751/1";
                var response = await httpClient.GetStringAsync(url);

                if (response.Contains("Error 1033") || response.Contains("Cloudflare Tunnel error"))
                {
                    NavView.IsEnabled = false;
                    ContentFrame.IsEnabled = false;
                    MessageBox.Show(
                        "Şu anda API bakım çalışması yapılmaktadır. Lütfen daha sonra tekrar deneyiniz.",
                        "Bakım Bildirimi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                NavView.IsEnabled = false;
                ContentFrame.IsEnabled = false;
                MessageBox.Show(
                    "Şu anda API bakım çalışması yapılmaktadır. Lütfen daha sonra tekrar deneyiniz.",
                    "Bakım Bildirimi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }
        }

        private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (await CheckApiStatus() && args.SelectedItem is NavigationViewItem selectedItem)
            {
                switch (selectedItem.Tag.ToString())
                {
                    case "FilmPage":
                        ContentFrame.Navigate(new FilmPage());
                        break;
                    case "DiziPage":
                        ContentFrame.Navigate(new SeriesPage());
                        break;
                    case "AnimePage":
                        ContentFrame.Navigate(new AnimesPage());
                        break;
                    case "CanliTv":
                        ContentFrame.Navigate(new CanliTv());
                        break;
                    case "SettingsPage":
                        ContentFrame.Navigate(new SettingsPage());
                        break;
                }
            }
        }
    }
}