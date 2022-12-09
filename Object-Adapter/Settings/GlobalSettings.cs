using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Object_Adapter.Settings
{
    [Serializable]
    public class GlobalSettings
    {
        public string MachineIP { get; set; }
        public string token { get; set; }
        public string bucket { get; set; }
        public string org { get; set; }
        public string host { get; set; }
        public bool isCurrent { get; set; }

        public bool nullSettingsExist()
        {
            if (token == null || bucket == null || org == null || host == null)
            {
                return true;
            }
            return false;
        }

        public string[] getSettings()
        {
            string[] ret = new string[5];

            if(MachineIP!= null) { ret[0] = MachineIP; }
            
            ret[1] = token;
            ret[2] = bucket;
            ret[3] = org;
            ret[4] = host;
            
            return ret;            
        }

    }
}
