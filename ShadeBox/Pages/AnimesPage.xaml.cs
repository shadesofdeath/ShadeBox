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
    public class AnimesApiResponse
    {
        public string attention { get; set; }
        public string using_in { get; set; }
        public string source_code { get; set; }
        public List<AnimesApiData> data { get; set; }
    }

    public class AnimesApiData
    {
        public int id { get; set; }
        public string name { get; set; }
        public string poster_path { get; set; }
        public double vote_average { get; set; }
        public string backdrop_path { get; set; }
        public string genre_name { get; set; }
        public string release_date { get; set; }
    }
    public class AnimesDetailsResponse
    {
        public int id { get; set; }
        public string name { get; set; }
        public string poster_path { get; set; }
        public double vote_average { get; set; }
        public string release_date { get; set; }
        public string genre_name { get; set; }
    }
    public partial class AnimesPage : iNKORE.UI.WPF.Modern.Controls.Page, INotifyPropertyChanged
    {
        private readonly HttpClient httpClient;
        private ObservableCollection<Animes> _animes;
        private int currentPage = 1;
        private bool isLoading = false;
        private SemaphoreSlim loadingSemaphore = new SemaphoreSlim(1, 1);
        private Dictionary<string, BitmapImage> imageCache = new Dictionary<string, BitmapImage>();
        private string searchText;
        private bool isSearchMode = false;
        private bool isNavigating = false;
        private static string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorite_animes.db");
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

        public ObservableCollection<Animes> Animes => _animes;

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

        public AnimesPage()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            _animes = new ObservableCollection<Animes>();
            AnimesItemsControl.ItemsSource = _animes;

            DataContext = this;
            _suggestions = new ObservableCollection<string>();
            _ = Loadanimes();
        }
        private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            isShowingFavorites = !isShowingFavorites;

            if (isShowingFavorites)
            {
                await LoadFavoriteanimes();
            }
            else
            {
                // Reset to normal view
                currentPage = 1;
                isSearchMode = false;
                await Loadanimes();
            }
        }
        private async Task LoadFavoriteanimes()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _animes.Clear();
                    NextButton.IsEnabled = false;
                    PreviousButton.IsEnabled = false;
                });

                var favoriteanimes = await Task.Run(() => GetFavoriteanimesFromDb());

                foreach (var animesId in favoriteanimes)
                {
                    try
                    {
                        string url = $"https://ythls.kekikakademi.org/sinewix/anime/{animesId}";
                        var response = await httpClient.GetStringAsync(url);
                        var animesDetails = JsonSerializer.Deserialize<AnimesDetailsResponse>(response);

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var animes = new Animes(this)
                            {
                                Id = animesId,
                                Title = animesDetails.name,
                                PosterPath = animesDetails.poster_path,
                                VoteAverage = animesDetails.vote_average,
                                ReleaseDate = DateTime.TryParse(animesDetails.release_date, out var date) ? date : DateTime.Now,
                                Category = animesDetails.genre_name
                            };
                            _animes.Add(animes);
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading animes {animesId}: {ex.Message}");
                    }
                }

                if (_animes.Count == 0)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show("Favori anime bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information));
                }
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show($"Favori animeler yüklenirken bir hata oluştu: {ex.Message}", "Hata",
                        MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        private List<int> GetFavoriteanimesFromDb()
        {
            var favoriteanimes = new List<int>();

            if (!File.Exists(DbPath))
            {
                return favoriteanimes;
            }

            using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
            {
                connection.Open();
                using (var command = new SQLiteCommand("SELECT animes_id FROM favorites", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            favoriteanimes.Add(reader.GetInt32(0));
                        }
                    }
                }
            }

            return favoriteanimes;
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
                    await Loadanimes();
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
                await Searchanimes(searchText, CancellationToken.None);
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
                        foreach (var result in searchResults.search.Where(r => r.type == "anime"))
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
        private void animesCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is int animesId)
            {
                var detailsWindow = new AnimesDetailsWindow(animesId);
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
        private async Task Searchanimes(string searchTerm, CancellationToken cancellationToken)
        {
            try
            {
                // Clear movies on the UI thread
                Application.Current.Dispatcher.Invoke(() => _animes.Clear());

                string encodedSearch = Uri.EscapeDataString(searchTerm);
                string searchUrl = $"https://ythls.kekikakademi.org/sinewix/search/{encodedSearch}";

                var searchResponse = await httpClient.GetStringAsync(searchUrl, cancellationToken);
                var searchResults = JsonSerializer.Deserialize<SearchApiResponse>(searchResponse);

                if (searchResults?.search == null || !searchResults.search.Any())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show("Anime bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information));
                    return;
                }
                var animesTasks = searchResults.search
                    .Where(result => result.type == "anime")
                    .Select(async result =>
                    {
                        try
                        {
                            string detailsUrl = $"https://ythls.kekikakademi.org/sinewix/anime/{result.id}";
                            var detailsResponse = await httpClient.GetStringAsync(detailsUrl, cancellationToken);
                            return JsonSerializer.Deserialize<AnimesDetailsResponse>(detailsResponse);
                        }
                        catch (Exception) when (!cancellationToken.IsCancellationRequested)
                        {
                            return null;
                        }
                    });

                var animes = await Task.WhenAll(animesTasks);

                // Update UI on the dispatcher thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var details in animes.Where(m => m != null))
                    {
                        var anime = new Animes(this)
                        {
                            Id = details.id,
                            Title = details.name,
                            PosterPath = details.poster_path,
                            VoteAverage = details.vote_average,
                            ReleaseDate = DateTime.TryParse(details.release_date, out var date) ? date : DateTime.Now                        };
                        _animes.Add(anime);
                    }
                });
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Application.Current.Dispatcher.Invoke(() =>
                  MessageBox.Show($"Anime arama sırasında bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        private async Task Loadanimes()
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

                Application.Current.Dispatcher.Invoke(() => _animes.Clear());

                // İlk sayfayı yükle
                var page1Result = await LoadSinglePage(currentPage);
                if (page1Result != null && page1Result.Any())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var animesData in page1Result)
                        {
                            AddanimesToCollection(animesData);
                        }
                    });

                    // İkinci sayfayı yükle
                    var page2Result = await LoadSinglePage(currentPage + 1);
                    if (page2Result != null && page2Result.Any())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var animesData in page2Result)
                            {
                                AddanimesToCollection(animesData);
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
                        MessageBox.Show("Daha fazla anime bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Animeler yüklenirken bir hata oluştu: {ex.Message}");
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
            return $"https://ythls.kekikakademi.org/sinewix/animes/{page}";
        }

        private async Task<List<AnimesApiData>> LoadSinglePage(int pageNumber)
        {
            try
            {
                string url = BuildApiUrl(pageNumber);
                var response = await httpClient.GetStringAsync(url);
                var animesResponse = JsonSerializer.Deserialize<AnimesApiResponse>(response);

                // Sadece data kontrolü yapalım
                if (animesResponse?.data != null && animesResponse.data.Any())
                {
                    return animesResponse.data;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        private void AddanimesToCollection(AnimesApiData animesData)
        {
            if (animesData == null) return;

            // Aynı ID'ye sahip film var mı diye kontrol et
            if (!_animes.Any(m => m.Id == animesData.id))
            {
                var animes = new Animes(this)
                {
                    Id = animesData.id,
                    Title = animesData.name,
                    PosterPath = animesData.poster_path,
                    VoteAverage = animesData.vote_average,
                    ReleaseDate = DateTime.TryParse(animesData.release_date, out var date) ? date : DateTime.Now,
                    Category = animesData.genre_name
                };
                _animes.Add(animes);
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
            foreach (var anime in Animes)
            {
                anime.UnloadImage();
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
            _ = Loadanimes();
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading || isNavigating) return;

            if (currentPage > 2)
            {
                PreviousButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                currentPage -= 2;
                await Loadanimes();
            }
        }
        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading || isNavigating) return;

            PreviousButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            currentPage += 2;
            await Loadanimes();
        }
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class Animes : IDisposable
    {
        private readonly AnimesPage _animesPage;
        private BitmapImage _posterImage;

        public Animes(AnimesPage animesPage)
        {
            _animesPage = animesPage;
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
                    _posterImage = _animesPage.GetCachedImage(PosterPath);
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