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

namespace ShadeBox.Pages
{
    public class MovieApiResponse
    {
        public string attention { get; set; }
        public string usingIn { get; set; }
        public string source_code { get; set; }
        public List<MovieApiData> data { get; set; }
    }

    public class MovieApiData
    {
        public int id { get; set; }
        public string title { get; set; }
        public string poster_path { get; set; }
        public double vote_average { get; set; }
        public string backdrop_path { get; set; }
        public string genresname { get; set; }
        public List<Genre> genres { get; set; }
    }

    public class SearchApiResponse
    {
        public List<SearchResult> search { get; set; }
    }

    public class SearchResult
    {
        public int id { get; set; }
        public string name { get; set; }
        public string poster_path { get; set; }
        public string type { get; set; }
    }

    public class MovieDetailsResponse
    {
        public int id { get; set; }
        public string title { get; set; }
        public string poster_path { get; set; }
        public double vote_average { get; set; }
        public string release_date { get; set; }
        public string genresname { get; set; }
        public List<Genre> genres { get; set; }
    }

    public class Genre
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class CategoryInfo
    {
        public string Name { get; set; }
        public string ApiId { get; set; }
    }

    public partial class FilmPage : iNKORE.UI.WPF.Modern.Controls.Page, INotifyPropertyChanged
    {
        private readonly HttpClient httpClient;
        private ObservableCollection<Movie> _movies;
        private int currentPage = 1;
        private int displayedPages = 2;
        private bool isLoading = false;
        private SemaphoreSlim loadingSemaphore = new SemaphoreSlim(1, 1);
        private Dictionary<string, BitmapImage> imageCache = new Dictionary<string, BitmapImage>();
        private string selectedCategory = null;
        private string searchText;
        private bool isSearchMode = false;
        private bool isNavigating = false;
        private static string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorite_movies.db");
        private bool isShowingFavorites = false;
        private CancellationTokenSource _searchCancellationTokenSource;
        private readonly Dictionary<string, CategoryInfo> categoryMappings;
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

        public ObservableCollection<Movie> Movies => _movies;

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

        public FilmPage()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            _movies = new ObservableCollection<Movie>();
            MovieItemsControl.ItemsSource = _movies;

            DataContext = this;

            categoryMappings = new Dictionary<string, CategoryInfo>
            {
                { "Aile", new CategoryInfo { Name = "Aile", ApiId = "10751" } },
                { "Aksiyon", new CategoryInfo { Name = "Aksiyon", ApiId = "28" } },
                { "Animasyon", new CategoryInfo { Name = "Animasyon", ApiId = "16" } },
                { "Belgesel", new CategoryInfo { Name = "Belgesel", ApiId = "99" } },
                { "Bilim Kurgu & Fantazi", new CategoryInfo { Name = "Bilim Kurgu & Fantazi", ApiId = "10765" } },
                { "Bilim-Kurgu", new CategoryInfo { Name = "Bilim-Kurgu", ApiId = "878" } },
                { "Dram", new CategoryInfo { Name = "Dram", ApiId = "18" } },
                { "Fantastik", new CategoryInfo { Name = "Fantastik", ApiId = "14" } },
                { "Gerilim", new CategoryInfo { Name = "Gerilim", ApiId = "53" } },
                { "Gizem", new CategoryInfo { Name = "Gizem", ApiId = "9648" } },
                { "Komedi", new CategoryInfo { Name = "Komedi", ApiId = "35" } },
                { "Korku", new CategoryInfo { Name = "Korku", ApiId = "27" } },
                { "Macera", new CategoryInfo { Name = "Macera", ApiId = "12" } },
                { "Müzik", new CategoryInfo { Name = "Müzik", ApiId = "10402" } },
                { "Romantik", new CategoryInfo { Name = "Romantik", ApiId = "10749" } },
                { "Savaş", new CategoryInfo { Name = "Savaş", ApiId = "10752" } },
                { "Suç", new CategoryInfo { Name = "Suç", ApiId = "80" } },
                { "TV film", new CategoryInfo { Name = "TV film", ApiId = "10770" } },
                { "Tarih", new CategoryInfo { Name = "Tarih", ApiId = "36" } }
            };

