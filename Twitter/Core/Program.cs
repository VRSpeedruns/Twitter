using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Core.Models;
using Tweetinvi.Parameters;
using Twitter.Entities;
using Twitter.Util;

namespace Twitter.Core
{
    class Program
    {
        private static List<string> WorldRecords = new List<string>();
        private static Config config;

        private static TwitterClient Twitter;

        private static void Main(string[] args)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (!Directory.Exists("files"))
            {
                Directory.CreateDirectory("files");
            }
            if (!File.Exists("files/config.json"))
            {
                File.WriteAllText("files/config.json", JsonConvert.SerializeObject(JsonConvert.DeserializeObject("{\"consumerKey\": \"\", \"consumerSecret\": \"\", \"accessToken\": \"\", \"accessTokenSecret\": \"\"}"), Formatting.Indented));
            }
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("files/config.json"));

            if (config.HasEmpty())
            {
                FConsole.WriteLine("&cERROR: &fPlease fully fill out 'files/config.json'");
                Console.ReadLine();
                return;
            }

            Twitter = new TwitterClient(config.consumerKey, config.consumerSecret, config.accessToken, config.accessTokenSecret);

            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            if (File.Exists("files/worldrecords.json"))
            {
                WorldRecords = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("files/worldrecords.json"));
            }
            else
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "VRSpeedruns-Twitter");
                    var result = await wc.DownloadStringTaskAsync("https://api.github.com/repos/VRSRBot/LatestWorldRecords/releases?per_page=10");
                    dynamic json = JsonConvert.DeserializeObject(result);

                    foreach (dynamic run in json)
                    {
                        WorldRecords.Add((string)run.name);
                    }
                    File.WriteAllText("files/worldrecords.json", JsonConvert.SerializeObject(WorldRecords, Formatting.Indented));
                }
            }

            var gameCount = -1;
            while (true)
            {
                using (WebClient wc = new WebClient())
                {
                    //get current game count and update twitter bio
                    try
                    {
                        var gamesDownload = await wc.DownloadStringTaskAsync("https://vrspeed.run/vrsrassets/other/games.json");
                        dynamic gamesJson = JsonConvert.DeserializeObject(gamesDownload);

                        if (gameCount != (int)gamesJson.Count)
                        {
                            gameCount = (int)gamesJson.Count;

                            var user = await Twitter.Users.GetAuthenticatedUserAsync();
                            var bio = user.Description.Split(new[] { "Currently watching " }, StringSplitOptions.None)[0];
                            bio += $"Currently watching {gameCount} games.";

                            var parameters = new UpdateProfileParameters();
                            parameters.Description = bio;

                            await Twitter.AccountSettings.UpdateProfileAsync(parameters);
                        }
                    }
                    catch (Exception e)
                    {
                        FConsole.WriteLine($"&8- &cERROR: &fError when trying to update bio game count.\n&8-- &7{e.Message}");
                    }



                    wc.Headers.Add("User-Agent", "VRSpeedruns-Discord");
                    string result = "";

                    try
                    {
                        result = await wc.DownloadStringTaskAsync("https://api.github.com/repos/VRSRBot/LatestWorldRecords/releases?per_page=10");
                    }
                    catch (Exception e)
                    {
                        FConsole.WriteLine($"&8- &cERROR: &fError when trying to get latest WRs.\n&8-- &7{e.Message}");
                    }

                    if (result != "")
                    {
                        dynamic json = JsonConvert.DeserializeObject(result);
                        var count = 0;

                        FConsole.WriteLine("Performing world record check...");

                        for (var i = json.Count - 1; i >= 0; i--)
                        {
                            if (!WorldRecords.Contains((string)json[i].name))
                            {
                                var run = new Run((string)json[i].name);
                                var success = await SendTweet(run);

                                if (success)
                                {
                                    count++;
                                    WorldRecords.Add((string)json[i].name);
                                    File.WriteAllText("files/worldrecords.json", JsonConvert.SerializeObject(WorldRecords, Formatting.Indented));
                                }
                            }
                        }

                        FConsole.WriteLine($"Found and posted {count} new world records.");
                    }
                }

                await Task.Delay(600000); //10 min
            }
        }

        private static async Task<bool> SendTweet(Run run)
        {
            await run.DownloadData();
            var info = run.GetInfo();

            try
            {
                await Twitter.Tweets.PublishTweetAsync($"🏆 NEW VR WORLD RECORD! 🏆\n\n{info.game} - {info.category}\n\nRun completed in {info.time} by {info.player}\n\n{info.link}");

            }
            catch (Exception e)
            {
                FConsole.WriteLine($"&8- &cERROR: &fException when posting tweet. Record '{run.ID}' skipped.\n&8-- &7{e.Message}");
                return false;
            }
            FConsole.WriteLine($"&8- &fPosted tweet for record '{run.ID}'");
            return true;
        }
    }
}
