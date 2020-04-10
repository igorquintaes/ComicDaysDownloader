using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ComicDaysDownloader
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Please, insert a manga link with public viewing of comic-days website:");
            var document = new HtmlWeb().Load(Console.ReadLine());
            var json = document.DocumentNode.SelectSingleNode("//*[@id='episode-json']").GetDataAttribute("value").Value;
            var dynamicJson = JsonConvert.DeserializeObject<dynamic>(HtmlEntity.DeEntitize(json));
            var filesDir = Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));
            Directory.CreateDirectory(filesDir);
            Console.Clear();

            var pageCount = 1;
            var pages = ((JArray) dynamicJson.readableProduct.pageStructure.pages)
                .Where(x => !string.IsNullOrWhiteSpace(x.Value<string>("src")))
                .OrderBy(x => x.Value<string>("src"))
                .ToDictionary(_ => pageCount++, x => (dynamic) x);

            using var httpClient = new HttpClient();
            Parallel.ForEach(pages, (dic) =>
            {
                var page = dic.Key;
                var image = dic.Value;
                Console.WriteLine($"Processing page {page.ToString("00")}...");

                var filePath = Path.Combine(filesDir, $"{page}.png");
                var pageWidth = (int)image.width;
                var pageHeight = (int)image.height;
                var spacingWidth = (int)Math.Floor((double)(pageWidth / 32)) * 8;
                var spacingHeight = (int)Math.Floor((double)(pageHeight / 32)) * 8; ;

                var imageHttpResponse = httpClient.GetAsync(image.src.ToString()).GetAwaiter().GetResult();
                using var imageDownloaded = imageHttpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using var mixedImage = new Bitmap(imageDownloaded);
                using var newImage = new Bitmap(pageWidth, pageHeight);

                for (var imageX = 0; imageX + spacingWidth <= pageWidth; imageX += spacingWidth)
                {
                    for (var imageY = (imageX / spacingWidth) * spacingHeight + spacingHeight;
                         imageY + spacingHeight <= pageHeight;
                         imageY += spacingHeight)
                    {
                        var rectOldPosition = new Rectangle(imageX, imageY, spacingWidth, spacingHeight);
                        var partialImageOldPosition = mixedImage.Clone(rectOldPosition, PixelFormat.DontCare);

                        var newPositionX = (imageY / spacingHeight) * spacingWidth;
                        var newPositionY = (imageX / spacingWidth) * spacingHeight;
                        var rectNewPosition = new Rectangle(newPositionX, newPositionY, spacingWidth, spacingHeight);
                        var partialImageNewPosition = mixedImage.Clone(rectNewPosition, PixelFormat.DontCare);

                        using var graphics = Graphics.FromImage(newImage);
                        using var myBrush = new SolidBrush(Color.Red);
                        graphics.DrawImage(partialImageNewPosition, new Point(imageX, imageY));
                        graphics.DrawImage(partialImageOldPosition, new Point(newPositionX, newPositionY));
                    }
                }

                for (var middleLine = 0; middleLine < 4; middleLine++)
                {
                    var middleLineX = middleLine * spacingWidth;
                    var middleLineY = middleLine * spacingHeight;
                    var rectMiddleLine = new Rectangle(middleLineX, middleLineY, spacingWidth, spacingHeight);
                    var ImageMiddleLine = mixedImage.Clone(rectMiddleLine, PixelFormat.DontCare);

                    using var graphics = Graphics.FromImage(newImage);
                    using var myBrush = new SolidBrush(Color.Red);
                    graphics.DrawImage(ImageMiddleLine, new Point(middleLineX, middleLineY));
                }

                using MemoryStream memory = new MemoryStream();
                using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite);
                newImage.Save(memory, ImageFormat.Png);
                var bytes = memory.ToArray();
                fs.Write(bytes, 0, bytes.Length);

                Console.WriteLine($"Page {page.ToString("00")} done!");
            });
        }
    }
}
