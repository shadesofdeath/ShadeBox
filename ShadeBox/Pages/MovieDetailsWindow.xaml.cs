using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace ShadeBox.Pages
{
    public class FavoriteMoviesDb
    {
        private static string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorite_movies.db");
        private static string ConnectionString => $"Data Source={DbPath};Version=3;";

        public static void InitializeDatabase()
        {
            try
            {
                // Önce veritabanı dosyasını oluştur
                if (!File.Exists(DbPath))
                {
                    SQLiteConnection.CreateFile(DbPath);
                }

                // Bağlantıyı aç
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();

                    // Tabloyu oluştur
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS favorites (
                                movie_id INTEGER PRIMARY KEY,
                                title TEXT NOT NULL,
                                added_date DATETIME DEFAULT (datetime('now','localtime'))
                            );";

                        cmd.ExecuteNonQuery();
                    }
                }

                // Test sorgusu yap
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='favorites';";
                        var result = cmd.ExecuteScalar();

                        if (result == null)
                        {
                            throw new Exception("Tablo oluşturulamadı!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veritabanı başlatılırken hata oluştu: {ex.Message}", "Veritabanı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                throw; // Hatayı yukarı fırlat
            }
        }

        public static bool IsMovieFavorited(int movieId)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1) FROM favorites WHERE movie_id = @movieId";
                        cmd.Parameters.AddWithValue("@movieId", movieId);

                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Favori kontrolü yapılırken hata oluştu: {ex.Message}", "Veritabanı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static void AddToFavorites(int movieId, string title)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO favorites (movie_id, title) 
                            VALUES (@movieId, @title)";

                        cmd.Parameters.AddWithValue("@movieId", movieId);
                        cmd.Parameters.AddWithValue("@title", title);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Favorilere eklenirken hata oluştu: {ex.Message}", "Veritabanı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void RemoveFromFavorites(int movieId)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM favorites WHERE movie_id = @movieId";
                        cmd.Parameters.AddWithValue("@movieId", movieId);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Favorilerden kaldırılırken hata oluştu: {ex.Message}", "Veritabanı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public partial class MovieDetailsWindow : Window
    {
        private readonly HttpClient _httpClient;
        private MovieDetailApiResponse _movieDetails;
        private bool _isFavorited;
        public MovieDetailsWindow(int movieId)
        {
            InitializeComponent();

            try
            {
                // Veritabanını başlat
                FavoriteMoviesDb.InitializeDatabase();

                _httpClient = new HttpClient();
                // Wire up the watch button click event
                var watchButton = this.FindName("WatchButton") as Button;
                if (watchButton != null)
                {
                    watchButton.Click += WatchButton_Click;
                }
                // Favori butonu olayını bağla
                var favoriteButton = this.FindName("FavoriteButton") as Button;
                if (favoriteButton != null)
                {
                    favoriteButton.Click += FavoriteButton_Click;
                }

                LoadMovieDetails(movieId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pencere başlatılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void UpdateFavoriteButtonText()
        {
            var favoriteText = this.FindName("FavoriteText") as TextBlock;
            if (favoriteText != null)
            {
                favoriteText.Text = _isFavorited ? "Favorilerden Kaldır" : "Favorilere Ekle";
            }
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_movieDetails == null) return;

                if (_isFavorited)
                {
                    FavoriteMoviesDb.RemoveFromFavorites(_movieDetails.id);
                    _isFavorited = false;
                }
                else
                {
                    FavoriteMoviesDb.AddToFavorites(_movieDetails.id, _movieDetails.title);
                    _isFavorited = true;
                }

                UpdateFavoriteButtonText();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Favori işlemi sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WatchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_movieDetails?.videos == null || _movieDetails.videos.Count == 0)
                {
                    MessageBox.Show("Bu film için izleme linki bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var videoLink = _movieDetails.videos[0].link;
                if (string.IsNullOrEmpty(videoLink))
                {
                    MessageBox.Show("Geçerli bir video linki bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string mpvPath;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    // Windows için mpv'nin uygulama dizininde olup olmadığını kontrol et
                    string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    mpvPath = Path.Combine(exePath, "mpv", "mpvnet.exe");

                    if (!File.Exists(mpvPath))
                    {
                        MessageBox.Show("MPV player bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    // Linux için global mpv'yi kullan
                    Process whichMpv = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "which",
                            Arguments = "mpv",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    whichMpv.Start();
                    mpvPath = whichMpv.StandardOutput.ReadLine();
                    whichMpv.WaitForExit();

                    if (string.IsNullOrEmpty(mpvPath))
                    {
                        MessageBox.Show("Linux sistemde MPV yüklü değil.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Kullanıcı ayarlarına göre MPV argümanlarını oluştur
                List<string> arguments = new List<string>
        {
            $"\"{videoLink}\"",
            "--sub-font-size=18"
        };

                if (Settings.Default.SavePositionOnQuit)
                    arguments.Add("--save-position-on-quit=yes");

                if (Settings.Default.HardwareAcceleration)
                    arguments.Add("--hwdec=auto");

                if (Settings.Default.SubtitlesEnabled)
                    arguments.Add("--sid=1");
                else
                    arguments.Add("--sid=no");

                if (Settings.Default.RememberVolume)
                    arguments.Add("--volume-max=100");

                if (Settings.Default.LowLatencyMode)
                    arguments.Add("--video-sync=display-resample");

                if (Settings.Default.AutoFullscreen)
                    arguments.Add("--fullscreen");

                if (Settings.Default.AutoExit)
                    arguments.Add("--idle=once");

                if (Settings.Default.SubtitleShadow)
                    arguments.Add("--sub-shadow-offset=2");

                if (Settings.Default.BassBoost)
                    arguments.Add("--af=bass=10");

                if (Settings.Default.PowerSave)
                    arguments.Add("--video-sync=audio");

                if (Settings.Default.Loop)
                    arguments.Add("--loop=inf");

                if (Settings.Default.BestQuality)
                    arguments.Add("--scale=ewa_lanczossharp");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = mpvPath,
                    Arguments = string.Join(" ", arguments),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Video oynatılırken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private string FormatRuntime(string runtimeInMinutes)
        {
            if (string.IsNullOrEmpty(runtimeInMinutes) || !int.TryParse(runtimeInMinutes, out int minutes))
                return string.Empty;

            int hours = minutes / 60;
            int remainingMinutes = minutes % 60;

            if (hours > 0)
            {
                return remainingMinutes > 0
                    ? $"• {hours} Saat {remainingMinutes} Dakika"
                    : $"• {hours} Saat";
            }

            return $"• {minutes} Dakika";
        }
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_movieDetails?.videos == null || _movieDetails.videos.Count == 0)
                {
                    MessageBox.Show("Bu film için indirme linki bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var videoLink = _movieDetails.videos[0].link;
                if (string.IsNullOrEmpty(videoLink))
                {
                    MessageBox.Show("Geçerli bir video linki bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var fileName = $"{_movieDetails.title}.mp4";
                var downloadWindow = new DownloadProgressWindow(videoLink, fileName);
                downloadWindow.Owner = this;
                downloadWindow.Show();
                await downloadWindow.StartDownload();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İndirme başlatılırken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void LoadMovieDetails(int movieId)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"https://ythls.kekikakademi.org/sinewix/movie/{movieId}");
                _movieDetails = JsonSerializer.Deserialize<MovieDetailApiResponse>(response);
                _isFavorited = FavoriteMoviesDb.IsMovieFavorited(movieId);
                UpdateFavoriteButtonText();
                if (!string.IsNullOrEmpty(_movieDetails.poster_path))
                {
                    PosterImage.Source = new BitmapImage(new Uri(_movieDetails.poster_path));
                }

                // Set text content
                TitleBlock.Text = _movieDetails.title;
                OriginalTitleBlock.Text = _movieDetails.original_name;
                RatingBlock.Text = _movieDetails.vote_average.ToString("0.0");

                if (DateTime.TryParse(_movieDetails.release_date, out DateTime releaseDate))
                {
                    YearBlock.Text = releaseDate.Year.ToString();
                }

                // Format runtime in hours and minutes
                if (!string.IsNullOrEmpty(_movieDetails.runtime))
                {
                    RuntimeBlock.Text = FormatRuntime(_movieDetails.runtime);
                }

                OverviewBlock.Text = _movieDetails.overview;

                // Clear existing items
                GenresPanel.Children.Clear();
                CastPanel.Children.Clear();

                // Add genres
                foreach (var genre in _movieDetails.genres)
                {
                    var genreBorder = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10, 5, 10, 5),
                        Margin = new Thickness(0, 0, 5, 0)
                    };

                    genreBorder.Child = new TextBlock
                    {
                        Text = genre.name,
                        Foreground = System.Windows.Media.Brushes.White
                    };

                    GenresPanel.Children.Add(genreBorder);
                }

                // Add cast members (limit to first 8)
                var castLimit = Math.Min(_movieDetails.casterslist.Count, 8);
                for (int i = 0; i < castLimit; i++)
                {
                    var actor = _movieDetails.casterslist[i];
                    var actorContainer = new StackPanel
                    {
                        Width = 90,
                        Margin = new Thickness(0, 0, 10, 20)
                    };

                    var containerBorder = new Border
                    {
                        Width = 70,
                        Height = 70,
                        Background = System.Windows.Media.Brushes.Gray,  // Placeholder renk
                        CornerRadius = new CornerRadius(60),
                    };

                    var viewbox = new Viewbox
                    {
                        Width = 70,
                        Height = 70
                    };

                    var ellipse = new System.Windows.Shapes.Ellipse
                    {
                        Width = 70,
                        Height = 70,
                        Fill = new System.Windows.Media.ImageBrush
                        {
                            Stretch = System.Windows.Media.Stretch.UniformToFill
                        }
                    };

                    if (!string.IsNullOrEmpty(actor.profile_path))
                    {
                        var imageBrush = (System.Windows.Media.ImageBrush)ellipse.Fill;
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(actor.profile_path);
                        image.EndInit();
                        imageBrush.ImageSource = image;
                    }

                    viewbox.Child = ellipse;
                    containerBorder.Child = viewbox;

                    var nameBlock = new TextBlock
                    {
                        Text = actor.name,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0),
                        FontSize = 14,
                        TextAlignment = TextAlignment.Center,
                        Foreground = System.Windows.Media.Brushes.White,
                        Width = 70
                    };

                    var characterBlock = new TextBlock
                    {
                        Text = actor.character ?? "Unknown Role",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0),
                        FontSize = 12,
                        TextAlignment = TextAlignment.Center,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        Width = 70
                    };

                    actorContainer.Children.Add(containerBorder);
                    actorContainer.Children.Add(nameBlock);
                    actorContainer.Children.Add(characterBlock);
                    CastPanel.Children.Add(actorContainer);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Film detayları yüklenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }


        }
    }
    public class MovieDetailApiResponse
    {
        public int id { get; set; }
        public int tmdb_id { get; set; }
        public string title { get; set; }
        public string imdb_external_id { get; set; }
        public string original_name { get; set; }
        public string subtitle { get; set; }
        public string overview { get; set; }
        public string poster_path { get; set; }
        public string backdrop_path { get; set; }
        public string backdrop_path_tv { get; set; }
        public string preview_path { get; set; }
        public double vote_average { get; set; }
        public string trailer_url { get; set; }
        public int vote_count { get; set; }
        public double popularity { get; set; }
        public string runtime { get; set; }
        public int views { get; set; }
        public int featured { get; set; }
        public int premuim { get; set; }
        public int active { get; set; }
        public string release_date { get; set; }
        public int skiprecap_start_in { get; set; }
        public int hasrecap { get; set; }
        public int pinned { get; set; }
        public int enable_stream { get; set; }
        public int enable_media_download { get; set; }
        public int enable_ads_unlock { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public List<CastMember> casterslist { get; set; }
        public int substype { get; set; }
        public List<object> networkslist { get; set; }
        public string genresname { get; set; }
        public List<Genre> genres { get; set; }
        public List<Video> videos { get; set; }
        public List<object> downloads { get; set; }
        public List<object> substitles { get; set; }
        public List<SpokenLanguage> spoken_languages { get; set; }
        public List<Collection> belongs_to_collection { get; set; }
        public List<object> certifications { get; set; }
        public List<object> comments { get; set; }
    }

    public class CastMember
    {
        public int id { get; set; }
        public string name { get; set; }
        public string original_name { get; set; }
        public string profile_path { get; set; }
        public string character { get; set; }
    }

    public class MovieDetailGenre
    {
        public int id { get; set; }
        public int movie_id { get; set; }
        public int genre_id { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public string name { get; set; }
    }

    public class Video
    {
        public int id { get; set; }
        public int movie_id { get; set; }
        public string server { get; set; }
        public string header { get; set; }
        public string useragent { get; set; }
        public string link { get; set; }
        public string lang { get; set; }
        public string video_name { get; set; }
        public int hd { get; set; }
        public int embed { get; set; }
        public int youtubelink { get; set; }
        public int hls { get; set; }
        public int supported_hosts { get; set; }
        public int downloadonly { get; set; }
        public int drm { get; set; }
        public string drmuuid { get; set; }
        public string drmlicenceuri { get; set; }
        public int status { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }

    public class SpokenLanguage
    {
        public int id { get; set; }
        public string name { get; set; }
        public string iso_639_1 { get; set; }
        public int movie_id { get; set; }
        public string language_name { get; set; }
        public string language_code { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }

    public class Collection
    {
        public int id { get; set; }
        public string name { get; set; }
        public int movie_id { get; set; }
        public int collection_id { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }
}