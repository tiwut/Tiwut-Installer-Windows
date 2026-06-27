using System;
using System.Drawing;
using System.IO;

namespace IconGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            string inPath = "icon.png";
            string outPath = "app.ico";
            try
            {
                if (!File.Exists(inPath))
                {
                    Console.Error.WriteLine("Input file not found: " + inPath);
                    Environment.Exit(1);
                }

                using (Image img = Image.FromFile(inPath))
                {
                    SavePngAsIcon(img, outPath);
                }
                Console.WriteLine("Successfully converted icon.png to app.ico");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                Environment.Exit(1);
            }
        }

        static void SavePngAsIcon(Image image, string iconPath)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byte[] pngData = ms.ToArray();

                using (FileStream fs = new FileStream(iconPath, FileMode.Create))
                {
                    fs.WriteByte(0); fs.WriteByte(0);
                    fs.WriteByte(1); fs.WriteByte(0);
                    fs.WriteByte(1); fs.WriteByte(0);

                    fs.WriteByte((byte)(image.Width >= 256 ? 0 : image.Width));
                    fs.WriteByte((byte)(image.Height >= 256 ? 0 : image.Height));
                    fs.WriteByte(0);
                    fs.WriteByte(0);
                    fs.WriteByte(1); fs.WriteByte(0);
                    fs.WriteByte(32); fs.WriteByte(0);

                    byte[] sizeBytes = BitConverter.GetBytes(pngData.Length);
                    fs.Write(sizeBytes, 0, 4);

                    byte[] offsetBytes = BitConverter.GetBytes(22);
                    fs.Write(offsetBytes, 0, 4);

                    fs.Write(pngData, 0, pngData.Length);
                }
            }
        }
    }
}
