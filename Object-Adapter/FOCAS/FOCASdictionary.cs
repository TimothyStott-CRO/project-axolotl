using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fanuc_adapter.FanucFiles
{
    public class FOCASdictionary
    {
        public static readonly Dictionary<int, string> Modes = new Dictionary<int, string>()
        {
            { 0, "MDI" },
            { 1, "AUTO" },
            { 3, "EDIT" },
            { 4, "HAND WHEEL" },
            { 5, "JOG" },
            { 6, "TEAH JOG" },
            { 7, "TEACH HANDLE" },
            { 8, "INC FEED" },
            { 9, "REFERENCE" },
            { 10, "REMOTE" }
        };

        public static readonly Dictionary<int, string> Status = new Dictionary<int, string>()
        {
            { 0, "IDLE" },
            { 1, "STOP" },
            { 2, "HOLD" },
            { 3, "RUNNING" },
            { 4, "MSTR" },
        };

        public static Dictionary<short, string> AlarmType = new Dictionary<short, string>()
        {
            { 0, "SW" },
            { 1, "PW" },
            { 2, "IO" },
            { 3, "PS" },
            { 4, "OT" },
            { 5, "OH" },
            { 6, "SV" },
            { 7, "SV" },
            { 8, "MC" },
            { 9, "SP" },
            { 10, "DS" },
            { 11, "IE" },
            { 12, "BG" },
            { 13, "SN" },
            { 15, "EX" },
            { 19, "PC" },
        };
    }
}
