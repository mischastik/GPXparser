using Gavaghan.Geodesy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace GPXparser
{
    /// <summary>
    /// Represents a GPS-track or -route.
    /// </summary>
    public class Track
    {
        #region Types
        /// <summary>
        /// Holds all kinds of statistics of a track.
        /// </summary>
        public class TrackStatistics
        {
            /// <summary>
            /// Amount of time where a speed threshold between waypoints is exceeded.
            /// </summary>
            public TimeSpan TimeInMotion { get; set; }
            /// <summary>
            /// Average speed of the track parts with motion.
            /// </summary>
            public double AverageSpeedInMotion { get; set; }
            /// <summary>
            /// Total average speed including breaks.
            /// </summary>
            public double AverageSpeed { get; set; }
            /// <summary>
            /// Track length in km.
            /// </summary>
            public double Length { get; set; }
            /// <summary>
            /// Total meters climbed.
            /// </summary>
            public double AbsoluteClimb { get; set; }
            /// <summary>
            /// Total meters decended.
            /// </summary>
            public double AbsoluteDescent { get; set; }
            /// <summary>
            /// Produces a string summarizing the track statistics.
            /// </summary>
            /// <returns>String summary.</returns>
            public override string ToString()
            {
                return $"{Length:0.00} km at avg. speed of {AverageSpeed:0.00} km/h ({AverageSpeedInMotion:0.00} km/h in motion), {AbsoluteClimb:0.0} m up and {AbsoluteDescent:0.0} m down.";
            }
        }
        #endregion

        #region Private Fields
        private TrackStatistics statisics;
        private double motionSpeedThreshold = 1.7;
        #endregion

        #region Public Properties
        /// <summary>
        /// All waypoints of the track.
        /// </summary>
        public List<Waypoint> Waypoints { get; set; } = new List<Waypoint>();
        /// <summary>
        /// True if object represents a route, false if it represents a track.
        /// </summary>
        public bool IsRoute { get; set; }
        /// <summary>
        /// Name of the track.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Minimum latitude value in track.
        /// </summary>
        public double MinLat
        {
            get
            {
                return Waypoints.Min(x => x.Latitude);
            }
        }
        /// <summary>
        /// Maximum latitude value in track.
        /// </summary>
        public double MaxLat
        {
            get
            {
                return Waypoints.Max(x => x.Latitude);
            }
        }
        /// <summary>
        /// Minimum longitude value in track.
        /// </summary>
        public double MinLon
        {
            get
            {
                return Waypoints.Min(x => x.Longitude);
            }
        }
        /// <summary>
        /// Maximum longitude value in track.
        /// </summary>
        public double MaxLon
        {
            get
            {
                return Waypoints.Max(x => x.Longitude);
            }
        }

        /// <summary>
        /// Track statistics.
        /// </summary>
        /// <remarks>Statistics are computed when the property is called for the first time. Needs to be reset if waypoints are changed.</remarks>
        public TrackStatistics Statistics
        {
            get
            {
                if (statisics == null)
                {
                    ComputeStatistics();
                }
                return statisics;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Reads the contents of a GPX-file.
        /// </summary>
        /// <param name="filename">Path to the GPX-file.</param>
        /// <returns>List of all tracks and routes contained in the file.</returns>
        /// <remarks>Supports only tracks and routes, everything else (e. g. single waypoints) is ignored and track-segments are joined into one track.</remarks>
        public static List<Track> ReadTracksFromFile(string filename)
        {
            List<Track> tracks = new List<Track>();
            using (XmlReader reader = XmlReader.Create(filename))
            {
                while (reader.Read())
                {
                    if (reader.IsStartElement())
                    {
                        if (reader.Name == "trk")
                        {
                            Track trk = new Track();
                            trk.IsRoute = false;
                            trk.Read(reader);
                            tracks.Add(trk);
                        }
                        else if (reader.Name == "rte")
                        {
                            Track trk = new Track();
                            trk.IsRoute = true;
                            trk.Read(reader);
                            tracks.Add(trk);
                        }
                        // ignore everything else for now, even metadata
                    }
                }
            }
            return tracks;
        }
        /// <summary>
        /// If waypoints are altered manually, previously computed track statistics might become invalid and you need to call this method.
        /// </summary>
        public void ResetStatistics()
        {
            this.statisics = null;
        }
        /// <summary>
        /// Split a track at all jumps where the distance between two waypoints exceeds a threshold.
        /// </summary>
        /// <param name="distanceThreshold">Distance threshold in </param>
        /// <returns>Splitted tracks.</returns>
        /// <remarks>Track names are preserved and a running number is appended.</remarks>
        public List<Track> SplitAtDistanceJumps(double distanceThreshold)
        {
            List<Track> splitTracks = new List<Track>();
            if (Waypoints.Count <= 1)
            {
                splitTracks.Add(this);
                return splitTracks;
            }

            Track currentTrack = new Track();
            splitTracks.Add(currentTrack);
            currentTrack.Name = this.Name + "_" + splitTracks.Count;
            // go though points and start a new track if distance treashold is exceeded.
            for (int i = 0; i < Waypoints.Count - 1; i++)
            {
                currentTrack.Waypoints.Add(Waypoints[i]);
                double distance = Waypoint.ComputeDistance(Waypoints[i], Waypoints[i + 1], out double elevationChange);
                if (distanceThreshold < distance)
                {
                    currentTrack = new Track();
                    splitTracks.Add(currentTrack);
                    currentTrack.Name = this.Name + "_" + splitTracks.Count;
                }
            }
            // take care of last point
            double lastDistance = Waypoint.ComputeDistance(Waypoints[Waypoints.Count - 2], Waypoints[Waypoints.Count - 1], out double lastElevationChange);
            if (lastDistance > distanceThreshold)
            {
                currentTrack = new Track();
                splitTracks.Add(currentTrack);
                currentTrack.Name = this.Name + "_" + splitTracks.Count;
            }
            currentTrack.Waypoints.Add(Waypoints[Waypoints.Count - 1]);

            return splitTracks;
        }
        #endregion

        #region Private Methods
        private void ComputeStatistics()
        {
            TrackStatistics trackStatistics = new TrackStatistics();
            for (int i = 0; i < Waypoints.Count - 1; i++)
            {
                Waypoint w1 = Waypoints[i];
                Waypoint w2 = Waypoints[i + 1];
                double distance = Waypoint.ComputeDistance(w1, w2, out double elevationChange);
                if (elevationChange < 0)
                {
                    trackStatistics.AbsoluteDescent -= elevationChange;
                }
                else
                {
                    trackStatistics.AbsoluteClimb += elevationChange;
                }
                TimeSpan timeBetween = w2.Time - w1.Time;
                w1.Speed = distance / (1000.0 * timeBetween.TotalHours);
                if (w1.Speed > motionSpeedThreshold)
                {
                    trackStatistics.TimeInMotion += timeBetween;
                }
                trackStatistics.Length += distance;
            }
            trackStatistics.Length /= 1000; // convert to km
            TimeSpan duration = Waypoints[Waypoints.Count - 1].Time - Waypoints[0].Time;
            trackStatistics.AverageSpeed = trackStatistics.Length / duration.TotalHours;
            trackStatistics.AverageSpeedInMotion = trackStatistics.Length / trackStatistics.TimeInMotion.TotalHours;
            statisics = trackStatistics;
        }

        private void Read(XmlReader reader)
        {
            Waypoints.Clear();
            while (reader.Read())
            {
                if (reader.IsStartElement())
                {
                    if (reader.Name == "trkpt" || reader.Name == "rtept")
                    {
                        Waypoint wp = new Waypoint();
                        wp.Read(reader);
                        Waypoints.Add(wp);
                    }
                    if (reader.Name == "name")
                    {
                        reader.Read();
                        this.Name = reader.Value.Trim();
                    }
                }
                if (reader.NodeType == XmlNodeType.EndElement && (reader.Name == "trk" || reader.Name == "rte"))
                {
                    break;
                }
            }
        }
        #endregion
    }
}
