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

            _isErrorDisplayed = false; 
            _hasDownloadFailed = false; 

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
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36 Edg/149.0.0.0");
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

            Log($"Failed to download {url} after {MAX_RETRIES} retries.", ConsoleColor.Red, false);
            SetDownloadFailed(); 
            return false;
        }

        static async Task<string[]> DownloadSegments(Source source, string jobUuid, int[] segmentIdxRange)
        {
            List<string> testPrefixes = new List<string> { source.UrlPrefix };
            foreach (var provider in SOURCES.PROVIDERS)
            {
                string parsedPrefix = source.GetUrlPrefixForProvider(provider);
                if (!testPrefixes.Contains(parsedPrefix))
                {
                    testPrefixes.Add(parsedPrefix);
                }
            }

            for (int p = 0; p < testPrefixes.Count; p++)
            {
                string currentUrlPrefix = testPrefixes[p];
                _hasDownloadFailed = false;

                Log($"[PROVIDER TRY {p + 1}/{testPrefixes.Count}] Using base URL: {currentUrlPrefix}", ConsoleColor.Cyan);

                string videoInitUrl = $"{currentUrlPrefix}{source.VideoPath}/segment.init";
                string audioInitUrl = $"{currentUrlPrefix}{source.AudioPath}/segment.init";

                string videoInitFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_init.m4s";
                string audioInitFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_init.m4s";

                Log("Downloading initialization segments...", ConsoleColor.Cyan);

                bool videoInitSuccess = await DownloadSegmentWithRetry(videoInitUrl, videoInitFilename);
                if (!videoInitSuccess)
                {
                    Log($"Init video segment failed on provider {p + 1}. using next provider...", ConsoleColor.Magenta);
                    continue;
                }

                bool audioInitSuccess = await DownloadSegmentWithRetry(audioInitUrl, audioInitFilename);
                if (!audioInitSuccess)
                {
                    Log($"Init audio segment failed on provider {p + 1}. using next provider...", ConsoleColor.Magenta);
                    continue;
                }

                int totalSegments = (segmentIdxRange[1] - segmentIdxRange[0] + 1) * 2;
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

                            // video segment block
                            tasks.Add(Task.Run(async () =>
                            {
                                await semaphore.WaitAsync();
                                try
                                {
                                    if (_hasDownloadFailed) return;
                                    string videoUrl = $"{currentUrlPrefix}t=3840/{source.VideoPath}/{idx}.m4s";
                                    string videoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_{idx}.m4s";

                                    await DownloadSegmentAsync(videoUrl, videoFilename, () =>
                                    {
                                        Interlocked.Increment(ref completedSegments);
                                        UpdateProgress(completedSegments, totalSegments);
                                    });
                                }
                                finally { semaphore.Release(); }
                            }));

                            // audio segment block
                            tasks.Add(Task.Run(async () =>
                            {
                                await semaphore.WaitAsync();
                                try
                                {
                                    if (_hasDownloadFailed) return;
                                    string audioUrl = $"{currentUrlPrefix}t=3840/{source.AudioPath}/{idx}.m4s";
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
                        if (_hasDownloadFailed) break;

                        string videoUrl = $"{currentUrlPrefix}t=3840/{source.VideoPath}/{segmentIdx}.m4s";
                        string videoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_{segmentIdx}.m4s";
                        Log($"Downloading video segment {segmentIdx}...", ConsoleColor.White);
                        bool videoSuccess = await DownloadSegmentWithRetry(videoUrl, videoFilename);
                        if (videoSuccess)
                        {
                            Interlocked.Increment(ref completedSegments);
                            UpdateProgress(completedSegments, totalSegments);
                        }

                        string audioUrl = $"{currentUrlPrefix}t=3840/{source.AudioPath}/{segmentIdx}.m4s";
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

                // If this loop finished and the flag remains false, the whole set downloaded perfectly!
                if (!_hasDownloadFailed)
                {
                    Log($"All segments obtained using provider  {p + 1}!", ConsoleColor.Green);
                    return new[] { videoInitFilename, audioInitFilename };
                }
                else
                {
                    Log($"Provider {p + 1} errored out, trying next...", ConsoleColor.Magenta);
                }
            }

            Log("All provider servers failed. aborted", ConsoleColor.Red, true);
            SetErrorDisplayed();
            return null;
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

                        if (filenames != null)
                        {
                            await CombineSegments(jobUuid, segmentIdxRange, filenames[0], filenames[1], outputFilename, finalPath, encode);
                            Log($"Clip created at {outputFilename}", ConsoleColor.Cyan);
                        }
                        else
                        {
                            Log("Download failed across all providers. aborted", ConsoleColor.Red, true);
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
                    Arguments = command, 
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


 

}
