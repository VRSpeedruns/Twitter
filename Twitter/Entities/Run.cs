using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Twitter.Entities
{
    class Run
    {
        public string ID;

        public string Response;

        public Run(string id)
        {
            ID = id;
        }

        public async Task DownloadData()
        {
            var url = $"https://www.speedrun.com/api/v1/runs/{ID}?embed=players,platform,game,category,category.variables";

            using (WebClient wc = new WebClient())
            {
                Response = await SRCAPICall(url, wc);
            }
        }

        public (string game, string category, string time, string player, string link) GetInfo()
        {
            if (Response == "") return (null, null, null, null, null);

            dynamic data = JsonConvert.DeserializeObject(Response);
            data = data.data;

            string game = data.game.data.names.international;
            string category = data.category.data.name;
            string subcats = "";
            var _subcats = new List<string>();

            foreach (var variable in data.category.data.variables.data)
            {
                if ((bool)variable["is-subcategory"])
                {
                    _subcats.Add((string)variable.values.values[(string)data.values[(string)variable.id]].label);
                }
            }
            if (_subcats.Count > 0)
            {
                subcats = $" ({string.Join(", ", _subcats)})";
            }

            var time = ((string)data.times.primary).Replace("PT", "").Replace("H", "h ").Replace("M", "m ");
            if (time.Contains("."))
            {
                time = time.Replace(".", "s ").Replace("S", "ms");
                var ms = time.Split(new[] { "s " }, StringSplitOptions.None)[1].Split(new[] { "ms" }, StringSplitOptions.None)[0];
                ms = Regex.Replace(ms, @"^0", "");

                time = $"{time.Split(new[] { "s " }, StringSplitOptions.None)[0]}s {ms}ms";
            }
            else
            {
                time = time.Replace("S", "s");
            }

            var player = "";
            if (data.players.data[0].rel == "user")
            {
                player = data.players.data[0].names.international;
            }
            else
            {
                player = data.players.data[0].name;
            }

            return (game, category + subcats, time, player, data.weblink);
        }

        public static async Task<string> SRCAPICall(string url, WebClient wc)
        {
            string result;
            try
            {
                result = await wc.DownloadStringTaskAsync(url);
            }
            catch (Exception e)
            {
                result = e.Message;
            }
            return result;
        }
    }
}
