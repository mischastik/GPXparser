using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace GPXparser
{
    public class Waypoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; } = double.NaN;
        public double Speed { get; set; }
        public string Name { get; set; }
        public DateTime Time { get; set; }

        internal void Read(XmlReader reader)
        {
            string latStr = reader.GetAttribute("lat");
            if (latStr != null && latStr != "")
            {
                Latitude = Convert.ToDouble(latStr, CultureInfo.InvariantCulture);
            }
            string lonStr = reader.GetAttribute("lon");
            if (lonStr != null && lonStr != "")
            {
                Longitude = Convert.ToDouble(lonStr, CultureInfo.InvariantCulture);
            }
            while (reader.Read())
            {
                if (reader.IsStartElement())
                {
                    if (reader.Name == "ele")
                    {
                        reader.Read();
                        Elevation = Convert.ToDouble(reader.Value.Trim(), CultureInfo.InvariantCulture);
                    }
                    if (reader.Name == "time")
                    {
                        reader.Read();
                        string datetimestring = reader.Value.Trim();
                        Time = ParseDateTimeString(datetimestring);
                    }
                }
                if (reader.NodeType == XmlNodeType.EndElement && (reader.Name == "trkpt" || reader.Name == "rtept"))
                {
                    break;
                }
            }
        }

        private DateTime ParseDateTimeString(string datetimestring)
        {
            string[] elems = datetimestring.Split('-', 'T', ':', 'Z');
            if (elems.Length == 3)
            {
                return new DateTime(Convert.ToInt32(elems[0]), Convert.ToInt32(elems[1]), Convert.ToInt32(elems[2]));
            }
            return new DateTime(Convert.ToInt32(elems[0]),
                Convert.ToInt32(elems[1]), 
                Convert.ToInt32(elems[2]), 
                Convert.ToInt32(elems[3]),
                Convert.ToInt32(elems[4]),
                Convert.ToInt32(elems[5]));
        }
    }
}
