using InfluxDB.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Object_Adapter
{
    public class DBwriterArgs : EventArgs
    {
        public string field { get; set; }
        public string value { get; set; }
        public string bucket { get; set; }
        public string org { get; set; }
        public InfluxDBClient dBClient { get; set; }
    }
}
