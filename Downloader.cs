using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace BBCD3_Desktop
{
    public class Downloader
    {
        public static string TEMP_DIRECTORY = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\TempFiles";
        const int MAX_RETRIES = 3;
        const int RETRY_DELAY_MS = 1000;
        public static bool FAST_MODE = true;  // Fast mode (Parallel Downloads) Stable Mode (Sequential Downloads)
        private static string _logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "download_log.txt");
        private static bool _isErrorDisplayed = false; // Static to persist across Clip calls within a download.
        private static bool _hasDownloadFailed = false; // Flag to signal a download failure.

        //event to update status Text
        public static event EventHandler<string> StatusUpdate;
        public static event EventHandler<string> DownloadError;
        public static event EventHandler<int> ProgressUpdated;

        public static async Task StartDownload(string startTimeStr, string endTimeStr, string channel, string finalPath, bool encode, bool fastMode)
        {
            FAST_MODE = fastMode;
            StatusUpdate?.Invoke(null, "Starting download...");

            try
            {
                File.WriteAllText(_logFilePath, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing log file: {ex.Message}");
                StatusUpdate?.Invoke(null, $"[ERROR] Error clearing log file: {ex.Message}");
            }

            string jobUuid = Guid.NewGuid().ToString();

            DateTime startTime = DateTime.ParseExact(startTimeStr, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            DateTime endTime = DateTime.ParseExact(endTimeStr, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            long startTimestamp = new DateTimeOffset(startTime).ToUnixTimeSeconds();
            long endTimestamp = new DateTimeOffset(endTime).ToUnixTimeSeconds();

            _isErrorDisplayed = false; // Reset at the start of each *full* download.
            _hasDownloadFailed = false; // Reset at the start of each full download.

            await Clip(channel, startTimestamp, endTimestamp, jobUuid, finalPath, encode);
        }

        static async Task<bool> DownloadSegmentWithRetry(string url, string filename)
        {
            if (_hasDownloadFailed) // Short-circuit if another download has failed
            {
                Log($"Skipping download {url} due to previous failure.", ConsoleColor.DarkGray);
                return false;
            }

            int retries = 0;
            while (retries < MAX_RETRIES)
            {
                try
                {
                    Log($"Attempting to download {url} to {filename}, attempt {retries + 1}/{MAX_RETRIES}", ConsoleColor.Yellow);

                    using (HttpClient client = new HttpClient())
                    {
                        var response = await client.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            var data = await response.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(filename, data);

                            Log($"Successfully downloaded {url} to {filename}", ConsoleColor.Green);

                            return true;
                        }
                        else
                        {
                            Log($"Download failed for {url}: {response.ReasonPhrase}.  Status Code: {response.StatusCode}", ConsoleColor.Red);
                            retries++;
                            if (retries < MAX_RETRIES)
                            {
                                Log($"Retrying in {RETRY_DELAY_MS}ms...", ConsoleColor.DarkYellow);
                                await Task.Delay(RETRY_DELAY_MS);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Exception during download of {url}: {ex.Message}", ConsoleColor.Red);
                    retries++;
                    if (retries < MAX_RETRIES)
                    {
                        Log($"Retrying in {RETRY_DELAY_MS}ms...", ConsoleColor.DarkYellow);
                        await Task.Delay(RETRY_DELAY_MS);
                    }
                }
            }

            Log($"Failed to download {url} after {MAX_RETRIES} retries.", ConsoleColor.Red, true);
            SetDownloadFailed(); // Set the failure flag.
            return false;
        }

        static async Task<string[]> DownloadSegments(string channel, string jobUuid, int[] segmentIdxRange)
        {
            string urlPrefix = SOURCES.GetSource(channel).UrlPrefix;

            string videoInitUrl = $"{urlPrefix}v=pv14/b=5070016/segment.init";
            string audioInitUrl = $"{urlPrefix}a=pa3/al=en-GB/ap=main/b=96000/segment.init";

            string videoInitFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_init.m4s";
            string audioInitFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_init.m4s";

            Log("Downloading initialization segments...", ConsoleColor.Cyan);

            bool videoInitSuccess = await DownloadSegmentWithRetry(videoInitUrl, videoInitFilename);
            if (!videoInitSuccess)
            {
                Log("Failed to download initialization segments. Aborting.", ConsoleColor.Red, true);
                SetDownloadFailed();
                SetErrorDisplayed();
                return null;
            }

            bool audioInitSuccess = await DownloadSegmentWithRetry(audioInitUrl, audioInitFilename);
            if (!audioInitSuccess)
            {
                Log("Failed to download initialization segments. Aborting.", ConsoleColor.Red, true);
                SetDownloadFailed();
                SetErrorDisplayed();
                return null;
            }

            // Calculate total segments for progress reporting.
            int totalSegments = (segmentIdxRange[1] - segmentIdxRange[0] + 1) * 2;

            // Track completed segment downloads
            int completedSegments = 0;

            if (FAST_MODE)
            {
                Log("Downloading segments in FAST MODE (parallel downloads)...", ConsoleColor.Magenta);

                List<Task> downloadTasks = new List<Task>();
                for (int segmentIdx = segmentIdxRange[0]; segmentIdx <= segmentIdxRange[1]; segmentIdx++)
                {
                    string videoUrl = $"{urlPrefix}t=3840/v=pv14/b=5070016/{segmentIdx}.m4s";
                    string videoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_{segmentIdx}.m4s";
                    string audioUrl = $"{urlPrefix}t=3840/a=pa3/al=en-GB/ap=main/b=96000/{segmentIdx}.m4s";
                    string audioFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_{segmentIdx}.m4s";

                    // Stop if a download has failed
                    if (_hasDownloadFailed) break;

                    downloadTasks.Add(DownloadSegmentAsync(videoUrl, videoFilename, () =>
                    {
                        Interlocked.Increment(ref completedSegments);
                        UpdateProgress(completedSegments, totalSegments);
                    }));
                    downloadTasks.Add(DownloadSegmentAsync(audioUrl, audioFilename, () =>
                    {
                        Interlocked.Increment(ref completedSegments);
                        UpdateProgress(completedSegments, totalSegments);
                    }));
                }
                await Task.WhenAll(downloadTasks);

            }
            else
            {
                Log("Downloading segments in STABLE MODE (sequential downloads)...", ConsoleColor.DarkYellow);

                for (int segmentIdx = segmentIdxRange[0]; segmentIdx <= segmentIdxRange[1]; segmentIdx++)
                {
                    // Stop if a download has failed
                    if (_hasDownloadFailed) break;

                    string videoUrl = $"{urlPrefix}t=3840/v=pv14/b=5070016/{segmentIdx}.m4s";
                    string videoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_{segmentIdx}.m4s";
                    Log($"Downloading video segment {segmentIdx}...", ConsoleColor.White);
                    bool videoSuccess = await DownloadSegmentWithRetry(videoUrl, videoFilename);
                    if (videoSuccess)
                    {
                        Interlocked.Increment(ref completedSegments);
                        UpdateProgress(completedSegments, totalSegments);
                    }

                    string audioUrl = $"{urlPrefix}t=3840/a=pa3/al=en-GB/ap=main/b=96000/{segmentIdx}.m4s";
                    string audioFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_{segmentIdx}.m4s";
                    Log($"Downloading audio segment {segmentIdx}...", ConsoleColor.White);
                    bool audioSuccess = await DownloadSegmentWithRetry(audioUrl, audioFilename);
                    if (audioSuccess)
                    {
                        Interlocked.Increment(ref completedSegments);
                        UpdateProgress(completedSegments, totalSegments);
                    }
                }
            }

            if (_hasDownloadFailed) // Check if downloads were aborted
            {
                return null; // Do not pass incomplete downloads to combine
            }

            return new[] { videoInitFilename, audioInitFilename };
        }

        static async Task DownloadSegmentAsync(string url, string filename, Action onComplete)
        {
            Log($"Downloading segment asynchronously: {url} to {filename}", ConsoleColor.White);
            bool success = await DownloadSegmentWithRetry(url, filename);

            if (success)
            {
                onComplete?.Invoke();
            }
            else
            {
                SetDownloadFailed(); // Signal the download failure in parallel.
            }
        }

        static void SetDownloadFailed()
        {
            _hasDownloadFailed = true;
            SetErrorDisplayed(); // Also make sure error flag is set
        }

        static void SetErrorDisplayed()
        {
            _isErrorDisplayed = true;
        }

        //Progress update
        static void UpdateProgress(int completedSegments, int totalSegments)
        {
            //Calculate progress as percentage
            int progress = (int)((double)completedSegments / totalSegments * 100);

            //Report progress
            ProgressUpdated?.Invoke(null, progress);
        }

        static async Task CombineSegments(string jobUuid, int[] segmentIdxRange, string videoInitFilename, string audioInitFilename, string outputFilename, string finalPath, bool encode)
        {
            if (_hasDownloadFailed)
            {
                Log("Skipping combining segments due to download failure.", ConsoleColor.DarkGray);
                return; // Abort combining segments.
            }

            Log("Combining segments...", ConsoleColor.Cyan);
            StatusUpdate?.Invoke(null, "Combining segments...");

            List<string> videoFileList = new List<string>() { videoInitFilename };
            List<string> audioFileList = new List<string>() { audioInitFilename };

            for (int number = segmentIdxRange[0]; number <= segmentIdxRange[1]; number++)
            {
                string videoSegmentPath = $"{TEMP_DIRECTORY}/{jobUuid}/video_{number}.m4s";
                string audioSegmentPath = $"{TEMP_DIRECTORY}/{jobUuid}/audio_{number}.m4s";

                if (File.Exists(videoSegmentPath))
                {
                    Log($"Adding video segment {number} to the concatenation...", ConsoleColor.Green);
                    videoFileList.Add(videoSegmentPath);
                }
                else
                {
                    Log($"Video segment {number} not found, skipping.", ConsoleColor.DarkGray);
                }

                if (File.Exists(audioSegmentPath))
                {
                    Log($"Adding audio segment {number} to the concatenation...", ConsoleColor.Green);
                    audioFileList.Add(audioSegmentPath);
                }
                else
                {
                    Log($"Audio segment {number} not found, skipping.", ConsoleColor.DarkGray);
                }
            }

            string videoFiles = "concat:" + string.Join("|", videoFileList);
            string audioFiles = "concat:" + string.Join("|", audioFileList);

            string concatenatedVideoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_full.mp4";
            string concatenatedAudioFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_full.mp4";

            string[] concatCommands = {
            $"-loglevel quiet -i \"{videoFiles}\" -c copy {concatenatedVideoFilename}",
            $"-loglevel quiet -i \"{audioFiles}\" -c copy {concatenatedAudioFilename}"
        };

            List<Task> ffmpegTasks = new List<Task>();

            Log($"Running command: {concatCommands[0]}", ConsoleColor.White);
            Process process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    FileName = "ffmpeg",
                    Arguments = concatCommands[0]
                }
            };
            process.Start();
            process.WaitForExit();

            Log($"Running command: {concatCommands[1]}", ConsoleColor.White);
            Process processAudio = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    FileName = "ffmpeg",
                    Arguments = concatCommands[1]
                }
            };
            processAudio.Start();
            processAudio.WaitForExit();

            Log("Concatenation complete", ConsoleColor.Cyan);

            await Task.Delay(1000);

            string finalCommand;
            StatusUpdate?.Invoke(null, $"Joining video and audio...");
            if (encode)
            {
                finalCommand = $"-loglevel quiet -i {concatenatedVideoFilename} -i {concatenatedAudioFilename} -c:v libx264 -c:a copy {outputFilename}";
                Log($"Running command: {finalCommand}", ConsoleColor.White);
                StatusUpdate?.Invoke(null, $"Encoding... This may take a while!!");
                await RunProcess(finalCommand);

            }
            else
            {
                finalCommand = $"-loglevel quiet -i {concatenatedVideoFilename} -i {concatenatedAudioFilename} -c:v copy -c:a copy {outputFilename}";
                Log($"Running command: {finalCommand}", ConsoleColor.White);
                await RunProcess(finalCommand);
            }

            StatusUpdate?.Invoke(null, $"Cleaning up...");
            Log("Cleaning up...", ConsoleColor.Cyan);

            //copy the file to desired directory
            File.Copy(outputFilename, Path.Combine(Path.GetFullPath(finalPath), Path.GetFileName(outputFilename)), true);

            StatusUpdate?.Invoke(null, $"Finished :)");
            Log("Cleanup complete", ConsoleColor.Cyan);
        }

        static async Task Clip(string channel, long startTimestamp, long endTimestamp, string jobUuid, string finalPath, bool encode)
        {
            string fancyName = $"{channel}_{startTimestamp}_{endTimestamp}.mp4";
            fancyName = fancyName.Replace(" ", "-").Replace("-[UK-Only]-", "").Replace("-[US-Only]-", "");
            string outputFilename = $"{TEMP_DIRECTORY}/{jobUuid}/{fancyName}";

            try
            {
                try
                {
                    Log($"Starting clip from channel {channel} from {startTimestamp}-{endTimestamp} with UUID {jobUuid}", ConsoleColor.Cyan);

                    int[] segmentIdxRange = Array.ConvertAll(new[] { startTimestamp, endTimestamp },
                        bound => CalculateSegmentIdx(bound + 38));

                    if (!Directory.Exists($"{TEMP_DIRECTORY}/{jobUuid}"))
                    {
                        Directory.CreateDirectory($"{TEMP_DIRECTORY}/{jobUuid}");
                    }
                    try
                    {
                        var filenames = await DownloadSegments(channel, jobUuid, segmentIdxRange);

                        if (filenames != null && !_hasDownloadFailed) // Check if downloads were successful AND no download has failed
                        {
                            await CombineSegments(jobUuid, segmentIdxRange, filenames[0], filenames[1], outputFilename, finalPath, encode);
                            Log($"Clip created at {outputFilename}", ConsoleColor.Cyan);
                        }
                        else
                        {
                            Log("Download failed. Aborting combining segments.", ConsoleColor.Red, true);
                        }
                    }
                    catch (Exception e)
                    {
                        Log($"Error: {e.Message}", ConsoleColor.Red, true);

                        if (!_isErrorDisplayed)
                        {
                            DownloadError?.Invoke(null, $"Error in Clip: {e.Message}");
                            _isErrorDisplayed = true;
                        }
                        throw;
                    }
                }
                finally
                {
                    //kill the process
                    Process[] processes = Process.GetProcessesByName("ffmpeg");
                    foreach (Process process in processes)
                    {
                        process.Kill();
                    }

                    Log("Clearing temp files...", ConsoleColor.Cyan);
                    if (Directory.Exists($"{TEMP_DIRECTORY}/{jobUuid}"))
                    {
                        Directory.Delete($"{TEMP_DIRECTORY}/{jobUuid}", true);
                    }
                }
            }
            catch (Exception e)
            {
                Log($"Error: {e.Message}", ConsoleColor.Red, true);
                if (!_isErrorDisplayed)
                {
                    DownloadError?.Invoke(null, $"Error in Clip: {e.Message}");
                    _isErrorDisplayed = true;
                }

            }

        }

        static int CalculateSegmentIdx(long timestamp)
        {
            return (int)Math.Floor(timestamp / 3.840000074);
        }

        static async Task RunProcess(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"{command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Log($"Error: {error}", ConsoleColor.Red, true);
                SetDownloadFailed();
                throw new Exception($"Process failed with exit code {process.ExitCode}");
            }
            else
            {
                Console.WriteLine(output);
            }
        }

        static void Log(string message, ConsoleColor color = ConsoleColor.White, bool isError = false)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();

            try
            {
                using (StreamWriter writer = File.AppendText(_logFilePath))
                {
                    writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
                StatusUpdate?.Invoke(null, $"[ERROR] Error writing to log file: {ex.Message}");
            }

            if (isError)
            {
                if (!_isErrorDisplayed)
                {
                    _isErrorDisplayed = true;  //set true so the message box doesn't reappear

                    StatusUpdate?.Invoke(null, $"[ERROR] {message}");  //Send status in all cases

                    //Raise one *global* error that something has failed in the total process.
                    DownloadError?.Invoke(null, message);
                }

            }
            else
            {
                if (message.StartsWith("Attempting to download"))
                {
                    StatusUpdate?.Invoke(null, "Downloading segments...");
                }
                else if (message.StartsWith("Combining segments"))
                {
                    StatusUpdate?.Invoke(null, "Combining segments...");
                }
            }
        }
    }

    public static class SOURCES
    {
        public static Dictionary<string, Source> All = new Dictionary<string, Source>
        {
            { "BBC News (United Kingdom)", new Source { UrlPrefix = "https://vs-cmaf-push-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_news_channel_hd/" } },
            { "BBC News (North America) [US Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-ntham-gcomm-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_world_news_north_america/" } },
            { "BBC Arabic", new Source { UrlPrefix = "https://vs-cmaf-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_arabic_tv/" } },
            { "BBC Persian", new Source { UrlPrefix = "https://vs-cmaf-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_persian_tv/" } },
            { "BBC One London [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-push-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_london/" } },
            { "BBC One Wales [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_wales_hd/" } },
            { "BBC One Scotland [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_scotland_hd/" } },
            { "BBC One Northern Ireland [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_northern_ireland_hd/" } },
            { "BBC One Channel Islands [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_channel_islands/" } },
            { "BBC One East [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east/" } },
            { "BBC One East Midlands [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_midlands/" } },
            { "BBC One East Yorkshire & Lincolnshire [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_yorkshire/" } },
            { "BBC One North East [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_north_east/" } },
            { "BBC One North West [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_north_west/" } },
            { "BBC One South [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south/" } },
            { "BBC One South East [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_one_south_east/" } },
            { "BBC One South West [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_one_south_west/" } },
            { "BBC One West [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west/" } },
            { "BBC One West Midlands [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west_midlands/" } },
            { "BBC One Yorkshire [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_yorks/" } },
            { "BBC Two England [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-push-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_hd/" } },
            { "BBC Two Northern Ireland [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_two_northern_ireland_hd/" } },
            { "BBC Two Wales [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_wales_digital/" } },
            { "BBC THREE [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_three_hd/" } },
            { "BBC Four [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_four_hd/" } },
            { "CBBC [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbbc_hd/" } },
            { "CBEEBIES [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbeebies_hd/" } },
            { "BBC Scotland [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_scotland_hd/" } },
            { "BBC Parliament [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_parliament/" } },
            { "BBC ALBA [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_alba/" } },
            { "S4C [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:s4cpbs/" } },
            { "BBC STREAM 51 [UK Only]", new Source { UrlPrefix = "https://ve-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_051/" } },
            { "BBC STREAM 52 [UK Only]", new Source { UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_052/" } },
            { "BBC STREAM 53 [UK Only]", new Source { UrlPrefix = "https://ve-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_053/" } }

        };

        public static Source GetSource(string channel) => All[channel];
    }

    public class Source
    {
        public string UrlPrefix { get; set; }
    }
}