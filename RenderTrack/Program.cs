using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using GPXparser;

namespace RenderTrack
{
    class Program
    {
        static void Main(string[] args)
        {
            // create file email.txt with your e-mail adress
            string email;
            try
            {
                email = File.ReadAllText(@"email.txt");
            } catch (Exception)
            {
                Console.WriteLine("Please create a file called \"email.txt\" that contains your e-mail adress. It is required for the Open Street Map API.");
                return;
            }
            if (args.Length < 2 || args.Length > 3)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " [path to GPX file] [zoomlevel] {tile cache directory}");
                return;
            }
            int zoomLevel = int.Parse(args[1]);
            string gpxPath = args[0];
            if (!File.Exists(gpxPath))
            {
                Console.WriteLine("Cannot find file " + gpxPath);
                return;                
            }
            string cacheDir = "";
            if (zoomLevel < 0 || zoomLevel >= 20)
            {
                Console.WriteLine("Zoom level must be in [0;20].");
                return;
            }
            if (args.Length > 2)
            {
                cacheDir = args[2];
                try
                {
                    if (!Directory.Exists(cacheDir))
                    {
                        Directory.CreateDirectory(cacheDir);
                    }
                } catch (Exception)
                {
                    Console.WriteLine("Could not create cache directory " + args[2]);
                    return;
                }
            }
            List<Track> tracks;
            try
            {
                tracks = Track.ReadTracksFromFile(gpxPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot read GPX file " + gpxPath + ": " + ex.Message);
                return;
            }
            double minLongitude = double.MaxValue;
            double maxLongitude = double.MinValue;
            double minLatitude = double.MaxValue;
            double maxLatitude = double.MinValue;

            foreach (var track in tracks)
            {
                double minLon = track.MinLon;
                double maxLon = track.MaxLon;
                double minLat = track.MinLat;
                double maxLat = track.MaxLat;
                if (minLon < minLongitude)
                    minLongitude = minLon;
                if (maxLon > maxLongitude)
                    maxLongitude = maxLon;
                if (minLat < minLatitude)
                    minLatitude = minLat;
                if (maxLat > maxLatitude)
                    maxLatitude = maxLat;
            }
            Tuple<int, int> minTile = Deg2num(maxLatitude, minLongitude, zoomLevel);
            Tuple<int, int> maxTile = Deg2num(minLatitude, maxLongitude, zoomLevel);
            Bitmap[,] bitmaps = new Bitmap[maxTile.Item2 - minTile.Item2 + 1, maxTile.Item1 - minTile.Item1 + 1];
            // load bitmaps from cache or OSM API.
            for (int y = 0; y < bitmaps.GetLength(0); y++)
            {
                for (int x = 0; x < bitmaps.GetLength(1); x++)
                {
                    string lonIdxStr = (x + minTile.Item1).ToString();
                    string latIdxStr = (y + minTile.Item2).ToString();
                    if (cacheDir != "")
                    {
                        string filepath = Path.Combine(cacheDir, zoomLevel.ToString() + "_" + lonIdxStr + "_" + latIdxStr + ".png");
                        if (File.Exists(filepath))
                            bitmaps[y, x] = new Bitmap(Bitmap.FromFile(filepath));
                        else
                        {
                            string url = "https://b.tile.openstreetmap.org/" + zoomLevel.ToString() + "/" + lonIdxStr + "/" + latIdxStr + ".png";
                            bitmaps[y, x] = new Bitmap(GetImageFromURL(url, email));
                            bitmaps[y, x].Save(filepath);
                        }
                    }
                    else
                    {
                        string url = "https://b.tile.openstreetmap.org/" + zoomLevel.ToString() + "/" + lonIdxStr + "/" + latIdxStr + ".png";
                        bitmaps[y, x] = new Bitmap(GetImageFromURL(url, email));
                    }
                }
            }


            // create combined bitmap
            int tileWidth = bitmaps[0, 0].Width;
            int tileHeight = bitmaps[0, 0].Height;
            Bitmap fullBmp = new Bitmap(tileWidth * bitmaps.GetLength(1), tileHeight * bitmaps.GetLength(0), bitmaps[0, 0].PixelFormat);
            Graphics graphics = Graphics.FromImage(fullBmp);
            for (int y = 0; y < bitmaps.GetLength(0); y++)
            {
                for (int x = 0; x < bitmaps.GetLength(1); x++)
                {
                    graphics.DrawImageUnscaled(bitmaps[y, x], x * tileWidth, y * tileHeight);
                }
            }

            // find boundaries
            double lonStart = Num2deg(minTile.Item1, minTile.Item2, zoomLevel).Item2;
            double lonStep = Num2deg(minTile.Item1+1, minTile.Item2, zoomLevel).Item2 - lonStart;
            double lonIncrement = lonStep / tileWidth;
            double[] latBounds = new double[bitmaps.GetLength(0) + 1];

            for (int i = 0; i < latBounds.Length; i++)
            {
                latBounds[i] = Num2deg(minTile.Item1, minTile.Item2 + i, zoomLevel).Item1;
            }

            // draw track
            Pen redPen = new Pen(Color.Red, 3);
            Pen yellowPen = new Pen(Color.Yellow, 1);
            foreach (var track in tracks)
            {
                int colPrev = int.MaxValue;
                int linePrev = int.MaxValue;
                foreach (var waypoint in track.Waypoints)
                {
                    int line = LatToLine(minTile.Item2, tileHeight, waypoint.Latitude, zoomLevel);
                    //line = fullBmp.Height - line;
                    int col = (int)((waypoint.Longitude - lonStart) / lonIncrement);
                    if (colPrev != int.MaxValue)
                    {
                        graphics.DrawLine(redPen, colPrev, linePrev, col, line);
                        graphics.DrawLine(yellowPen, colPrev, linePrev, col, line);
                    }
                    colPrev = col;
                    linePrev = line;
                }
            }
            fullBmp.Save(Path.Combine(Path.GetDirectoryName(gpxPath), Path.GetFileNameWithoutExtension(gpxPath) + zoomLevel.ToString("00") + ".png"));
        }

