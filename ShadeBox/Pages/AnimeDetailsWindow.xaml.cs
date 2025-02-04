using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HtmlAgilityPack;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace ShadeBox.Pages
{
    public class AnimeMediafireExtractor
    {
        private static readonly HttpClient _httpClient;

        static AnimeMediafireExtractor()
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
    public class FavoriteAnimesDb
    {
        private static string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorite_animes.db");
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
                                animes_id INTEGER PRIMARY KEY,
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
        public static bool IsAnimesFavorited(int animesId)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1) FROM favorites WHERE animes_id = @animesId";
                        cmd.Parameters.AddWithValue("@animesId", animesId);

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
        public static void AddToFavorites(int animesId, string title)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO favorites (animes_id, title) 
                            VALUES (@animesId, @title)";
                        cmd.Parameters.AddWithValue("@animesId", animesId);
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
        public static void RemoveFromFavorites(int animesId)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM favorites WHERE animes_id = @animesId";
                        cmd.Parameters.AddWithValue("@animesId", animesId);

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
    public partial class AnimesDetailsWindow : Window
    {
        private readonly HttpClient _httpClient;
        private AnimesDetailApiResponse _animesDetails;
        private bool _isFavorited;
        public AnimesDetailsWindow(int animesId)
        {
            InitializeComponent();
            try
            {
                // Veritabanını başlat
                FavoriteAnimesDb.InitializeDatabase();
                _httpClient = new HttpClient();
                // Favori butonu olayını bağla
                var favoriteButton = this.FindName("FavoriteButton") as Button;
                if (favoriteButton != null)
                {
                    favoriteButton.Click += FavoriteButton_Click;
                }
                LoadAnimesDetails(animesId);
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
                if (_animesDetails == null) return;
                if (_isFavorited)
                {
                    FavoriteAnimesDb.RemoveFromFavorites(_animesDetails.id);
                    _isFavorited = false;
                }
                else
                {
                    FavoriteAnimesDb.AddToFavorites(_animesDetails.id, _animesDetails.name);
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
                videoLink = await AnimeMediafireExtractor.GetMediafireDownloadLink(videoLink);

                string mpvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv", "mpv.exe");
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
                videoLink = await AnimeMediafireExtractor.GetMediafireDownloadLink(videoLink);

                var fileName = $"{_animesDetails.name} - {videos[0].video_name}.mp4";
                var downloadWindow = new DownloadProgressWindow(videoLink, fileName);
                downloadWindow.Owner = this;
                downloadWindow.Show();
                await downloadWindow.StartDownload();
            }
        }
        private async void LoadAnimesDetails(int animesId)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"https://ythls.kekikakademi.org/sinewix/anime/{animesId}");
                _animesDetails = JsonSerializer.Deserialize<AnimesDetailApiResponse>(response);

                _isFavorited = FavoriteAnimesDb.IsAnimesFavorited(animesId);
                UpdateFavoriteButtonText();

                string defaultPosterPath = "https://i.imgur.com/47Crg0y.png";
                string firstEpisodePosterPath = null;

                // Büyük kapak posteri (ana poster) var mı?
                string mainPosterPath = _animesDetails.poster_path;
                if (string.IsNullOrWhiteSpace(mainPosterPath))
                {
                    mainPosterPath = defaultPosterPath;
                }

                // İlk bölümü bul ve poster yolunu sakla
                if (_animesDetails.seasons != null && _animesDetails.seasons.Count > 0)
                {
                    var firstSeason = _animesDetails.seasons[0];
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
                TitleBlock.Text = _animesDetails.name;
                OriginalTitleBlock.Text = _animesDetails.original_name;
                RatingBlock.Text = _animesDetails.vote_average.ToString("0.0");

                if (DateTime.TryParse(_animesDetails.first_air_date, out DateTime releaseDate))
                {
                    YearBlock.Text = releaseDate.Year.ToString();
                }

                OverviewBlock.Text = string.IsNullOrWhiteSpace(_animesDetails.overview) ? "Açıklama bulunamadı." : _animesDetails.overview;

                // Mevcut içeriği temizle ve verileri yükle
                GenresPanel.Children.Clear();

                if (_animesDetails.seasons != null)
                {
                    SeasonsListView.ItemsSource = _animesDetails.seasons;
                }

                // **Bölümler için sadece ilk bölüm posteri yedek olarak kullanılsın**
                foreach (var season in _animesDetails.seasons)
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
                foreach (var genre in _animesDetails.genres)
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

                if (_animesDetails.seasons.Count > 0)
                {
                    SeasonsListView.SelectedIndex = 0;
                    LoadEpisodes(_animesDetails.seasons[0]);
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
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is AnimesSeason selectedSeason)
            {
                LoadEpisodes(selectedSeason);
            }
        }
        private void LoadEpisodes(AnimesSeason season)
        {
            EpisodesItemsControl.ItemsSource = season.episodes;
        }
    }
    public class AnimesDetailApiResponse
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
        public List<AnimesNetwork> networkslist { get; set; }
        public string genresname { get; set; }
        public List<Genre> genres { get; set; }
        public List<AnimesSeason> seasons { get; set; }
        public List<SpokenLanguage> spoken_languages { get; set; }
        public List<object> belongs_to_collection { get; set; }
        public List<object> certifications { get; set; }

    }
    public class AnimesCastMember
    {
        public int id { get; set; }
        public string name { get; set; }
        public string original_name { get; set; }
        public string profile_path { get; set; }
        public string character { get; set; }
    }
    public class AnimesNetwork
    {
        public int id { get; set; }
        public string name { get; set; }
        public string logo_path { get; set; }
        public string origin_country { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
    }
    public class AnimesSeason
    {
        public int id { get; set; }
        public int? tmdb_id { get; set; }
        public int animes_id { get; set; }
        public int season_number { get; set; }
        public string name { get; set; }
        public string overview { get; set; }
        public string poster_path { get; set; }
        public string air_date { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public List<AnimesEpisode> episodes { get; set; }
    }
    public class AnimesEpisode
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
    public class AnimesVideo
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
    public class AnimesSpokenLanguage
    {
        public int id { get; set; }
        public string name { get; set; }
        public string iso_639_1 { get; set; }
        public int anime_id { get; set; }
        public string language_name { get; set; }
        public string language_code { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }

    }
    public class AnimesGenre
    {
        public int id { get; set; }
        public int anime_id { get; set; }
        public int genre_id { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public string name { get; set; }
    }
}