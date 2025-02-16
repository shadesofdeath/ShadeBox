using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace ShadeBox.Pages
{
    public partial class DownloadProgressWindow : Window
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _url;
        private readonly string _fileName;
        private DateTime _startTime;
        private long _totalBytes;
        private long _downloadedBytes;

        public DownloadProgressWindow(string url, string fileName)
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
            _url = url;
            _fileName = fileName;
            FileNameBlock.Text = $"İndiriliyor: {fileName}";
        }

        private string GetFileExtensionFromUrl(string url)
        {
            try
            {
                // URL'den dosya adını al
                string fileName = Path.GetFileName(new Uri(url).LocalPath);

                // Dosya adından uzantıyı al
                string extension = Path.GetExtension(fileName);

                // Uzantı boşsa veya geçersizse
                if (string.IsNullOrEmpty(extension))
                {
                    // URL'de query string varsa temizle ve tekrar dene
                    int queryIndex = url.IndexOf('?');
                    if (queryIndex != -1)
                    {
                        string cleanUrl = url.Substring(0, queryIndex);
                        extension = Path.GetExtension(cleanUrl);
                    }
                }

                // Hala uzantı bulunamadıysa veya geçersizse
                if (string.IsNullOrEmpty(extension))
                {
                    // Yaygın video formatlarını URL içinde ara
                    string[] commonExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".webm" };
                    foreach (var ext in commonExtensions)
                    {
                        if (url.Contains(ext, StringComparison.OrdinalIgnoreCase))
                        {
                            extension = ext;
                            break;
                        }
                    }
                }

                // Hiçbir şekilde uzantı bulunamadıysa varsayılan olarak .mp4 kullan
                return string.IsNullOrEmpty(extension) ? ".mp4" : extension;
            }
            catch
            {
                return ".mp4"; // Hata durumunda varsayılan olarak .mp4 döndür
            }
        }

        private string BuildFileFilter(string extension)
        {
            var cleanExtension = extension.TrimStart('.').ToUpper();
            var filter = $"{cleanExtension} files (*{extension})|*{extension}|All files (*.*)|*.*";
            return filter;
        }

        public async Task StartDownload()
        {
            try
            {
                string fileExtension = GetFileExtensionFromUrl(_url);
                string filter = BuildFileFilter(fileExtension);
                string defaultFileName = Path.ChangeExtension(_fileName, fileExtension);

                var saveFileDialog = new SaveFileDialog
                {
                    FileName = defaultFileName,
                    Filter = filter,
                    DefaultExt = fileExtension
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    Close();
                    return;
                }

                _startTime = DateTime.Now;
                using var client = new HttpClient();
                using var response = await client.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead);
                _totalBytes = response.Content.Headers.ContentLength ?? -1;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(saveFileDialog.FileName);
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, _cancellationTokenSource.Token);
                    _downloadedBytes += bytesRead;
                    UpdateProgress();
                }

                StatusText.Text = "İndirme tamamlandı!";
                CancelButton.Content = "Kapat";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "İndirme iptal edildi.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Hata: {ex.Message}";
            }
        }

        private void UpdateProgress()
        {
            if (_totalBytes == -1) return;

            var progress = (double)_downloadedBytes / _totalBytes;
            DownloadProgress.Value = progress * 100;

            var elapsedTime = (DateTime.Now - _startTime).TotalSeconds;
            var speed = _downloadedBytes / elapsedTime;
            var remainingBytes = _totalBytes - _downloadedBytes;
            var remainingTime = remainingBytes / speed;

            Dispatcher.Invoke(() =>
            {
                ProgressText.Text = $"{progress:P0}";
                SpeedText.Text = $"{FormatSpeed(speed)}";
                RemainingTimeText.Text = FormatTime(remainingTime);
                BytesText.Text = $"{FormatBytes(_downloadedBytes)} / {FormatBytes(_totalBytes)}";
            });
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s" };
            int order = 0;
            while (bytesPerSecond >= 1024 && order < sizes.Length - 1)
            {
                bytesPerSecond /= 1024;
                order++;
            }
            return $"{bytesPerSecond:0.#} {sizes[order]}";
        }

        private string FormatTime(double seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            return timeSpan.Hours > 0
                ? $"{timeSpan.Hours}s {timeSpan.Minutes}d {timeSpan.Seconds}sn"
                : $"{timeSpan.Minutes}d {timeSpan.Seconds}sn";
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (CancelButton.Content.ToString() == "Kapat")
            {
                Close();
            }
            else
            {
                _cancellationTokenSource.Cancel();
                CancelButton.Content = "Kapat";
            }
        }
    }
}