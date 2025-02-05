using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using iNKORE.UI.WPF.Modern.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace ShadeBox.Pages
{
    public class SeriesApiResponse
    {
        public string attention { get; set; }
        public string using_in { get; set; }
        public string source_code { get; set; }
        public List<SeriesApiData> data { get; set; }
    }

    public class SeriesApiData
    {
        public int id { get; set; }
        public string name { get; set; }
        public string poster_path { get; set; }
        public double vote_average { get; set; }
        public string backdrop_path { get; set; }
        public string genre_name { get; set; }
        public string release_date { get; set; }
    }
    public class SeriesDetailsResponse
    {
        public int id { get; set; }
        public string name { get; set; }
        public string poster_path { get; set; }
        public double vote_average { get; set; }
        public string release_date { get; set; }
        public string genre_name { get; set; }
    }
    public partial class SeriesPage : iNKORE.UI.WPF.Modern.Controls.Page, INotifyPropertyChanged
    {
        private readonly HttpClient httpClient;
        private ObservableCollection<Series> _series;
        private int currentPage = 1;
        private bool isLoading = false;
        private SemaphoreSlim loadingSemaphore = new SemaphoreSlim(1, 1);
        private Dictionary<string, BitmapImage> imageCache = new Dictionary<string, BitmapImage>();
        private string searchText;
        private bool isSearchMode = false;
        private bool isNavigating = false;
        private static string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorite_series.db");
        private bool isShowingFavorites = false;
        private CancellationTokenSource _searchCancellationTokenSource;
        private ObservableCollection<string> _suggestions;

        public ObservableCollection<string> Suggestions
        {
            get => _suggestions;
            set
            {
                _suggestions = value;
                OnPropertyChanged(nameof(Suggestions));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Series> Series => _series;

        public string CurrentPageText => $"Sayfa {currentPage}-{currentPage + 1}";

        public string SearchText
        {
            get => searchText;
            set
            {
                if (searchText != value)
                {
                    searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                }
            }
        }

        public SeriesPage()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            _series = new ObservableCollection<Series>();
            SeriesItemsControl.ItemsSource = _series;

            DataContext = this;
            _suggestions = new ObservableCollection<string>();
            _ = LoadSeries();
        }
        private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            isShowingFavorites = !isShowingFavorites;

            if (isShowingFavorites)
            {
                await LoadFavoriteSeries();
            }
            else
            {
                // Reset to normal view
                currentPage = 1;
                isSearchMode = false;
                await LoadSeries();
            }
        }
        private async Task LoadFavoriteSeries()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _series.Clear();
                    NextButton.IsEnabled = false;
                    PreviousButton.IsEnabled = false;
                });

                var favoriteSeries = await Task.Run(() => GetFavoriteSeriesFromDb());

                foreach (var seriesId in favoriteSeries)
                {
                    try
                    {
                        string url = $"https://ythls.kekikakademi.org/sinewix/serie/{seriesId}";
                        var response = await httpClient.GetStringAsync(url);
                        var seriesDetails = JsonSerializer.Deserialize<SeriesDetailsResponse>(response);

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var series = new Series(this)
                            {
                                Id = seriesId,
                                Title = seriesDetails.name,
                                PosterPath = seriesDetails.poster_path,
                                VoteAverage = seriesDetails.vote_average,
                                ReleaseDate = DateTime.TryParse(seriesDetails.release_date, out var date) ? date : DateTime.Now,
                                Category = seriesDetails.genre_name
                            };
                            _series.Add(series);
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading series {seriesId}: {ex.Message}");
                    }
                }

                if (_series.Count == 0)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show("Favori dizi bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information));
                }
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show($"Favori diziler yüklenirken bir hata oluştu: {ex.Message}", "Hata",
                        MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        private List<int> GetFavoriteSeriesFromDb()
        {
            var favoriteSeries = new List<int>();

            if (!File.Exists(DbPath))
            {
                return favoriteSeries;
            }

            using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
            {
                connection.Open();
                using (var command = new SQLiteCommand("SELECT series_id FROM favorites", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            favoriteSeries.Add(reader.GetInt32(0));
                        }
                    }
                }
            }

            return favoriteSeries;
        }

        private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var searchText = sender.Text;
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    isSearchMode = false;
                    Suggestions.Clear();
                    await LoadSeries();
                    return;
                }

                // Cancel any previous search
                _searchCancellationTokenSource?.Cancel();
                _searchCancellationTokenSource = new CancellationTokenSource();

                try
                {
                    // Wait a brief moment before searching
                    await Task.Delay(500, _searchCancellationTokenSource.Token);

                    // Get suggestions
                    await GetSuggestions(searchText, _searchCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Search was cancelled, ignore
                }
            }
        }
        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var searchText = args.QueryText;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                isSearchMode = true;
                await SearchSeries(searchText, CancellationToken.None);
            }
        }
        private async Task GetSuggestions(string searchTerm, CancellationToken cancellationToken)
        {
            try
            {
                string encodedSearch = Uri.EscapeDataString(searchTerm);
                string searchUrl = $"https://ythls.kekikakademi.org/sinewix/search/{encodedSearch}";

                var searchResponse = await httpClient.GetStringAsync(searchUrl, cancellationToken);
                var searchResults = JsonSerializer.Deserialize<SearchApiResponse>(searchResponse);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Suggestions.Clear();
                    if (searchResults?.search != null)
                    {
                        foreach (var result in searchResults.search.Where(r => r.type == "serie"))
                        {
                            Suggestions.Add(result.name);
                        }
                    }
                    SearchBox.ItemsSource = Suggestions;
                });
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Suggestions.Clear();
                    SearchBox.ItemsSource = Suggestions;
                });
            }
        }
        private void SeriesCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is int seriesId)
            {
                var existingWindow = Application.Current.Windows.OfType<SeriesDetailsWindow>().FirstOrDefault();

                // Eğer mevcut pencere varsa kapat
                existingWindow?.Close();

                // Yeni pencereyi aç
                var detailsWindow = new SeriesDetailsWindow(seriesId);
                detailsWindow.Show();
            }
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is string selectedSuggestion)
            {
                sender.Text = selectedSuggestion;
            }
        }
        private async Task SearchSeries(string searchTerm, CancellationToken cancellationToken)
        {
            try
            {
                // Clear movies on the UI thread
                Application.Current.Dispatcher.Invoke(() => _series.Clear());

                string encodedSearch = Uri.EscapeDataString(searchTerm);
                string searchUrl = $"https://ythls.kekikakademi.org/sinewix/search/{encodedSearch}";

                var searchResponse = await httpClient.GetStringAsync(searchUrl, cancellationToken);
                var searchResults = JsonSerializer.Deserialize<SearchApiResponse>(searchResponse);

                if (searchResults?.search == null || !searchResults.search.Any())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show("Dizi bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information));
                    return;
                }
                var seriesTasks = searchResults.search
                    .Where(result => result.type == "serie")
                    .Select(async result =>
                    {
                        try
                        {
                            string detailsUrl = $"https://ythls.kekikakademi.org/sinewix/serie/{result.id}";
                            var detailsResponse = await httpClient.GetStringAsync(detailsUrl, cancellationToken);
                            return JsonSerializer.Deserialize<SeriesDetailsResponse>(detailsResponse);
                        }
                        catch (Exception) when (!cancellationToken.IsCancellationRequested)
                        {
                            return null;
                        }
                    });

                var series = await Task.WhenAll(seriesTasks);

                // Update UI on the dispatcher thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var details in series.Where(m => m != null))
                    {
                        var serie = new Series(this)
                        {
                            Id = details.id,
                            Title = details.name,
                            PosterPath = details.poster_path,
                            VoteAverage = details.vote_average,
                            ReleaseDate = DateTime.TryParse(details.release_date, out var date) ? date : DateTime.Now                        };
                        _series.Add(serie);
                    }
                });
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Application.Current.Dispatcher.Invoke(() =>
                  MessageBox.Show($"Dizi arama sırasında bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        private async Task LoadSeries()
        {
            if (isSearchMode || isNavigating) return;

            try
            {
                if (!await loadingSemaphore.WaitAsync(0))
                {
                    return;
                }

                isNavigating = true;
                isLoading = true;
                UpdateButtonStates();

                Application.Current.Dispatcher.Invoke(() => _series.Clear());

                // İlk sayfayı yükle
                var page1Result = await LoadSinglePage(currentPage);
                if (page1Result != null && page1Result.Any())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var seriesData in page1Result)
                        {
                            AddSeriesToCollection(seriesData);
                        }
                    });

                    // İkinci sayfayı yükle
                    var page2Result = await LoadSinglePage(currentPage + 1);
                    if (page2Result != null && page2Result.Any())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var seriesData in page2Result)
                            {
                                AddSeriesToCollection(seriesData);
                            }
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            NextButton.IsEnabled = false;
                        });
                        return;
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        NextButton.IsEnabled = true;
                        PreviousButton.IsEnabled = currentPage > 1;
                    });
                }
                else
                {
                    currentPage = Math.Max(1, currentPage - 2);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        NextButton.IsEnabled = false;
                        PreviousButton.IsEnabled = currentPage > 1;
                        MessageBox.Show("Daha fazla dizi bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Diziler yüklenirken bir hata oluştu: {ex.Message}");
                    NextButton.IsEnabled = false;
                    PreviousButton.IsEnabled = currentPage > 1;
                });

                currentPage = Math.Max(1, currentPage - 2);
            }
            finally
            {
                isLoading = false;
                isNavigating = false;
                OnPropertyChanged(nameof(CurrentPageText));
                UpdateButtonStates();
                loadingSemaphore.Release();
            }
        }
        private string BuildApiUrl(int page)
        {
            return $"https://ythls.kekikakademi.org/sinewix/series/{page}";
        }

        private async Task<List<SeriesApiData>> LoadSinglePage(int pageNumber)
        {
            try
            {
                string url = BuildApiUrl(pageNumber);
                var response = await httpClient.GetStringAsync(url);
                var seriesResponse = JsonSerializer.Deserialize<SeriesApiResponse>(response);

                // Sadece data kontrolü yapalım
                if (seriesResponse?.data != null && seriesResponse.data.Any())
                {
                    return seriesResponse.data;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        private void AddSeriesToCollection(SeriesApiData seriesData)
        {
            if (seriesData == null) return;

            // Aynı ID'ye sahip film var mı diye kontrol et
            if (!_series.Any(m => m.Id == seriesData.id))
            {
                var series = new Series(this)
                {
                    Id = seriesData.id,
                    Title = seriesData.name,
                    PosterPath = seriesData.poster_path,
                    VoteAverage = seriesData.vote_average,
                    ReleaseDate = DateTime.TryParse(seriesData.release_date, out var date) ? date : DateTime.Now,
                    Category = seriesData.genre_name
                };
                _series.Add(series);
            }
        }
        private void UpdateButtonStates()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PreviousButton.IsEnabled = currentPage > 1 && !isLoading && !isNavigating;
                NextButton.IsEnabled = !isLoading && !isNavigating;
            });
        }

        public BitmapImage GetCachedImage(string posterPath)
        {
            if (string.IsNullOrEmpty(posterPath)) return null;

            if (!imageCache.TryGetValue(posterPath, out BitmapImage posterImage))
            {
                try
                {
                    posterImage = new BitmapImage();
                    posterImage.BeginInit();
                    posterImage.UriSource = new Uri(posterPath);
                    posterImage.DecodePixelWidth = 160;
                    posterImage.DecodePixelHeight = 240;
                    posterImage.CacheOption = BitmapCacheOption.OnLoad;
                    posterImage.CreateOptions = BitmapCreateOptions.None;
                    posterImage.EndInit();
                    if (posterImage.CanFreeze)
                    {
                        posterImage.Freeze();
                    }
                    imageCache[posterPath] = posterImage;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return posterImage;
        }

        public void ClearImageCache()
        {
            foreach (var serie in Series)
            {
                serie.UnloadImage();
            }
            foreach (var key in imageCache.Keys.ToList())
            {
                imageCache[key] = null;
            }
            imageCache.Clear();
            GC.Collect();
        }
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            currentPage = 1; // İlk sayfaya dön
            SearchBox.Text = "";
            isSearchMode = false;
            ClearImageCache();
            _ = LoadSeries();
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading || isNavigating) return;

            if (currentPage > 2)
            {
                PreviousButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                currentPage -= 2;
                await LoadSeries();
            }
        }
        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading || isNavigating) return;

            PreviousButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            currentPage += 2;
            await LoadSeries();
        }
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class Series : IDisposable
    {
        private readonly SeriesPage _seriesPage;
        private BitmapImage _posterImage;

        public Series(SeriesPage seriesPage)
        {
            _seriesPage = seriesPage;
        }
        public int Id { get; set; }
        public string Title { get; set; }
        public string PosterPath { get; set; }
        public double VoteAverage { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Category { get; set; }
        public BitmapImage PosterImage
        {
            get
            {
                if (_posterImage == null && !string.IsNullOrEmpty(PosterPath))
                {
                    _posterImage = _seriesPage.GetCachedImage(PosterPath);
                }
                return _posterImage;
            }
        }
        public void UnloadImage()
        {
            _posterImage = null;
        }
        public void Dispose()
        {
            UnloadImage();
        }
    }
}