﻿using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HtmlAgilityPack;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace ShadeBox.Pages
{
    public class MediafireExtractor
    {
        private static readonly HttpClient _httpClient;

        static MediafireExtractor()
        {
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = true
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }
        public static async Task<string> GetMediafireDownloadLink(string url)
        {
            if (!IsMediafireLink(url))
            {
                return url;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Referer", url);
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var htmlContent = await response.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                var downloadButton = doc.DocumentNode.SelectSingleNode("//a[@id='downloadButton']");
                if (downloadButton != null)
                {
                    var downloadLink = downloadButton.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(downloadLink))
                    {
                        return downloadLink;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Link çözümlenirken hata oluştu: {ex.Message}");
            }

            return url; // Hata olursa veya indirme linki bulunamazsa orijinal URL'yi döndür
        }
        private static bool IsMediafireLink(string url)
        {
            return url.Contains("mediafire.com");
        }
    }
    public class FavoriteSeriesDb
    {
        private static string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorite_series.db");
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
                                series_id INTEGER PRIMARY KEY,
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
        public static bool IsSeriesFavorited(int seriesId)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1) FROM favorites WHERE series_id = @seriesId";
                        cmd.Parameters.AddWithValue("@seriesId", seriesId);

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
        public static void AddToFavorites(int seriesId, string title)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO favorites (series_id, title) 
                            VALUES (@seriesId, @title)";
                        cmd.Parameters.AddWithValue("@seriesId", seriesId);
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
        public static void RemoveFromFavorites(int seriesId)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM favorites WHERE series_id = @seriesId";
                        cmd.Parameters.AddWithValue("@seriesId", seriesId);

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
    public partial class SeriesDetailsWindow : Window
    {
        private readonly HttpClient _httpClient;
        private SeriesDetailApiResponse _seriesDetails;
        private bool _isFavorited;
        public SeriesDetailsWindow(int seriesId)
        {
            InitializeComponent();
            try
            {
                // Veritabanını başlat
                FavoriteSeriesDb.InitializeDatabase();
                _httpClient = new HttpClient();
                // Favori butonu olayını bağla
                var favoriteButton = this.FindName("FavoriteButton") as Button;
                if (favoriteButton != null)
                {
                    favoriteButton.Click += FavoriteButton_Click;
                }
                LoadSeriesDetails(seriesId);
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
                if (_seriesDetails == null) return;
                if (_isFavorited)
                {
                    FavoriteSeriesDb.RemoveFromFavorites(_seriesDetails.id);
                    _isFavorited = false;
                }
                else
                {
                    FavoriteSeriesDb.AddToFavorites(_seriesDetails.id, _seriesDetails.name);
                    _isFavorited = true;
                }
                UpdateFavoriteButtonText();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Favori işlemi sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void EpisodeWatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is List<Video> videos)
            {
                if (videos == null || !videos.Any())
                {
                    MessageBox.Show("Bu bölüm için izleme linki bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var videoLink = videos[0].link;
                if (string.IsNullOrEmpty(videoLink))
                {
                    MessageBox.Show("Geçerli bir video linki bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                //Mediafire kontrolü ve link çekme
                videoLink = await MediafireExtractor.GetMediafireDownloadLink(videoLink);

                string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string mpvPath = Path.Combine(exePath, "mpv", "mpv.exe");
                if (!File.Exists(mpvPath))
                {
                    MessageBox.Show("MPV player bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = mpvPath,
                    Arguments = $"\"{videoLink}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(processStartInfo);
            }
        }

        private async void EpisodeDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is List<Video> videos)
            {
                if (videos == null || !videos.Any())
                {
                    MessageBox.Show("Bu bölüm için indirme linki bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var videoLink = videos[0].link;
                if (string.IsNullOrEmpty(videoLink))
                {
                    MessageBox.Show("Geçerli bir video linki bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Mediafire kontrolü ve link çekme
                videoLink = await MediafireExtractor.GetMediafireDownloadLink(videoLink);

                var fileName = $"{_seriesDetails.name} - {videos[0].video_name}.mp4";
                var downloadWindow = new DownloadProgressWindow(videoLink, fileName);
                downloadWindow.Owner = this;
                downloadWindow.Show();
                await downloadWindow.StartDownload();
            }
        }
        private async void LoadSeriesDetails(int seriesId)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"https://ythls.kekikakademi.org/sinewix/serie/{seriesId}");
                _seriesDetails = JsonSerializer.Deserialize<SeriesDetailApiResponse>(response);

                _isFavorited = FavoriteSeriesDb.IsSeriesFavorited(seriesId);
                UpdateFavoriteButtonText();

                string defaultPosterPath = "https://i.imgur.com/tuzQOFm.png";
                string firstEpisodePosterPath = null;

                // Büyük kapak posteri (ana poster) var mı?
                string mainPosterPath = _seriesDetails.poster_path;
                if (string.IsNullOrWhiteSpace(mainPosterPath))
                {
                    mainPosterPath = defaultPosterPath;
                }

                // İlk bölümü bul ve poster yolunu sakla
                if (_seriesDetails.seasons != null && _seriesDetails.seasons.Count > 0)
                {
                    var firstSeason = _seriesDetails.seasons[0];
                    if (firstSeason.episodes != null && firstSeason.episodes.Count > 0)
                    {
                        firstEpisodePosterPath = firstSeason.episodes[0].still_path;
                    }
                }

                // Eğer ilk bölüm posteri null ise, default posteri kullan
                if (string.IsNullOrWhiteSpace(firstEpisodePosterPath))
                {
                    firstEpisodePosterPath = defaultPosterPath;
                }

                try
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.None; // Önbelleğe almayı devre dışı bırak
                    bitmapImage.UriSource = new Uri(mainPosterPath);
                    bitmapImage.EndInit();
                    PosterImage.Source = bitmapImage;
                }
                catch
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.None; // Önbelleğe almayı devre dışı bırak
                    bitmapImage.UriSource = new Uri(defaultPosterPath);
                    bitmapImage.EndInit();
                    PosterImage.Source = bitmapImage;
                }

                // Diğer metin alanlarını ayarla
                TitleBlock.Text = _seriesDetails.name;
                OriginalTitleBlock.Text = _seriesDetails.original_name;
                RatingBlock.Text = _seriesDetails.vote_average.ToString("0.0");

                if (DateTime.TryParse(_seriesDetails.first_air_date, out DateTime releaseDate))
                {
                    YearBlock.Text = releaseDate.Year.ToString();
                }

                OverviewBlock.Text = string.IsNullOrWhiteSpace(_seriesDetails.overview) ? "Açıklama bulunamadı." : _seriesDetails.overview;

                // Mevcut içeriği temizle ve verileri yükle
                GenresPanel.Children.Clear();

                if (_seriesDetails.seasons != null)
                {
                    SeasonsListView.ItemsSource = _seriesDetails.seasons;
                }

                // **Bölümler için sadece ilk bölüm posteri yedek olarak kullanılsın**
                foreach (var season in _seriesDetails.seasons)
                {
                    if (season.episodes != null)
                    {
                        foreach (var episode in season.episodes)
                        {
                            if (string.IsNullOrWhiteSpace(episode.overview))
                            {
                                episode.overview = "Açıklama bulunamadı.";
                            }

                            // Eğer bölümün posteri yoksa, ilk bölüm posteri kullanılsın
                            if (string.IsNullOrWhiteSpace(episode.still_path))
                            {
                                episode.still_path = firstEpisodePosterPath;
                            }
                        }
                    }
                }

                // Türleri ekle
                foreach (var genre in _seriesDetails.genres)
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

                if (_seriesDetails.seasons.Count > 0)
                {
                    SeasonsListView.SelectedIndex = 0;
                    LoadEpisodes(_seriesDetails.seasons[0]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dizi detayları yüklenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }




        private void SeasonsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is SeriesSeason selectedSeason)
            {
                LoadEpisodes(selectedSeason);
            }
        }
        private void LoadEpisodes(SeriesSeason season)
        {
            EpisodesItemsControl.ItemsSource = season.episodes;
        }
    }
    public class SeriesDetailApiResponse
    {
        public string attention { get; set; }
        public string using_in { get; set; }
        public string source_code { get; set; }
        public int id { get; set; }
        public int tmdb_id { get; set; }
        public string name { get; set; }
        public string original_name { get; set; }
        public string imdb_external_id { get; set; }
        public string subtitle { get; set; }
        public string overview { get; set; }
        public string poster_path { get; set; }
        public string backdrop_path { get; set; }
        public string backdrop_path_tv { get; set; }
        public string trailer_url { get; set; }
        public string preview_path { get; set; }
        public int views { get; set; }
        public double vote_average { get; set; }
        public int vote_count { get; set; }
        public double popularity { get; set; }
        public int featured { get; set; }
        public int pinned { get; set; }
        public int newEpisodes { get; set; }
        public int premuim { get; set; }
        public int active { get; set; }
        public string first_air_date { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public List<CastMember> casterslist { get; set; }
        public List<SeriesNetwork> networkslist { get; set; }
        public string genresname { get; set; }
        public List<Genre> genres { get; set; }
        public List<SeriesSeason> seasons { get; set; }
        public List<SpokenLanguage> spoken_languages { get; set; }
        public List<object> belongs_to_collection { get; set; }
        public List<object> certifications { get; set; }

    }
    public class SeriesCastMember
    {
        public int id { get; set; }
        public string name { get; set; }
        public string original_name { get; set; }
        public string profile_path { get; set; }
        public string character { get; set; }
    }
    public class SeriesNetwork
    {
        public int id { get; set; }
        public string name { get; set; }
        public string logo_path { get; set; }
        public string origin_country { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }
    public class SeriesSeason
    {
        public int id { get; set; }
        public int? tmdb_id { get; set; }
        public int serie_id { get; set; }
        public int season_number { get; set; }
        public string name { get; set; }
        public string overview { get; set; }
        public string poster_path { get; set; }
        public string air_date { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public List<SeriesEpisode> episodes { get; set; }
    }
    public class SeriesEpisode
    {
        public int id { get; set; }
        public int? tmdb_id { get; set; }
        public int season_id { get; set; }
        public int episode_number { get; set; }
        public string name { get; set; }
        public string overview { get; set; }
        public string still_path { get; set; }
        public string still_path_tv { get; set; }
        public double vote_average { get; set; }
        public int? vote_count { get; set; }
        public int views { get; set; }
        public string air_date { get; set; }
        public int skiprecap_start_in { get; set; }
        public int hasrecap { get; set; }
        public int enable_stream { get; set; }
        public int enable_media_download { get; set; }
        public int enable_ads_unlock { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public List<Video> videos { get; set; }
        public List<object> substitles { get; set; }
        public List<object> downloads { get; set; }

    }
    public class SeriesVideo
    {
        public int id { get; set; }
        public int episode_id { get; set; }
        public string server { get; set; }
        public string header { get; set; }
        public string useragent { get; set; }
        public string link { get; set; }
        public string lang { get; set; }
        public string video_name { get; set; }
        public int embed { get; set; }
        public int youtubelink { get; set; }
        public int hls { get; set; }
        public int supported_hosts { get; set; }
        public int drm { get; set; }
        public string drmuuid { get; set; }
        public string drmlicenceuri { get; set; }
        public int status { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }

    }
    public class SeriesSpokenLanguage
    {
        public int id { get; set; }
        public string name { get; set; }
        public string iso_639_1 { get; set; }
        public int serie_id { get; set; }
        public string language_name { get; set; }
        public string language_code { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }

    }
    public class SeriesGenre
    {
        public int id { get; set; }
        public int serie_id { get; set; }
        public int genre_id { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public string name { get; set; }
    }
}