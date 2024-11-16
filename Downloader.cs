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

namespace BBCD3_Desktop
{
    public class Downloader
    {
        public static string TEMP_DIRECTORY = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)+ "\\TempFiles";

        //event to update status Text
        public static event EventHandler<string> StatusUpdate;
        public static event EventHandler<string> DownloadError;
        public static event EventHandler<int> ProgressUpdated;




        public static async Task StartDownload(string startTimeStr, string endTimeStr, string channel, string finalPath, bool encode)
        {
            StatusUpdate?.Invoke(null, "Starting download...");

            string jobUuid = Guid.NewGuid().ToString();
            
            DateTime startTime = DateTime.ParseExact(startTimeStr, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            DateTime endTime = DateTime.ParseExact(endTimeStr, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);


            // Convert to Unix timestamps
            long startTimestamp = new DateTimeOffset(startTime).ToUnixTimeSeconds();
            long endTimestamp = new DateTimeOffset(endTime).ToUnixTimeSeconds();

            await Clip(channel, startTimestamp, endTimestamp, jobUuid, finalPath, encode);
        }

        static async Task DownloadSegment(string url, string filename)
        {
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filename, data);
                }
                else
                {

                    throw new Exception($"Downloading segment failed: {response.ReasonPhrase}");
                }
            }
        }

        static async Task<string[]> DownloadSegments(string channel, string jobUuid, int[] segmentIdxRange, int maxConcurrentDownloads)
        {
            string urlPrefix = SOURCES.GetSource(channel).UrlPrefix;

            string videoInitUrl = $"{urlPrefix}v=pv14/b=5070016/segment.init";
            string audioInitUrl = $"{urlPrefix}a=pa3/al=en-GB/ap=main/b=96000/segment.init";

            string videoInitFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_init.m4s";
            string audioInitFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_init.m4s";

            int totalSegments = (segmentIdxRange[1] - segmentIdxRange[0] + 1) * 2 + 2; // includes video/audio init segments
            int completedSegments = 0;

            // Update progress after each segment download completes
            void UpdateProgress()
            {
                int progress = (int)((double)completedSegments / totalSegments * 100);
                ProgressUpdated?.Invoke(null, progress);
            }

            // Semaphore to control max concurrent downloads
            SemaphoreSlim semaphore = new SemaphoreSlim(maxConcurrentDownloads);
            List<Task> downloadTasks = new List<Task>
    {
        // Initial video and audio download tasks
        Task.Run(async () =>
        {
            await semaphore.WaitAsync();
            try
            {
                await DownloadSegment(videoInitUrl, videoInitFilename);
                completedSegments++;
                UpdateProgress();
            }
            finally { semaphore.Release(); }
        }),
        Task.Run(async () =>
        {
            await semaphore.WaitAsync();
            try
            {
                await DownloadSegment(audioInitUrl, audioInitFilename);
                completedSegments++;
                UpdateProgress();
            }
            finally { semaphore.Release(); }
        })
    };

            // Add tasks for each video and audio segment
            for (int segmentIdx = segmentIdxRange[0]; segmentIdx <= segmentIdxRange[1]; segmentIdx++)
            {
                string videoUrl = $"{urlPrefix}t=3840/v=pv14/b=5070016/{segmentIdx}.m4s";
                string videoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_{segmentIdx}.m4s";
                string audioUrl = $"{urlPrefix}t=3840/a=pa3/al=en-GB/ap=main/b=96000/{segmentIdx}.m4s";
                string audioFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_{segmentIdx}.m4s";

                // Video download task
                downloadTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await DownloadSegment(videoUrl, videoFilename);
                        completedSegments++;
                        // Update status for every 5 segments 
                        if (segmentIdx % 5 == 0)
                        {
                            StatusUpdate?.Invoke(null, $"Downloading segment {segmentIdx}...");
                            await Task.Delay(100); // Small delay to allow UI to refresh
                        }
                        UpdateProgress();
                    }
                    finally { semaphore.Release(); }
                }));

                // Audio download task
                downloadTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await DownloadSegment(audioUrl, audioFilename);
                        completedSegments++;
                        // Update status for every 5 segments 
                        if (segmentIdx % 5 == 0)
                        {
                            StatusUpdate?.Invoke(null, $"Downloading segment {segmentIdx}...");
                            await Task.Delay(100); // Small delay to allow UI to refresh
                        }
                        UpdateProgress();
                    }
                    finally { semaphore.Release(); }
                }));
            }

            await Task.WhenAll(downloadTasks);
            return new[] { videoInitFilename, audioInitFilename };
        }



        static async Task CombineSegments(string jobUuid, int[] segmentIdxRange, string videoInitFilename, string audioInitFilename, string outputFilename, string finalPath, bool encode)
        {

            Console.WriteLine("Combining segments...");
            StatusUpdate?.Invoke(null, "Combining segments...");
            string videoFiles = $"concat:{videoInitFilename}";
            string audioFiles = $"concat:{audioInitFilename}";

            for (int number = segmentIdxRange[0]; number <= segmentIdxRange[1]; number++)
            {
                Console.WriteLine($"Adding segment {number} to the concatenation...");
                videoFiles += $"|{TEMP_DIRECTORY}/{jobUuid}/video_{number}.m4s";
                audioFiles += $"|{TEMP_DIRECTORY}/{jobUuid}/audio_{number}.m4s";
            }

            string concatenatedVideoFilename = $"{TEMP_DIRECTORY}/{jobUuid}/video_full.mp4";
            string concatenatedAudioFilename = $"{TEMP_DIRECTORY}/{jobUuid}/audio_full.mp4";

            string[] concatCommands = {
            $"-loglevel quiet -i \"{videoFiles}\" -c copy {concatenatedVideoFilename}",
            $"-loglevel quiet -i \"{audioFiles}\" -c copy {concatenatedAudioFilename}"
        };

            List<Task> ffmpegTasks = new List<Task>();

            Console.WriteLine($"Running command: {concatCommands[0]}");
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

            Console.WriteLine($"Running command: {concatCommands[1]}");
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


            
            Console.WriteLine("Concatenation complete");

            await Task.Delay(1000);

            string finalCommand;
            StatusUpdate?.Invoke(null, $"Joining video and audio...");
            if (encode)
            {
                finalCommand = $"-loglevel quiet -i {concatenatedVideoFilename} -i {concatenatedAudioFilename} -c:v libx264 -c:a copy {outputFilename}";
                Console.WriteLine($"Running command: {finalCommand}");
                StatusUpdate?.Invoke(null, $"Encoding... This may take a while!!");
                await RunProcess(finalCommand);

            }
            else
            {
                finalCommand = $"-loglevel quiet -i {concatenatedVideoFilename} -i {concatenatedAudioFilename} -c:v copy -c:a copy {outputFilename}";
                Console.WriteLine($"Running command: {finalCommand}");
                await RunProcess(finalCommand);

            }


            StatusUpdate?.Invoke(null, $"Cleaning up...");
            Console.WriteLine("Cleaning up...");

            

            //copy the file to desired directory
            File.Copy(outputFilename, Path.Combine(Path.GetFullPath(finalPath), Path.GetFileName(outputFilename)), true);

            StatusUpdate?.Invoke(null, $"Finished :)");
            Console.WriteLine("Cleanup complete");
        }



        static async Task Clip(string channel, long startTimestamp, long endTimestamp, string jobUuid, string finalPath, bool encode)
        {

            //string outputFilename = $"{TEMP_DIRECTORY}/{jobUuid}/{jobUuid}.mp4";
            string fancyName = $"{channel}_{startTimestamp}_{endTimestamp}.mp4";
            fancyName = fancyName.Replace(" ", "-").Replace("-[UK-Only]-","").Replace("-[US-Only]-", "");
            string outputFilename = $"{TEMP_DIRECTORY}/{jobUuid}/{fancyName}";

            try
            {
                try
                {
                    Console.WriteLine(
                        $"Starting clip from channel {channel} from {startTimestamp}-{endTimestamp} with UUID {jobUuid}");

                    int[] segmentIdxRange = Array.ConvertAll(new[] { startTimestamp, endTimestamp },
                        bound => CalculateSegmentIdx(bound + 38));

                    if (!Directory.Exists($"{TEMP_DIRECTORY}/{jobUuid}"))
                    {
                        Directory.CreateDirectory($"{TEMP_DIRECTORY}/{jobUuid}");
                    }
                    try
                    {
                        var filenames = await DownloadSegments(channel, jobUuid, segmentIdxRange, maxConcurrentDownloads: 10); 


                        await CombineSegments(jobUuid, segmentIdxRange, filenames[0], filenames[1], outputFilename, finalPath ,encode);

                        Console.WriteLine($"Clip created at {outputFilename}");
                    }
                    catch (Exception e)
                    {
                        DownloadError?.Invoke(null, e.Message);
                        Console.WriteLine($"Error: {e.Message}");
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

                    Console.WriteLine("Clearing temp files...");
                    if (Directory.Exists($"{TEMP_DIRECTORY}/{jobUuid}"))
                    {
                        Directory.Delete($"{TEMP_DIRECTORY}/{jobUuid}", true);
                    }
                }
            }
            catch (Exception e)
            {
                DownloadError?.Invoke(null, e.Message);
                Console.WriteLine($"Error: {e.Message}");
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
                DownloadError?.Invoke(null, error);
                Console.WriteLine($"Error: {error}");
                throw new Exception($"Process failed with exit code {process.ExitCode}");
            }
            else
            {
                Console.WriteLine(output);
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
            { "BBC One South East [UK Only] ", new Source { UrlPrefix = "https://pub-c2-b4-thdow-bbc.live.bidi.net.uk/vs-cmaf-pushb-uk/x=4/i=urn:bbc:pips:service:bbc_one_south_east/" } },
            { "BBC One South West [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_west/" } },
            { "BBC One West [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west/" } },
            { "BBC One West Midlands [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west_midlands/" } },
            { "BBC One Yorkshire [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_yorks/" } },
            { "BBC Two England [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-push-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_hd/" } },
            { "BBC Two Northern Ireland [UK Only] ", new Source { UrlPrefix = "https://pub-c4-b7-thdow-bbc.live.bidi.net.uk/vs-cmaf-pushb-uk/x=4/i=urn:bbc:pips:service:bbc_two_northern_ireland_hd/" } },
            { "BBC Two Wales [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_wales_digital/" } },
            { "BBC THREE [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_three_hd/" } },
            { "BBC Four [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_four_hd/" } },
            { "CBBC [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbbc_hd/" } },
            { "CBEEBIES [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbeebies_hd/" } },
            { "BBC Scotland [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_scotland_hd/" } },
            { "BBC Parliament [UK Only] ", new Source { UrlPrefix = "https://pub-c3-b7-eqsl-bbc.live.bidi.net.uk/vs-cmaf-pushb-uk/x=4/i=urn:bbc:pips:service:bbc_parliament/" } },
            { "BBC ALBA [UK Only] ", new Source { UrlPrefix = "https://pub-c2-b6-rbsov-bbc.live.bidi.net.uk/vs-cmaf-pushb-uk/x=4/i=urn:bbc:pips:service:bbc_alba/" } },
            { "S4C [UK Only] ", new Source { UrlPrefix = "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:s4cpbs/" } },
            { "BBC STREAM 51", new Source { UrlPrefix = "https://ve-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_051/" } },
            { "BBC STREAM 52", new Source { UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_052/" } }

        };

        public static Source GetSource(string channel) => All[channel];
    }

    public class Source
    {
        public string UrlPrefix { get; set; }
    }

}
