using System.Windows;
using System.Windows.Controls;

namespace ShadeBox.Pages
{
    public partial class SettingsPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        public SettingsPage()
        {
            InitializeComponent();

            // Sayfa açıldığında ayarları yükle
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Existing settings
            ToggleSavePosition.IsOn = Settings.Default.SavePositionOnQuit;
            ToggleHardwareAcceleration.IsOn = Settings.Default.HardwareAcceleration;
            ToggleSubtitles.IsOn = Settings.Default.SubtitlesEnabled;
            ToggleRememberVolume.IsOn = Settings.Default.RememberVolume;
            ToggleLowLatency.IsOn = Settings.Default.LowLatencyMode;
            ToggleAutoFullscreen.IsOn = Settings.Default.AutoFullscreen;
            ToggleAutoExit.IsOn = Settings.Default.AutoExit;
            ToggleSubtitleShadow.IsOn = Settings.Default.SubtitleShadow;
            ToggleSmoothMotion.IsOn = Settings.Default.SmoothMotion;
            ToggleBassBoost.IsOn = Settings.Default.BassBoost;
            TogglePowerSave.IsOn = Settings.Default.PowerSave;
            ToggleLoop.IsOn = Settings.Default.Loop;
            ToggleBestQuality.IsOn = Settings.Default.BestQuality;
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            // Existing settings
            if (sender == ToggleSavePosition)
                Settings.Default.SavePositionOnQuit = ToggleSavePosition.IsOn;
            else if (sender == ToggleHardwareAcceleration)
                Settings.Default.HardwareAcceleration = ToggleHardwareAcceleration.IsOn;
            else if (sender == ToggleSubtitles)
                Settings.Default.SubtitlesEnabled = ToggleSubtitles.IsOn;
            else if (sender == ToggleRememberVolume)
                Settings.Default.RememberVolume = ToggleRememberVolume.IsOn;
            else if (sender == ToggleLowLatency)
                Settings.Default.LowLatencyMode = ToggleLowLatency.IsOn;
            else if (sender == ToggleAutoFullscreen)
                Settings.Default.AutoFullscreen = ToggleAutoFullscreen.IsOn;
            else if (sender == ToggleAutoExit)
                Settings.Default.AutoExit = ToggleAutoExit.IsOn;

            // New settings
            else if (sender == ToggleSubtitleShadow)
                Settings.Default.SubtitleShadow = ToggleSubtitleShadow.IsOn;
            else if (sender == ToggleSmoothMotion)
                Settings.Default.SmoothMotion = ToggleSmoothMotion.IsOn;
            else if (sender == ToggleBassBoost)
                Settings.Default.BassBoost = ToggleBassBoost.IsOn;
            else if (sender == TogglePowerSave)
                Settings.Default.PowerSave = TogglePowerSave.IsOn;
            else if (sender == ToggleLoop)
                Settings.Default.Loop = ToggleLoop.IsOn;
            else if (sender == ToggleBestQuality)
                Settings.Default.BestQuality = ToggleBestQuality.IsOn;

            Settings.Default.Save();
        }
    }
}