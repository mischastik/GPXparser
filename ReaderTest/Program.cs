using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPXparser;

namespace ReaderTest
{
    class Program
    {
        static void Main(string[] args)
        {
            List<Track> tracks = Track.ReadTracksFromFile(args[0]);
            foreach (var track in tracks)
            {
                Console.WriteLine(track.Name + ": ");
                Console.WriteLine(track.Statistics.ToString());
                Console.WriteLine();
            }
        }
    }
}
