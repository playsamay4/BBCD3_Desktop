using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BBCD3_Desktop
{
    public class Downloader
    {
        public static string TEMP_DIRECTORY = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\TempFiles";

        const int MAX_RETRIES = 3;
        const int RETRY_DELAY_MS = 1000;
        const int _CONCURRENT_DOWNLOAD_LIMIT = 5;

        public static bool FAST_MODE = true;  // Fast mode (Parallel Downloads) Stable Mode (Sequential Downloads)
        private static readonly string _logFilePath;
        private static bool _isErrorDisplayed = false; // Static to persist across Clip calls within a download.
        private static bool _hasDownloadFailed = false; // Flag to signal a download failure.

        //event to update status Text
        public static event EventHandler<string> StatusUpdate;
        public static event EventHandler<string> DownloadError;
        public static event EventHandler<int> ProgressUpdated;

        static Downloader()
        {
            string appFolder = AppDomain.CurrentDomain.BaseDirectory;
            string logsFolder = Path.Combine(appFolder, "logs");
            Directory.CreateDirectory(logsFolder); 
            _logFilePath = Path.Combine(logsFolder, "download_log.txt");
        }

        public static async Task StartDownload(string startTimeStr, string endTimeStr, string sourceId, string finalPath, bool encode, bool fastMode)
        {

            var source = SOURCES.GetSource(sourceId);
            if (source == null)
            {
                DownloadError?.Invoke(null, $"Invalid source ID: {sourceId}");
                return;
            }

            FAST_MODE = fastMode;
            StatusUpdate?.Invoke(null, $"Starting download {source.Name} ...");

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

            await Clip(source, startTimestamp, endTimestamp, jobUuid, finalPath, encode);
        }

        static async Task<bool> DownloadSegmentWithRetry(string url, string filename)
        {
            if (_hasDownloadFailed) return false;

            int retries = 0;
            while (retries < MAX_RETRIES)
            {
                try
                {
                    Log($"Attempting to download {url} to {filename}, attempt {retries + 1}/{MAX_RETRIES}", ConsoleColor.Yellow);

                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36 Edg/137.0.0.0");
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
                    Debug.WriteLine($"Exception during download of {url}: {ex.Message}");
                    Log($"Exception during download of {url}: {ex.Message}", ConsoleColor.Red);
                    retries++;
                    if (retries < MAX_RETRIES)
                    {
                        Debug.WriteLine($"Retrying download of {url} in {RETRY_DELAY_MS}ms...");
                        Log($"Retrying in {RETRY_DELAY_MS}ms...", ConsoleColor.DarkYellow);
                        await Task.Delay(RETRY_DELAY_MS);
                    }
                }
            }

            Log($"Failed to download {url} after {MAX_RETRIES} retries.", ConsoleColor.Red, true);
            SetDownloadFailed(); // Set the failure flag.
            return false;
        }

        static async Task<string[]> DownloadSegments(Source source, string jobUuid, int[] segmentIdxRange)
        {
            string urlPrefix = source.UrlPrefix;

            string videoInitUrl = $"{urlPrefix}{source.VideoPath}/segment.init";
            string audioInitUrl = $"{urlPrefix}{source.AudioPath}/segment.init";

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

            //Calculate total segments for progress reporting.
            int totalSegments = (segmentIdxRange[1] - segmentIdxRange[0] + 1) * 2;

            //Track completed segment downloads
            int completedSegments = 0;

            if (FAST_MODE)
            {
                Log("Downloading segments in FAST MODE (Limited Concurrency)...", ConsoleColor.Magenta);

                
                using (var semaphore = new SemaphoreSlim(_CONCURRENT_DOWNLOAD_LIMIT))
                {
                    var tasks = new List<Task>();

                    for (int segmentIdx = segmentIdxRange[0]; segmentIdx <= segmentIdxRange[1]; segmentIdx++)
                    {
                        if (_hasDownloadFailed) break;

                       
                        int idx = segmentIdx;

                        //vid  task
                        tasks.Add(Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                if (_hasDownloadFailed) return;
                                string videoUrl = $"{urlPrefix}t=3840/{source.VideoPath}/{idx}.m4s";
                                string videoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_{idx}.m4s";

                                await DownloadSegmentAsync(videoUrl, videoFilename, () =>
                                {
                                    Interlocked.Increment(ref completedSegments);
                                    UpdateProgress(completedSegments, totalSegments);
                                });
                            }
                            finally { semaphore.Release(); }
                        }));

                        //audio task
                        tasks.Add(Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                if (_hasDownloadFailed) return;
                                string audioUrl = $"{urlPrefix}t=3840/{source.AudioPath}/{idx}.m4s";
                                string audioFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_{idx}.m4s";

                                await DownloadSegmentAsync(audioUrl, audioFilename, () =>
                                {
                                    Interlocked.Increment(ref completedSegments);
                                    UpdateProgress(completedSegments, totalSegments);
                                });
                            }
                            finally { semaphore.Release(); }
                        }));
                    }

                    await Task.WhenAll(tasks);
                }
            }
            else
            {
                Log("Downloading segments in STABLE MODE (sequential downloads)...", ConsoleColor.DarkYellow);

                for (int segmentIdx = segmentIdxRange[0]; segmentIdx <= segmentIdxRange[1]; segmentIdx++)
                {
                    // Stop if a download has failed
                    if (_hasDownloadFailed) break;

                    string videoUrl = $"{urlPrefix}t=3840/{source.VideoPath}/{segmentIdx}.m4s";
                    string videoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_{segmentIdx}.m4s";
                    Log($"Downloading video segment {segmentIdx}...", ConsoleColor.White);
                    bool videoSuccess = await DownloadSegmentWithRetry(videoUrl, videoFilename);
                    if (videoSuccess)
                    {
                        Interlocked.Increment(ref completedSegments);
                        UpdateProgress(completedSegments, totalSegments);
                    }

                    string audioUrl = $"{urlPrefix}t=3840/{source.AudioPath}/{segmentIdx}.m4s";
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

            if (_hasDownloadFailed)
            {
                return null; 
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
                SetDownloadFailed(); 
            }
        }

        static void SetDownloadFailed()
        {
            _hasDownloadFailed = true;
            SetErrorDisplayed();
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
                return;
            }

            Log("Combining segments (Binary Stitching)...", ConsoleColor.Cyan);
            StatusUpdate?.Invoke(null, "Combining segments...");

            List<string> videoFiles = new List<string> { videoInitFilename };
            List<string> audioFiles = new List<string> { audioInitFilename };

            for (int number = segmentIdxRange[0]; number <= segmentIdxRange[1]; number++)
            {
                string vPath = $"{TEMP_DIRECTORY}/{jobUuid}/video_{number}.m4s";
                string aPath = $"{TEMP_DIRECTORY}/{jobUuid}/audio_{number}.m4s";

                if (File.Exists(vPath)) videoFiles.Add(vPath);
                if (File.Exists(aPath)) audioFiles.Add(aPath);
            }

            string concatenatedVideoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_full.mp4";
            string concatenatedAudioFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_full.mp4";

            try
            {
                Log($"Stitching {videoFiles.Count} video segments...", ConsoleColor.White);
                await MergeFilesAsync(videoFiles, concatenatedVideoFilename);

                Log($"Stitching {audioFiles.Count} audio segments...", ConsoleColor.White);
                await MergeFilesAsync(audioFiles, concatenatedAudioFilename);
            }
            catch (Exception ex)
            {
                Log($"Error stitching files: {ex.Message}", ConsoleColor.Red, true);
                throw;
            }

            //merge
            string finalCommand;

            Log("Merges complete. Processing with FFmpeg...", ConsoleColor.Cyan);
            StatusUpdate?.Invoke(null, $"Joining video and audio...");

            if (encode)
            {
                finalCommand = $"-i \"{concatenatedVideoFilename}\" -i \"{concatenatedAudioFilename}\" -c:v libx264 -c:a copy \"{outputFilename}\"";
                Log($"Encoding... This may take a while!!", ConsoleColor.Yellow);
                StatusUpdate?.Invoke(null, $"Encoding... This may take a while!!");
            }
            else
            {
                finalCommand = $"-i \"{concatenatedVideoFilename}\" -i \"{concatenatedAudioFilename}\" -c:v copy -c:a copy \"{outputFilename}\"";
                Log($"Multiplexing streams...", ConsoleColor.White);
            }

            await RunProcess(finalCommand);

            StatusUpdate?.Invoke(null, $"Cleaning up...");
            Log("Cleaning up...", ConsoleColor.Cyan);

            string destPath = Path.Combine(Path.GetFullPath(finalPath), Path.GetFileName(outputFilename));
            File.Copy(outputFilename, destPath, true);

            StatusUpdate?.Invoke(null, $"Finished :)");
            Log($"Cleanup complete. Saved to {destPath}", ConsoleColor.Green);
        }

        static async Task Clip(Source source, long startTimestamp, long endTimestamp, string jobUuid, string finalPath, bool encode)
        {
            string fancyName = $"{source.Id}_{startTimestamp}_{endTimestamp}.mp4";
            fancyName =
                fancyName.Replace(" ", "-");
                
            string outputFilename = $"{TEMP_DIRECTORY}/{jobUuid}/{fancyName}";

            try
            {
                try
                {
                    Log($"Starting clip from channel {source.Name} from {startTimestamp}-{endTimestamp} with UUID {jobUuid}", ConsoleColor.Cyan);

                    int[] segmentIdxRange = Array.ConvertAll(new[] { startTimestamp, endTimestamp },
                        bound => CalculateSegmentIdx(bound + 38));

                    if (!Directory.Exists($"{TEMP_DIRECTORY}/{jobUuid}"))
                    {
                        Directory.CreateDirectory($"{TEMP_DIRECTORY}/{jobUuid}");
                    }
                    try
                    {
                        var filenames = await DownloadSegments(source, jobUuid, segmentIdxRange);

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
                    Arguments = command, // Note: No extra quotes here; they are in the command string already
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // read Output and Error streams asynchronously in parallel
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync();

            string output = outputTask.Result;
            string error = errorTask.Result;

            if (process.ExitCode != 0)
            {
                Log($"FFmpeg Error Output: {error}", ConsoleColor.Red, true);
                SetDownloadFailed();
                throw new Exception($"Process failed with exit code {process.ExitCode}. Check logs.");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.WriteLine($"FFmpeg Info: {error}");
                }
            }
        }

        static async Task MergeFilesAsync(List<string> sourceFiles, string outputFile)
        {
            //output file stream
            using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                foreach (var file in sourceFiles)
                {
                    if (File.Exists(file))
                    {
                        //copy bytes to the output file
                        using (var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            await inputStream.CopyToAsync(outputStream);
                        }
                    }
                }
            }
        }

        static void Log(string message, ConsoleColor color = ConsoleColor.White, bool isError = false)
        {
            Console.ForegroundColor = color;
            Debug.WriteLine(message);
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
                Debug.WriteLine($"Error writing to log file: {ex.Message}");
                StatusUpdate?.Invoke(null, $"[ERROR] Error writing to log file: {ex.Message}");
            }

            if (isError)
            {
                if (!_isErrorDisplayed)
                {
                    _isErrorDisplayed = true;

                    StatusUpdate?.Invoke(null, $"[ERROR] {message}");  

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
            // BBC News
            { "news_uk", new Source { Id = "news_uk", Name = "BBC News UK", Category = "News", UrlPrefix = "https://vs-cmaf-push-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_news_channel_hd/" } },
            { "news_uk_fhd", new Source {
                Id = "news_uk_fhd",
                Name = "BBC News UK",
                Category = "News FHD",
                UrlPrefix = "https://vs-cmaf-push-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_news_channel_hd/",
                VideoPath = "v=pv66/b=6500000",
                AudioPath = "a=pa6/al=en-GB/ap=main/b=320000"
            } },
            { "news_na", new Source { Id = "news_na", Name = "BBC News World (North America) [US-Only Geoblock]", Category = "News", UrlPrefix = "https://vs-cmaf-pushb-ntham-gcomm-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_world_news_north_america/" } },
            { "news_af", new Source { Id = "news_af", Name = "BBC News World (Africa) [Australia-Only Geoblock]", Category = "News", UrlPrefix = "https://vs-cmaf-pushb-apac-gcomm.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_world_news_africa/" } },
            { "news_ar", new Source { Id = "news_ar", Name = "BBC News Arabic", Category = "News", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_arabic_tv/" } },
            { "news_fa", new Source { Id = "news_fa", Name = "BBC News Persian", Category = "News", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_persian_tv/" } },

            //BBC One HD
            { "one_lon", new Source { Id = "one_lon", Name = "BBC One London [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-push-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_london/" } },
            { "one_wal", new Source { Id = "one_wal", Name = "BBC One Wales [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_wales_hd/" } },
            { "one_sco", new Source { Id = "one_sco", Name = "BBC One Scotland [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_scotland_hd/" } },
            { "one_ni",  new Source { Id = "one_ni",  Name = "BBC One Northern Ireland [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_northern_ireland_hd/" } },
            { "one_ci",  new Source { Id = "one_ci",  Name = "BBC One Channel Islands [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_channel_islands/" } },
            { "one_east", new Source { Id = "one_east", Name = "BBC One East [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east/" } },
            { "one_em", new Source { Id = "one_em", Name = "BBC One East Midlands [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_midlands/" } },
            { "one_ey", new Source { Id = "one_ey", Name = "BBC One East Yorks & Lincs [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_yorkshire/" } },
            { "one_ne", new Source { Id = "one_ne", Name = "BBC One North East [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_one_north_east/" } },
            { "one_nw", new Source { Id = "one_nw", Name = "BBC One North West [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_north_west/" } },
            { "one_sou", new Source { Id = "one_sou", Name = "BBC One South [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south/" } },
            { "one_se", new Source { Id = "one_se", Name = "BBC One South East [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_east/" } },
            { "one_sw", new Source { Id = "one_sw", Name = "BBC One South West [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_west/" } },
            { "one_wes", new Source { Id = "one_wes", Name = "BBC One West [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west/" } },
            { "one_wm", new Source { Id = "one_wm", Name = "BBC One West Midlands [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west_midlands/" } },
            { "one_yor", new Source { Id = "one_yor", Name = "BBC One Yorkshire [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_yorks/" } },

            // BBC ONE FHD
            { "one_lon_fhd", new Source { Id = "one_lon_fhd", Name = "BBC One London [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-push-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_london/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_wal_fhd", new Source { Id = "one_wal_fhd", Name = "BBC One Wales [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_wales_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_sco_fhd", new Source { Id = "one_sco_fhd", Name = "BBC One Scotland [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_scotland_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_ni_fhd",  new Source { Id = "one_ni_fhd",  Name = "BBC One Northern Ireland [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_northern_ireland_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_ci_fhd",  new Source { Id = "one_ci_fhd",  Name = "BBC One Channel Islands [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_channel_islands/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_east_fhd", new Source { Id = "one_east_fhd", Name = "BBC One East [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_em_fhd", new Source { Id = "one_em_fhd", Name = "BBC One East Midlands [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_midlands/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_ey_fhd", new Source { Id = "one_ey_fhd", Name = "BBC One East Yorks & Lincs [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_yorkshire/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_ne_fhd", new Source { Id = "one_ne_fhd", Name = "BBC One North East [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_north_east/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_nw_fhd", new Source { Id = "one_nw_fhd", Name = "BBC One North West [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_north_west/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_sou_fhd", new Source { Id = "one_sou_fhd", Name = "BBC One South [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_se_fhd", new Source { Id = "one_se_fhd", Name = "BBC One South East [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_east/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_sw_fhd", new Source { Id = "one_sw_fhd", Name = "BBC One South West [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_west/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_wes_fhd", new Source { Id = "one_wes_fhd", Name = "BBC One West [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_wm_fhd", new Source { Id = "one_wm_fhd", Name = "BBC One West Midlands [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west_midlands/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_yor_fhd", new Source { Id = "one_yor_fhd", Name = "BBC One Yorkshire [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_yorks/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },

            // Other bbc HD
            { "two_eng", new Source { Id = "two_eng", Name = "BBC Two England [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-push-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_hd/" } },
            { "two_ni", new Source { Id = "two_ni", Name = "BBC Two Northern Ireland [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_northern_ireland_hd/" } },
            { "two_wal", new Source { Id = "two_wal", Name = "BBC Two Wales [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_wales_digital/" } },
            { "three", new Source { Id = "three", Name = "BBC THREE [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_three_hd/" } },
            { "four", new Source { Id = "four", Name = "BBC Four [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_four_hd/" } },
            { "cbbc", new Source { Id = "cbbc", Name = "CBBC [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:cbbc_hd/" } },
            { "cbeebies", new Source { Id = "cbeebies", Name = "CBEEBIES [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbeebies_hd/" } },
            { "scotland", new Source { Id = "scotland", Name = "BBC Scotland [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_scotland_hd/" } },
            { "parliament", new Source { Id = "parliament", Name = "BBC Parliament [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_parliament/" } },
            { "alba", new Source { Id = "alba", Name = "BBC ALBA [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_alba/" } },

            //Other bbc fhd
            { "two_eng_fhd", new Source { Id = "two_eng_fhd", Name = "BBC Two England [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-push-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "two_ni_fhd", new Source { Id = "two_ni_fhd", Name = "BBC Two Northern Ireland [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_northern_ireland_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "two_wal_fhd", new Source { Id = "two_wal_fhd", Name = "BBC Two Wales [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_wales_digital/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "three_fhd", new Source { Id = "three_fhd", Name = "BBC THREE [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_three_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "four_fhd", new Source { Id = "four_fhd", Name = "BBC Four [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_four_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "cbbc_fhd", new Source { Id = "cbbc_fhd", Name = "CBBC [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbbc_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "cbeebies_fhd", new Source { Id = "cbeebies_fhd", Name = "CBEEBIES [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbeebies_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "scotland_fhd", new Source { Id = "scotland_fhd", Name = "BBC Scotland [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_scotland_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "parliament_fhd", new Source { Id = "parliament_fhd", Name = "BBC Parliament [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_parliament/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "alba_fhd", new Source { Id = "alba_fhd", Name = "BBC ALBA [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_alba/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },

            { "s4c", new Source { Id = "s4c", Name = "S4C [UK Only]", Category = "S4C", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:s4cpbs/" } },

            // streams 
            { "stream_51_uk", new Source { Id = "stream_51_uk", Name = "BBC STREAM 51 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_051/" } },
            { "stream_52_uk", new Source { Id = "stream_52_uk", Name = "BBC STREAM 52 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_052/" } },
            { "stream_53_uk", new Source { Id = "stream_53_uk", Name = "BBC STREAM 53 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_053/" } },
            { "stream_54_uk", new Source { Id = "stream_54_uk", Name = "BBC STREAM 54 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_054/" } },
            { "stream_55_uk", new Source { Id = "stream_55_uk", Name = "BBC STREAM 55 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_055/" } },
            { "stream_56_uk", new Source { Id = "stream_56_uk", Name = "BBC STREAM 56 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_056/" } },
            { "stream_57_uk", new Source { Id = "stream_57_uk", Name = "BBC STREAM 57 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_057/" } },
            { "stream_58_uk", new Source { Id = "stream_58_uk", Name = "BBC STREAM 58 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_058/" } },
            { "stream_59_uk", new Source { Id = "stream_59_uk", Name = "BBC STREAM 59 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-hls-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_059/" } },

            { "stream_51_ww", new Source { Id = "stream_51_ww", Name = "BBC STREAM 51", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_051/" } },
            { "stream_52_ww", new Source { Id = "stream_52_ww", Name = "BBC STREAM 52", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_052/" } },
            { "stream_53_ww", new Source { Id = "stream_53_ww", Name = "BBC STREAM 53", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_053/" } },
            { "stream_54_ww", new Source { Id = "stream_54_ww", Name = "BBC STREAM 54", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_054/" } },
            { "stream_55_ww", new Source { Id = "stream_55_ww", Name = "BBC STREAM 55", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_055/" } },
            { "stream_56_ww", new Source { Id = "stream_56_ww", Name = "BBC STREAM 56", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_056/" } },
            { "stream_57_ww", new Source { Id = "stream_57_ww", Name = "BBC STREAM 57", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_057/" } },
            { "stream_58_ww", new Source { Id = "stream_58_ww", Name = "BBC STREAM 58", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_058/" } },
            { "stream_59_ww", new Source { Id = "stream_59_ww", Name = "BBC STREAM 59", Category = "BBC Streams (World)", UrlPrefix = "https://ve-hls-pushb-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:uk_bbc_stream_059/" } },

            // World service
            { "ws_05", new Source { Id = "ws_05", Name = "World Service Stream 05 (Urdu, Pashto, Burmese, Swahili, Arabic Services)", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:world_service_stream_05/" } },
            { "ws_06", new Source { Id = "ws_06", Name = "World Service Stream 06 (Telugu, Tamil, Kyrgyz, Hindi, Ukranian Services)", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:world_service_stream_06/" } },
            { "ws_07", new Source { Id = "ws_07", Name = "World Service Stream 07 (Afghan Retransmission)", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:world_service_stream_07/" } },
            { "ws_08", new Source { Id = "ws_08", Name = "World Service Stream 08 (News Asia Pacific)", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:world_service_stream_08/" } },
            { "ws_afghan", new Source { Id = "ws_afghan", Name = "BBC Afghanistan", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_afghan_tv/" } }
        };

        public static Source GetSource(string id) => All.ContainsKey(id) ? All[id] : null;
    }
}

public class Source
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public string UrlPrefix { get; set; }

    public string VideoPath { get; set; } = "v=pv14/b=5070016";

    public string AudioPath { get; set; } = "a=pa3/al=en-GB/ap=main/b=96000";
}