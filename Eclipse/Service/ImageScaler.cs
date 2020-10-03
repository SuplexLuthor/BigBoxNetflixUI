﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eclipse.Service
{
    public class ImageScaler
    {
        public static int GetDesiredHeight()
        {
            /*
             *                                      = (5/18)*Height - 4         =Height / 2         =(4/18)*Height
             * Resolution	Width	Height	        Game Front Height	        Background Height	Logo Height
                Laptop	    1366	768	            209.3333333	                384	                170.6666667
                1080	    1920	1080	        296	                        540	                240
                1440	    2560	1440	        396	                        720	                320
                4K	        3840	2160	        596	                        1080	            480
             */
            // todo: currently picking up primary display but should check for the screen in the big box settings for PrimaryMonitorIndex
            return (int)(System.Windows.SystemParameters.PrimaryScreenHeight * 5 / 18) - 4;
        }

        public static List<FileInfo> GetMissingImageFiles()
        {
            // enumerate platform directories 
            IEnumerable<string> platformImageDirectories = Directory.EnumerateDirectories(Helpers.LaunchboxImagesPath);
            List<string> foldersToProcess = new List<string>();
            List<FileInfo> filesToProcess = new List<FileInfo>();

            foreach (string platformImageDirectory in platformImageDirectories)
            {
                IEnumerable<string> imageDirectories = Directory.EnumerateDirectories(platformImageDirectory);
                foreach (string imageDirectory in imageDirectories)
                {
                    // todo: get list of directories from launchbox settings
                    if (imageDirectory.EndsWith("GOG Poster", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Steam Poster", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Epic Games Poster", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Box - Front", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Box - Front - Reconstructed", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Advertisement Flyer - Front", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Origin Poster", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Uplay Thumbnail", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Fanart - Box - Front", StringComparison.InvariantCultureIgnoreCase)
                    || imageDirectory.EndsWith("Steam Banner", StringComparison.InvariantCultureIgnoreCase))
                    {
                        IEnumerable<string> folders = Directory.EnumerateDirectories(imageDirectory);
                        foreach(string folder in folders)
                        {
                            foldersToProcess.Add(folder);
                        }
                        foldersToProcess.Add(imageDirectory);
                    }
                }
            }

            // get the list of files that are in the launchbox image folders but not in the plug-in image folders
            foreach(string folder in foldersToProcess)
            {
                filesToProcess.AddRange(GetMissingFilesInFolder(folder));
            }

            return filesToProcess;
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
            return destImage;
        }



        public static IEnumerable<FileInfo> GetMissingFilesInFolder(string directory)
        {
            string pathA = directory;
            string pathB = directory.Replace(Helpers.ApplicationPath, Helpers.MediaFolder);

            if(!Directory.Exists(pathB))
            {
                Directory.CreateDirectory(pathB);
            }

            DirectoryInfo dir1 = new DirectoryInfo(pathA);
            DirectoryInfo dir2 = new DirectoryInfo(pathB);

            // Take a snapshot of the file system.  
            IEnumerable<FileInfo> list1 = dir1.GetFiles("*.*", SearchOption.TopDirectoryOnly);
            IEnumerable<FileInfo> list2 = dir2.GetFiles("*.*", SearchOption.TopDirectoryOnly);

            FileCompare fileCompare = new FileCompare();

            // Find the files in the LB folder that are not in the plugin folder
            return (from file in list1 select file).Except(list2, fileCompare);
        }

        public static void ScaleImage(FileInfo fileInfo, int desiredHeight)
        {
            try
            {
                string file = fileInfo.FullName;
                int originalHeight, originalWidth, desiredWidth;
                double scale;

                using (Image originalImage = Image.FromFile(file))
                {
                    originalHeight = originalImage.Height;
                    originalWidth = originalImage.Width;
                    scale = (double)((double)desiredHeight / (double)originalHeight);
                    desiredWidth = (int)(originalWidth * scale);

                    using (Bitmap newBitmap = ResizeImage(originalImage, desiredWidth, desiredHeight))
                    {
                        string newFileName = file.Replace(Helpers.ApplicationPath, Helpers.MediaFolder);
                        string newFolder = Path.GetDirectoryName(newFileName);

                        if (!Directory.Exists(newFolder))
                        {
                            Directory.CreateDirectory(newFolder);
                        }
                        newBitmap.Save(newFileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while scaling an image: {0}", ex.Message);
            }
        }

    }


    // This implementation defines a very simple comparison  
    // between two FileInfo objects. It only compares the name  
    // of the files being compared  
    class FileCompare : IEqualityComparer<FileInfo>
    {
        public FileCompare() { }

        public bool Equals(FileInfo f1, FileInfo f2)
        {
            return (f1.Name == f2.Name);
        }

        // Return a hash that reflects the comparison criteria. According to the
        // rules for IEqualityComparer<T>, if Equals is true, then the hash codes must  
        // also be equal. Because equality as defined here is a simple value equality, not  
        // reference identity, it is possible that two or more objects will produce the same  
        // hash code.  
        public int GetHashCode(FileInfo fi)
        {
            string s = $"{fi.Name}";
            return s.GetHashCode();
        }
    }
}