        static int LatToLine(int minTile, int tileHeight, double lat_deg, int zoom)
        {
            int line = 0;
            double lat_rad = lat_deg * Math.PI / 180.0;
            double n = Math.Pow(2, zoom);
            double tileNum = ((1.0 - Asinh(Math.Tan(lat_rad)) / Math.PI) / 2.0 * n);
            int tileIdx = (int)tileNum;
            line += (tileIdx - minTile) * tileHeight;
            

            tileNum -= (int)tileNum;
            line += (int)(tileNum * tileHeight);
            return line;
        }

        static Tuple<int, int> Deg2num(double lat_deg, double lon_deg, int zoom)
        {
            double lat_rad = lat_deg * Math.PI / 180.0;
            double n = Math.Pow(2, zoom);
            int xtile = (int)((lon_deg + 180.0) / 360.0 * n);
            int ytile = (int)((1.0 - Asinh(Math.Tan(lat_rad)) / Math.PI) / 2.0 * n);
            return new Tuple<int, int>(xtile, ytile);
        }

        static Tuple<double, double> Num2deg(int xtile, int ytile, int zoom)
        {
            double n = Math.Pow(2, zoom);
            double lon_deg = xtile / n * 360.0 - 180.0;
            double lat_rad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * ytile / n)));
            double lat_deg = lat_rad * 180.0 / Math.PI;
            return new Tuple<double, double>(lat_deg, lon_deg);
        }


        static double Asinh(double x)
        {
            return Math.Log(x + Math.Sqrt(1 + x * x));
        }

        /// <summary>
        /// Gets the image from URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        private static Image GetImageFromURL(string url, string email)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(url);
            httpWebRequest.UserAgent = "Private test program contact " + email;
            HttpWebResponse httpWebReponse = (HttpWebResponse)httpWebRequest.GetResponse();
            Stream stream = httpWebReponse.GetResponseStream();
            return Image.FromStream(stream);
        }
    }
}