            _suggestions = new ObservableCollection<string>();

            LoadCategories();
            _ = LoadMovies();
        }

        private void LoadCategories()
        {
            CategoryComboBox.Items.Clear();
            foreach (var category in categoryMappings.Keys.OrderBy(k => k))
            {
                CategoryComboBox.Items.Add(new ComboBoxItem { Content = category });
            }
            CategoryComboBox.SelectedIndex = 0;
        }

        private string BuildApiUrl(int page)
        {
            string categoryId = "10751";
            if (!string.IsNullOrEmpty(selectedCategory) && categoryMappings.ContainsKey(selectedCategory))
            {
                categoryId = categoryMappings[selectedCategory].ApiId;
            }

            return $"https://ythls.kekikakademi.org/sinewix/movies/{categoryId}/{page}";
        }
        private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            isShowingFavorites = !isShowingFavorites;

            if (isShowingFavorites)
            {
                await LoadFavoriteMovies();
            }
            else
            {
                // Reset to normal view
                currentPage = 1;
                isSearchMode = false;
                await LoadMovies();
            }
        }
        private async Task LoadFavoriteMovies()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _movies.Clear();
                    NextButton.IsEnabled = false;
                    PreviousButton.IsEnabled = false;
                });

                var favoriteMovies = await Task.Run(() => GetFavoriteMoviesFromDb());

                foreach (var movieId in favoriteMovies)
                {
                    try
                    {
                        string url = $"https://ythls.kekikakademi.org/sinewix/movie/{movieId}";
                        var response = await httpClient.GetStringAsync(url);
                        var movieDetails = JsonSerializer.Deserialize<MovieDetailsResponse>(response);

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var movie = new Movie(this)
                            {
                                Id = movieId,
                                Title = movieDetails.title,
                                PosterPath = movieDetails.poster_path,
                                VoteAverage = movieDetails.vote_average,
                                ReleaseDate = DateTime.TryParse(movieDetails.release_date, out var date) ? date : DateTime.Now,
                                Category = movieDetails.genresname
                            };
                            _movies.Add(movie);
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading movie {movieId}: {ex.Message}");
                    }
                }

                if (_movies.Count == 0)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show("Favori film bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information));
                }
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show($"Favori filmler yüklenirken bir hata oluştu: {ex.Message}", "Hata",
                        MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private List<int> GetFavoriteMoviesFromDb()
        {
            var favoriteMovies = new List<int>();

            if (!File.Exists(DbPath))
            {
                return favoriteMovies;
            }

            using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
            {
                connection.Open();
                using (var command = new SQLiteCommand("SELECT movie_id FROM favorites", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            favoriteMovies.Add(reader.GetInt32(0));
                        }
                    }
                }
            }

            return favoriteMovies;
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
                    await LoadMovies();
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
                await SearchMovies(searchText, CancellationToken.None);
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
                        foreach (var result in searchResults.search.Where(r => r.type == "movie"))
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
        private void MovieCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is int movieId)
            {
                var detailsWindow = new MovieDetailsWindow(movieId);
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

        private async Task SearchMovies(string searchTerm, CancellationToken cancellationToken)
        {
            try
            {
                // Clear movies on the UI thread
                Application.Current.Dispatcher.Invoke(() => _movies.Clear());

                string encodedSearch = Uri.EscapeDataString(searchTerm);
                string searchUrl = $"https://ythls.kekikakademi.org/sinewix/search/{encodedSearch}";

                var searchResponse = await httpClient.GetStringAsync(searchUrl, cancellationToken);
                var searchResults = JsonSerializer.Deserialize<SearchApiResponse>(searchResponse);

                if (searchResults?.search == null || !searchResults.search.Any())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show("Film bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information));
                    return;
                }

                var movieTasks = searchResults.search
                    .Where(result => result.type == "movie")
                    .Select(async result =>
                    {
                        try
                        {
                            string detailsUrl = $"https://ythls.kekikakademi.org/sinewix/movie/{result.id}";
                            var detailsResponse = await httpClient.GetStringAsync(detailsUrl, cancellationToken);
                            return JsonSerializer.Deserialize<MovieDetailsResponse>(detailsResponse);
                        }
                        catch (Exception) when (!cancellationToken.IsCancellationRequested)
                        {
                            return null;
                        }
                    });

                var movies = await Task.WhenAll(movieTasks);

                // Update UI on the dispatcher thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var details in movies.Where(m => m != null))
                    {
                        var movie = new Movie(this)
                        {
                            Id = details.id,  // Bu satırı ekleyin!
                            Title = details.title,
                            PosterPath = details.poster_path,
                            VoteAverage = details.vote_average,
                            ReleaseDate = DateTime.TryParse(details.release_date, out var date) ? date : DateTime.Now,
                            Category = details.genresname
                        };
                        _movies.Add(movie);
                    }
                });
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Film arama sırasında bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        private async Task LoadMovies()
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

                Application.Current.Dispatcher.Invoke(() => _movies.Clear());

                // İlk sayfayı yükle
                var page1Result = await LoadSinglePage(currentPage);
                if (page1Result != null && page1Result.Any())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var movieData in page1Result)
                        {
                            AddMovieToCollection(movieData);
                        }
                    });

                    // İkinci sayfayı yükle
                    var page2Result = await LoadSinglePage(currentPage + 1);
                    if (page2Result != null && page2Result.Any())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var movieData in page2Result)
                            {
                                AddMovieToCollection(movieData);
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
                        MessageBox.Show("Daha fazla film bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Filmler yüklenirken bir hata oluştu: {ex.Message}");
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


        private async Task<List<MovieApiData>> LoadSinglePage(int pageNumber)
        {
            try
            {
                string url = BuildApiUrl(pageNumber);
                var response = await httpClient.GetStringAsync(url);
                var movieResponse = JsonSerializer.Deserialize<MovieApiResponse>(response);

                // Sadece data kontrolü yapalım
                if (movieResponse?.data != null && movieResponse.data.Any())
                {
                    return movieResponse.data;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
        private void AddMovieToCollection(MovieApiData movieData)
        {
            if (movieData == null) return;

            // Aynı ID'ye sahip film var mı diye kontrol et
            if (!_movies.Any(m => m.Id == movieData.id))
            {
                var movie = new Movie(this)
                {
                    Id = movieData.id,
                    Title = movieData.title,
                    PosterPath = movieData.poster_path,
                    VoteAverage = movieData.vote_average,
                    ReleaseDate = DateTime.Now,
                    Category = movieData.genresname
                };
                _movies.Add(movie);
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
            foreach (var movie in Movies)
            {
                movie.UnloadImage();
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
            CategoryComboBox.SelectedIndex = 0;
            SearchBox.Text = "";
            isSearchMode = false;
            ClearImageCache();
            _ = LoadMovies();
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                selectedCategory = selectedItem.Content.ToString();
                currentPage = 1;
                SearchBox.Text = "";
                isSearchMode = false;
                ClearImageCache();
                _ = LoadMovies();
            }
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading || isNavigating) return;

            if (currentPage > 2)
            {
                PreviousButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                currentPage -= 2;
                await LoadMovies();
            }
        }
        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading || isNavigating) return;

            PreviousButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            currentPage += 2;
            await LoadMovies();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Movie : IDisposable
    {
        private readonly FilmPage _filmPage;
        private BitmapImage _posterImage;

        public Movie(FilmPage filmPage)
        {
            _filmPage = filmPage;
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
                    _posterImage = _filmPage.GetCachedImage(PosterPath);
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