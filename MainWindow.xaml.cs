using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;


namespace BBCD3_Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    string savePath = Environment.GetEnvironmentVariable("USERPROFILE") + @"\" + "Downloads";

    public MainWindow()
    {
        InitializeComponent();

        foreach(string key in SOURCES.All.Keys)
        {
            ChannelsList.Items.Add(key);
        }

        Downloader.StatusUpdate += Downloader_StatusUpdate;
        Downloader.DownloadError += Downloader_DownloadError;
        Downloader.ProgressUpdated += (sender, progress) =>
        {
            Dispatcher.Invoke(() => StatusProgress.Value = progress);
        };

        StartTimePicker.Value = DateTime.Now;
        DatePicker.SelectedDate = DateTime.Now;
    }

    private void Downloader_StatusUpdate(object sender, string e)
    {
        //invoke
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = $"Status: {e}";
        });
    }

    private void Downloader_DownloadError(object sender, string e)
    {
        //invoke
        Dispatcher.Invoke(() =>
        {
           if(e.Contains("Forbidden"))
            {
                ShowError("Couldn't download. This channel link is geo-blocked and can't be accessed from your location.\n\nDebug information\nError occured at stage '" + StatusText.Text.Replace("Status: ", "") + "': " + e);
            }
           else if(e.Contains("Not Found"))
            {
                ShowError("Couldn't download. You can only grab as far back as ~13 days.");
            }    
            ShowError("Couldn't download. Check the date you've selected isn't in the future and not too far in the past (todo: check exactly how far back it can grab !!!)\n\nDebug information\nError occured at stage '"+ StatusText.Text.Replace("Status: ","")+ "': " + e);
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
        if (ChannelsList.SelectedItem == null)
        {
            ShowError("Please select a channel");
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

        if (DurationHours.Text == "")
        {
            ShowError("Please enter a duration");
            return false;
        }

        if (DurationMinutes.Text == "")
        {
            ShowError("Please enter a duration" );
            return false;
        }

        if (DurationSeconds.Text == "")
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
            //is uk in daylight savings? if so, subtrack 1 hour
            if (TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time").IsDaylightSavingTime(pickedDateTime))
            {
                pickedDateTime = pickedDateTime.AddHours(-1);
            }
            //convert to str yyyy-MM-ddTHH:mm:ssZ
            string pickedDateTimeStr = pickedDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string channel = ChannelsList.SelectedItem.ToString();

            int durationHrs = Convert.ToInt32(DurationHours.Text);
            int durationMins = Convert.ToInt32(DurationMinutes.Text);
            int durationSecs = Convert.ToInt32(DurationSeconds.Text);
            //add to start time, converted to yyyy-MM-ddTHH:mm:ssZ
            DateTime endTime = pickedDateTime.AddHours(durationHrs).AddMinutes(durationMins).AddSeconds(durationSecs);
            string endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    
            bool encode = EncodeCheckbox.IsChecked.Value;
    
            Downloader.StartDownload(pickedDateTimeStr, endTimeStr, channel, savePath, encode);
        }


    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}