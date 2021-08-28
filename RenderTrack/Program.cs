using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
                Console.WriteLine("Please create a file called \"email.txt\" that contains your e-mail address. It is required for the Open Street Map API.");
                return;
            }
            if (args.Length < 2 || args.Length > 3)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " [path to GPX file or folder] [zoomlevel] {tile cache directory}");
                return;
            }
            int zoomLevel = int.Parse(args[1]);
            List<string> gpxFilePaths = new List<string>();
            {
                string gpxPath = args[0];
                if (File.Exists(gpxPath))
                {
                    gpxFilePaths.Add(gpxPath);
                }
                else if (Directory.Exists(gpxPath))
                {
                    gpxFilePaths.AddRange(Directory.GetFiles(gpxPath));
                }
                else
                {
                    Console.WriteLine("Cannot find file or path " + gpxPath);
                    return;
                }

                if (gpxFilePaths.Count == 0)
                {
                    Console.WriteLine("Cannot find a gpx file in " + gpxPath);
                    return;
                }
            }
            string cacheDir = "";

            if (args.Length > 2)
            {
                cacheDir = args[2];
            }

            double minLongitude = double.MaxValue;
            double maxLongitude = double.MinValue;
            double minLatitude = double.MaxValue;
            double maxLatitude = double.MinValue;

            foreach (string gpxFile in gpxFilePaths)
            {
                List<Track> tracks;
                try
                {
                    tracks = Track.ReadTracksFromFile(gpxFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Cannot read GPX file " + gpxFile + ": " + ex.Message);
                    continue;
                }

                foreach (Track track in tracks)
                {
                    if (track.Waypoints.Count == 0)
                        continue;
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
            }
            MapManager mapManager;
            try
            {
                mapManager = new MapManager(email, minLongitude, maxLongitude, minLatitude, maxLatitude, zoomLevel, cacheDir);
            } catch (Exception ex)
            {
                Console.WriteLine("Error during set-up of map manager: " + ex.Message);
                return;                
            }

            // draw track
            foreach (string gpxFile in gpxFilePaths)
            {
                List<Track> tracks;
                try
                {
                    tracks = Track.ReadTracksFromFile(gpxFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Cannot read GPX file " + gpxFile + ": " + ex.Message);
                    continue;
                }
                for (int i = 0; i < tracks.Count; i++)
                {
                    Track track = tracks[i];
                    if (track.Waypoints.Count == 0)
                        continue;
                    List<Track> tracksSplit = track.SplitAtDistanceJumps(1000);
                    if (tracksSplit.Count > 1)
                    {
                        track = tracksSplit[0];
                        tracksSplit.RemoveAt(0);
                        tracks.AddRange(tracksSplit);
                    }
                    mapManager.DrawTrack(track);
                }
            }
            string baseName = "tracks";
            if (gpxFilePaths.Count == 1)
            {
                baseName = Path.GetFileNameWithoutExtension(gpxFilePaths[0]);
            }
            mapManager.SaveMap(Path.Combine(Path.GetDirectoryName(gpxFilePaths[0]), baseName + zoomLevel.ToString("00") + ".png"));
        }
    }
}