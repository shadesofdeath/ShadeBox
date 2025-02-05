using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using System.Text.RegularExpressions;
using System.IO;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace ShadeBox.Pages
{
    public partial class CanliTv : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private List<Channel> allChannels = new List<Channel>();
        private string mpvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv", "mpv.exe");

        public class Channel
        {
            public string Name { get; set; }
            public string Logo { get; set; }
            public string Url { get; set; }
            public string Group { get; set; }
            public string Language { get; set; }
        }

        public CanliTv()
        {
            InitializeComponent();
            LoadChannels();
        }

        private async void LoadChannels()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var m3uContent = await client.GetStringAsync("https://raw.githubusercontent.com/keyiflerolsun/IPTV_YenirMi/main/Kanallar/KekikAkademi.m3u");
                    ParseM3U(m3uContent);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kanal listesi yüklenirken hata oluştu: {ex.Message}");
            }
        }

        private void ParseM3U(string content)
        {
            var lines = content.Split('\n');
            Channel currentChannel = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("#EXTINF"))
                {
                    currentChannel = new Channel();

                    // Parse channel info
                    var nameMatch = Regex.Match(line, "tvg-name=\"([^\"]+)\"");
                    var logoMatch = Regex.Match(line, "tvg-logo=\"([^\"]+)\"");
                    var groupMatch = Regex.Match(line, "group-title=\"([^\"]+)\"");
                    var langMatch = Regex.Match(line, "tvg-language=\"([^\"]+)\"");

                    if (nameMatch.Success) currentChannel.Name = nameMatch.Groups[1].Value;
                    if (logoMatch.Success) currentChannel.Logo = logoMatch.Groups[1].Value;
                    if (groupMatch.Success) currentChannel.Group = groupMatch.Groups[1].Value;
                    if (langMatch.Success) currentChannel.Language = langMatch.Groups[1].Value;
                }
                else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#") && currentChannel != null)
                {
                    currentChannel.Url = line.Trim();
                    allChannels.Add(currentChannel);
                    currentChannel = null;
                }
            }

            var categories = allChannels.Select(c => c.Group).Distinct().OrderBy(g => g).ToList();
            categories.Insert(0, "Tümü");
            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedIndex = 0;

            UpdateChannelDisplay();
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var suggestions = allChannels
                    .Where(c => c.Name.ToLower().Contains(sender.Text.ToLower()))
                    .Select(c => c.Name)
                    .Take(5)
                    .ToList();

                sender.ItemsSource = suggestions;
                UpdateChannelDisplay();
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            UpdateChannelDisplay();
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.Text = args.SelectedItem.ToString();
            UpdateChannelDisplay();
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateChannelDisplay();
        }

        private void UpdateChannelDisplay()
        {
            var selectedCategory = CategoryComboBox.SelectedItem as string;
            var searchText = SearchBox.Text.ToLower();

            var filteredChannels = allChannels.Where(c =>
                (selectedCategory == "Tümü" || c.Group == selectedCategory) &&
                (string.IsNullOrEmpty(searchText) || c.Name.ToLower().Contains(searchText))
            ).ToList();

            ChannelList.ItemsSource = filteredChannels;
        }

        private void Channel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button card && card.DataContext is Channel channel)
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = mpvPath,
                        Arguments = $"--force-window=yes \"{channel.Url}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kanal açılırken hata oluştu: {ex.Message}");
                }
            }
        }
    }
}