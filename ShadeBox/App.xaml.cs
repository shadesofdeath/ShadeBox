using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ShadeBox
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ExtractMpvResourceFiles();
        }

        private void ExtractMpvResourceFiles()
        {
            try
            {
                string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string mpvDestinationFolder = Path.Combine(appDirectory, "mpv");

                // Check if mpv folder already exists and has files
                if (Directory.Exists(mpvDestinationFolder) && Directory.GetFiles(mpvDestinationFolder).Length > 0)
                {
                    return;
                }

                // Ensure destination folder exists
                Directory.CreateDirectory(mpvDestinationFolder);

                // Get the resource stream
                using (var resourceStream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("ShadeBox.mpv.mpv.zip"))
                {
                    if (resourceStream == null)
                    {
                        throw new FileNotFoundException("MPV resource not found");
                    }

                    // Save the resource to a temporary file
                    string tempZipPath = Path.Combine(Path.GetTempPath(), "mpv.zip");
                    using (var fileStream = File.Create(tempZipPath))
                    {
                        resourceStream.CopyTo(fileStream);
                    }

                    // Extract the zip file
                    System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, mpvDestinationFolder);

                    // Clean up temporary file
                    File.Delete(tempZipPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting MPV files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}