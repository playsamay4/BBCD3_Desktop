using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace BBCD3_Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly string logPath;
    string savePath = Environment.GetEnvironmentVariable("USERPROFILE") + @"\" + "Downloads";

    private Source _selectedSource = null;

    public class GridNavigationItem
    {
        public string DisplayName { get; set; }
        public string TargetId { get; set; }       //nulld if category folder
        public string TargetCategory { get; set; } // nulled if leaf channel item
        public bool IsCategoryFolder => TargetId == null;
    }

    public MainWindow()
    {
        InitializeComponent();

        _ = CheckUpdatesSilent();

        // logs directory in application folder
        string appFolder = AppDomain.CurrentDomain.BaseDirectory;
        string logsFolder = Path.Combine(appFolder, "logs");
        Directory.CreateDirectory(logsFolder);
        logPath = Path.Combine(logsFolder, "error_log.txt");



        //var channelItems = SOURCES.All.Values.ToList();
        //ListCollectionView lcv = new ListCollectionView(channelItems);
        //lcv.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        //ChannelsList.ItemsSource = lcv;
        //ChannelsList.DisplayMemberPath = "Name";
        //ChannelsList.SelectedValuePath = "Id";

        LoadTopLevelCategories();

        Downloader.StatusUpdate += Downloader_StatusUpdate;
        Downloader.DownloadError += Downloader_DownloadError;
        Downloader.ProgressUpdated += (sender, progress) =>
        {
            Dispatcher.Invoke(() => StatusProgress.Value = progress);
        };

        TimeZoneInfo londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        DateTime londonTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, londonTimeZone);
        StartTimePicker.Value = londonTime;
        DatePicker.SelectedDate = londonTime;
    }

    private void LoadTopLevelCategories()
    {
        // Extracts unique category strings out of your hardcoded Downloader.SOURCES dictionary
        var topCategories = SOURCES.All.Values
            .Select(s => s.Category)
            .Distinct()
            .Select(cat => new GridNavigationItem
            {
                DisplayName = cat,
                TargetCategory = cat
            })
            .ToList();

        ChannelsGridList.ItemsSource = topCategories;
        CategoryHeader.Text = "Pick a channel category";
        BackButton.Visibility = Visibility.Collapsed;
        _selectedSource = null;
    }

    private void ChannelsGridList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelsGridList.SelectedItem is GridNavigationItem navItem)
        {
            if (navItem.IsCategoryFolder)
            {
                // User clicked a Category card -> Drill Down to see channels inside it
                var subChannels = SOURCES.All.Values
                    .Where(s => s.Category == navItem.TargetCategory)
                    .Select(s => new GridNavigationItem
                    {
                        // Clean up strings like "BBC One London [UK Only]" down to just "London" or keeping it clean
                        DisplayName = s.Name.Replace($" [{navItem.TargetCategory}]", "").Replace(" [UK Only]", "").Replace(" [US-Only Geoblock]", "").Replace(" [Australia-Only Geoblock]", ""),
                        TargetId = s.Id
                    })
                    .ToList();

                ChannelsGridList.ItemsSource = subChannels;
                CategoryHeader.Text = $"{navItem.TargetCategory} Sub-Options";
                BackButton.Visibility = Visibility.Visible;
            }
            else
            {
                // User clicked an actual channel item
                _selectedSource = SOURCES.GetSource(navItem.TargetId);
                if (_selectedSource != null)
                {
                    StatusText.Text = $"Selected: {_selectedSource.Name}";
                }
            }
        }
    }


    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        // Go back up to main categories
        LoadTopLevelCategories();
    }


    private void Downloader_StatusUpdate(object sender, string e)
    {
        //invoke
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = $"Status: {e}";
        });
    }

    private void LogError(string errorMessage)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] {errorMessage}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write to error log: {ex.Message}");
        }
    }

    private void Downloader_DownloadError(object sender, string e)
    {
        //invoke
        Dispatcher.Invoke(() =>
        {
            string errorMessage;
            if(e.Contains("Forbidden"))
            {
                errorMessage = $"Geo-blocking error: {e}";
                ShowError("Couldn't download. This channel link is geo-blocked and can't be accessed from your location.");
            }
            else if(e.Contains("Not Found"))
            {
                errorMessage = $"Date range error: {e}";
                ShowError("Couldn't download. You can only grab from the past, as far back as 14 days.");
            }    
            else
            {
                errorMessage = $"General error: {e}";
                ShowError("Couldn't download. Check logs for more information.");
            }

            // Log the error with current status
            LogError($"{errorMessage}\nStatus at time of error: {StatusText.Text}");
        });

    }
    private void ChangePathBtn_Click(object sender, RoutedEventArgs e)
    {
        //open file dialog
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select a folder to save to",
            InitialDirectory = savePath
        };

        if (folderDialog.ShowDialog() == true)
        {
            var folderName = folderDialog.FolderName;
            savePath = folderName;
            SavePathText.Text = "Saving to: "+savePath;
            
        }
    }

    public void ShowError(string content)
    {
        PrettyError error = new PrettyError();
        error.errorContent = content;
        error.ShowDialog();

    }

    private bool Validate()
    {
        //validate all fields
        if (_selectedSource == null)
        {
            ShowError("Please select a channel from the menu grid.");
            return false;
        }

        if (DatePicker.SelectedDate == null)
        {
            ShowError("Please select a date");
            return false;
        }

        if (StartTimePicker.Value == null)
        {
            ShowError("Please select a start time");
            return false;
        }

        if (DurationHours.Text == "0" && DurationMinutes.Text == "0" && DurationSeconds.Text == "0")
        {
            ShowError("Please enter a duration");
            return false;
        }

        return true;

    }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {

            if (!Validate()) { return; }

            DateTime pickedDate = DatePicker.SelectedDate.Value;
            DateTime pickedTime = (DateTime)StartTimePicker.Value;
            DateTime pickedDateTime = new DateTime(pickedDate.Year, pickedDate.Month, pickedDate.Day, pickedTime.Hour, pickedTime.Minute, pickedTime.Second);
            //is uk in daylight savings? if so, subtract 1 hour
            if (TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time").IsDaylightSavingTime(pickedDateTime))
            {
                pickedDateTime = pickedDateTime.AddHours(-1);
            }

            //convert to str yyyy-MM-ddTHH:mm:ssZ
            string pickedDateTimeStr = pickedDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string selectedId = _selectedSource.Id;

        int durationHrs = Convert.ToInt32(DurationHours.Text);
            int durationMins = Convert.ToInt32(DurationMinutes.Text);
            int durationSecs = Convert.ToInt32(DurationSeconds.Text);
            //add to start time, converted to yyyy-MM-ddTHH:mm:ssZ
            DateTime endTime = pickedDateTime.AddHours(durationHrs).AddMinutes(durationMins).AddSeconds(durationSecs);
            string endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    
            bool encode = EncodeCheckbox.IsChecked.Value;
            bool fastMode = FastModeCheckbox.IsChecked.Value;
    
            Downloader.StartDownload(pickedDateTimeStr, endTimeStr, selectedId, savePath, encode, fastMode);
        }


    private async Task CheckUpdatesSilent()
    {
        await Task.Delay(500);
        await UpdateChecker.CheckForUpdatesAsync();
    }
}