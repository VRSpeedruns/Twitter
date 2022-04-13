using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Twitter.Core;

namespace Twitter.Entities
{
    class Run
    {
        public string ID;
        public string Response;

        private dynamic data;
        private dynamic VRSR;

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
                data = JsonConvert.DeserializeObject(Response);
                data = data.data;
            }

            for (var i = 0; i < Program.VRSRGames.Count; i++)
            {
                if ((string)Program.VRSRGames[i].api_id == (string)data.game.data.id)
                {
                    VRSR = Program.VRSRGames[i];
                }
            }
        }

        public (string game, string category, string time, string player, string link, string vrsrLink) GetInfo()
        {
            if (Response == "") return (null, null, null, null, null, null);

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

            return (game, category + subcats, time, player, data.weblink, $"https://vrspeed.run/{VRSR.abbreviation}/run/{data.id}");
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

        public async Task GenerateImage()
        {
            var info = GetInfo();

            Bitmap template = (Bitmap)Image.FromFile("files/img/template.png");
            Bitmap logo = (Bitmap)Image.FromFile("files/img/logo.png");

            using (WebClient wc = new WebClient())
            {
                try
                {
                    await wc.DownloadFileTaskAsync($"https://www.speedrun.com/gameasset/{data.game.data.id}/cover", "files/img/temp_cover.png");
                }
                catch
                {
                    await wc.DownloadFileTaskAsync("https://www.speedrun.com/images/blankcover.png", "files/img/temp_cover.png");
                }
            }

            var cover = (Bitmap)Image.FromFile("files/img/temp_cover.png");

            using (Graphics graphics = Graphics.FromImage(template))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;

                ColorMatrix matrix = new ColorMatrix { Matrix33 = 0.1f };
                ImageAttributes attributes = new ImageAttributes();
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                float scale = Math.Max((float)template.Width / cover.Width, (float)template.Height / cover.Height);
                var scaleWidth = (int)(cover.Width * scale);
                var scaleHeight = (int)(cover.Height * scale);

                // GAME COVER (BACKGROUND)
                graphics.DrawImage(cover, new Rectangle((template.Width - scaleWidth) / 2, (template.Height - scaleHeight) / 2, scaleWidth, scaleHeight), 0, 0, cover.Width, cover.Height, GraphicsUnit.Pixel, attributes);
                
                // LOGO
                graphics.DrawImage(logo, 20, 530);

                // GAME
                using (Font font = new Font("Open Sans", 70, FontStyle.Bold))
                {
                    var str = (string)VRSR.name;
                    while (graphics.MeasureString(str, font).Width > template.Width)
                    {
                        str = str.Substring(0, str.Length - 1);
                    }
                    if (str != (string)VRSR.name)
                    {
                        str = str.Substring(0, str.Length - 1) + "...";
                    }

                    graphics.DrawString(str, font, Brushes.White, new PointF(8, 0));
                }

                // CATEGORY
                using (Font font = new Font("Open Sans", 60, FontStyle.Bold))
                {
                    var str = info.category;
                    while (graphics.MeasureString(str, font).Width > template.Width)
                    {
                        str = str.Substring(0, str.Length - 1);
                    }
                    if (str != info.category)
                    {
                        str = str.Substring(0, str.Length - 1) + "...";
                    }

                    graphics.DrawString(str, font, Brushes.White, new PointF(8, 120));
                }

                // TIME
                using (Font font = new Font("Open Sans", 55, FontStyle.Regular))
                {
                    var str = info.time;
                    while (graphics.MeasureString(str, font).Width > template.Width)
                    {
                        str = str.Substring(0, str.Length - 1);
                    }
                    if (str != info.time)
                    {
                        str = str.Substring(0, str.Length - 1) + "...";
                    }

                    graphics.DrawString(str, font, Brushes.White, new PointF(8, 230));
                }

                // PLAYER
                using (Font font = new Font("Open Sans", 50, FontStyle.Bold))
                {
                    var str = info.player;
                    while (graphics.MeasureString(str, font).Width > template.Width)
                    {
                        str = str.Substring(0, str.Length - 1);
                    }

                    List<Color> colors;

                    if (data.players.data[0]["name-style"].style == "gradient")
                    {
                        colors = InterpolateColors(str.Length,
                            (string)data.players.data[0]["name-style"]["color-from"].dark,
                            (string)data.players.data[0]["name-style"]["color-to"].dark);
                    }
                    else
                    {
                        colors = InterpolateColors(str.Length,
                            (string)data.players.data[0]["name-style"].color.dark,
                            (string)data.players.data[0]["name-style"].color.dark);
                    }

                    if (str != info.player)
                    {
                        str = str.Substring(0, str.Length - 1) + "...";

                        colors.AddRange(new[] { Color.White, Color.White, Color.White });
                    }

                    colors.Reverse();
                    foreach (Color c in colors)
                    {
                        graphics.DrawString(str, font, new SolidBrush(c), new PointF(8, 330));

                        str = str.Substring(0, str.Length - 1);
                    }
                }
            }

            template = (Bitmap)RoundCorners(template, 35);

            template = DrawBitmapWithBorder(template, ColorTranslator.FromHtml((string)Program.VRSRColors[(string)VRSR.color].color));
            template.Save("files/img/out.png");
        }

        // https://stackoverflow.com/a/56035786
        private static Bitmap DrawBitmapWithBorder(Bitmap bmp, Color color)
        {
            int borderSize = 15;
            int newWidth = bmp.Width + (borderSize * 2);
            int newHeight = bmp.Height + (borderSize * 2) + 10;

            Image newImage = new Bitmap(newWidth, newHeight);
            using (Graphics gfx = Graphics.FromImage(newImage))
            {
                using (Brush border = new SolidBrush(color))
                {
                    gfx.FillRectangle(border, 0, 0,
                        newWidth, newHeight);
                }
                gfx.DrawImage(bmp, new Rectangle(borderSize, borderSize + 5, bmp.Width, bmp.Height));

            }
            return (Bitmap)newImage;
        }

        // logic from https://stackoverflow.com/a/13253053
        private static List<Color> InterpolateColors(int size, string start, string end)
        {
            (int R, int G, int B) color1 = HexToRGB(start);
            (int R, int G, int B) color2 = HexToRGB(end);

            var colors = new List<Color>();

            var interval_R = (color2.R - color1.R) / size;
            var interval_G = (color2.G - color1.G) / size;
            var interval_B = (color2.B - color1.B) / size;

            var current_R = color1.R;
            var current_G = color1.G;
            var current_B = color1.B;


            for (var i = 0; i < size; i++)
            {
                colors.Add(Color.FromArgb(current_R, current_G, current_B));
                 
                current_R += interval_R;
                current_G += interval_G;
                current_B += interval_B;
            }

            return colors;
        }

        // https://www.devx.com/tips/dot-net/c-sharp/convert-hex-to-rgb-190527095529.html
        public static (int R, int G, int B) HexToRGB(string hexString)
        {
            //replace # occurences
            if (hexString.IndexOf('#') != -1)
                hexString = hexString.Replace("#", "");

            int r = int.Parse(hexString.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int g = int.Parse(hexString.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int b = int.Parse(hexString.Substring(4, 2), NumberStyles.AllowHexSpecifier);

            return (r, g, b);
        }

        // https://stackoverflow.com/a/20757041
        private Image RoundCorners(Image image, int cornerRadius)
        {
            cornerRadius *= 2;
            Bitmap roundedImage = new Bitmap(image.Width, image.Height);
            GraphicsPath gp = new GraphicsPath();
            gp.AddArc(0, 0, cornerRadius, cornerRadius, 180, 90);
            gp.AddArc(0 + roundedImage.Width - cornerRadius, 0, cornerRadius, cornerRadius, 270, 90);
            gp.AddArc(0 + roundedImage.Width - cornerRadius, 0 + roundedImage.Height - cornerRadius, cornerRadius, cornerRadius, 0, 90);
            gp.AddArc(0, 0 + roundedImage.Height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
            using (Graphics g = Graphics.FromImage(roundedImage))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                g.SetClip(gp);
                g.DrawImage(image, Point.Empty);
            }
            return roundedImage;
        }
    }
}
