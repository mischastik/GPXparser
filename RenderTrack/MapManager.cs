using System;
using System.IO;
using System.Net.Http;
using System.Drawing;

using GPXparser;

namespace RenderTrack
{
    public class MapManager
    {
        private Bitmap mapBitmap = null;
        private int minLongitudeLine;
        private int tileHeight;
        private double n;
        private double lonStart;
        private double lonIncrement;
        private string cacheDir;
        private string email;
        private static readonly HttpClient httpClient = new HttpClient();


        public MapManager(string email, double minLongitude, double maxLongitude, double minLatitude, double maxLatitude, int zoomLevel, string tileCacheDirectory = "")
        {
            this.email = email;
            cacheDir = tileCacheDirectory;

            if (zoomLevel < 0 || zoomLevel >= 20)
            {
                throw new ArgumentException("Zoom level must be in [0;20].");
            }

            if (tileCacheDirectory != null && tileCacheDirectory != "")
            {
                try
                {
                    if (!Directory.Exists(tileCacheDirectory))
                    {
                        Directory.CreateDirectory(tileCacheDirectory);
                    }
                }
                catch (Exception)
                {
                    throw new ArgumentException("Could not create cache directory " + tileCacheDirectory);
                }
            }
            CreateMapBitmap(minLongitude, maxLongitude, minLatitude, maxLatitude, zoomLevel);
        }

        public void DrawTrack(Track track)
        {
            Pen redPen = new Pen(Color.FromArgb(63, Color.Red), 3);
            Pen yellowPen = new Pen(Color.FromArgb(127, Color.Red), 1);
            int colPrev = int.MaxValue;
            int linePrev = int.MaxValue;
            Graphics mapBitmapGraphics = Graphics.FromImage(mapBitmap);
            foreach (var waypoint in track.Waypoints)
            {
                int line = LatToLine(waypoint.Latitude);
                //line = fullBmp.Height - line;
                int col = (int)((waypoint.Longitude - lonStart) / lonIncrement);
                if (colPrev != int.MaxValue)
                {
                    mapBitmapGraphics.DrawLine(redPen, colPrev, linePrev, col, line);
                    mapBitmapGraphics.DrawLine(yellowPen, colPrev, linePrev, col, line);
                }
                colPrev = col;
                linePrev = line;
            }
        }

        public void SaveMap(string path)
        {
            mapBitmap.Save(path);
        }

        private void CreateMapBitmap(double minLongitude, double maxLongitude, double minLatitude, double maxLatitude, int zoomLevel)
        {
            n = Math.Pow(2, zoomLevel);
            Tuple<int, int> minTile = Deg2num(maxLatitude, minLongitude);
            minLongitudeLine = minTile.Item2;
            int tileWidth = 0;
            Tuple<int, int> maxTile = Deg2num(minLatitude, maxLongitude);
            int mapWidthInTiles = maxTile.Item1 - minTile.Item1 + 1;
            int mapHeightInTiles = maxTile.Item2 - minTile.Item2 + 1;
            Graphics mapBitmapGraphics = null;
            //Bitmap[,] bitmaps = new Bitmap[mapHeightInTiles, mapWidthInTiles];
            // load bitmaps from cache or OSM API.
            for (int y = 0; y < mapHeightInTiles; y++)
            {
                for (int x = 0; x < mapWidthInTiles; x++)
                {
                    Bitmap tileBitmap;
                    string lonIdxStr = (x + minTile.Item1).ToString();
                    string latIdxStr = (y + minTile.Item2).ToString();
                    if (cacheDir != "")
                    {
                        string filepath = Path.Combine(cacheDir, zoomLevel.ToString() + "_" + lonIdxStr + "_" + latIdxStr + ".png");
                        if (File.Exists(filepath))
                        {
                            tileBitmap = new Bitmap(Bitmap.FromFile(filepath));
                        }
                        else
                        {
                            string url = "https://b.tile.openstreetmap.org/" + zoomLevel.ToString() + "/" + lonIdxStr + "/" + latIdxStr + ".png";
                            tileBitmap = new Bitmap(GetImageFromURL(url, email));
                            tileBitmap.Save(filepath);
                        }
                    }
                    else
                    {
                        string url = "https://b.tile.openstreetmap.org/" + zoomLevel.ToString() + "/" + lonIdxStr + "/" + latIdxStr + ".png";
                        tileBitmap = new Bitmap(GetImageFromURL(url, email));
                    }
                    if (mapBitmap == null)
                    {
                        // create combined bitmap
                        tileWidth = tileBitmap.Width;
                        tileHeight = tileBitmap.Height;
                        mapBitmap = new Bitmap(tileWidth * mapWidthInTiles, tileHeight * mapHeightInTiles, tileBitmap.PixelFormat);
                        mapBitmapGraphics = Graphics.FromImage(mapBitmap);
                    }
                    mapBitmapGraphics.DrawImageUnscaled(tileBitmap, x * tileWidth, y * tileHeight);
                }
            }
            // find boundaries
            lonStart = Num2deg(minTile.Item1, minTile.Item2).Item2;
            double lonStep = Num2deg(minTile.Item1 + 1, minTile.Item2).Item2 - lonStart;
            lonIncrement = lonStep / tileWidth;
            double[] latBounds = new double[mapHeightInTiles + 1];

            for (int i = 0; i < latBounds.Length; i++)
            {
                latBounds[i] = Num2deg(minTile.Item1, minTile.Item2 + i).Item1;
            }
        }

        private Tuple<int, int> Deg2num(double lat_deg, double lon_deg)
        {
            double lat_rad = lat_deg * Math.PI / 180.0;
            int xtile = (int)((lon_deg + 180.0) / 360.0 * n);
            int ytile = (int)((1.0 - Asinh(Math.Tan(lat_rad)) / Math.PI) / 2.0 * n);
            return new Tuple<int, int>(xtile, ytile);
        }

        private Tuple<double, double> Num2deg(int xtile, int ytile)
        {
            double lon_deg = xtile / n * 360.0 - 180.0;
            double lat_rad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * ytile / n)));
            double lat_deg = lat_rad * 180.0 / Math.PI;
            return new Tuple<double, double>(lat_deg, lon_deg);
        }

        private int LatToLine(double lat_deg)
        {
            int line = 0;
            double lat_rad = lat_deg * Math.PI / 180.0;
            double tileNum = ((1.0 - Asinh(Math.Tan(lat_rad)) / Math.PI) / 2.0 * n);
            int tileIdx = (int)tileNum;
            line += (tileIdx - minLongitudeLine) * tileHeight;

            tileNum -= (int)tileNum;
            line += (int)(tileNum * tileHeight);
            return line;
        }

        private static double Asinh(double x)
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
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", "Private test program contact " + email);
                var responseTask = httpClient.SendAsync(request);
                responseTask.Wait();
                using (var response = responseTask.Result)
                {
                    response.EnsureSuccessStatusCode();
                    var streamTask = response.Content.ReadAsStreamAsync();
                    streamTask.Wait();
                    return Image.FromStream(streamTask.Result);
                }
            }
        }


    }
}