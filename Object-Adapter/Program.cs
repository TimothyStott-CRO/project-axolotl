using System.Threading;
using System;
using System.Diagnostics;
using Object_Adapter;
using Microsoft.Extensions.Configuration;
using Object_Adapter.Settings;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

namespace Application
{
    class ObjectAdapter
    {
        static InformationGatherer _info = new InformationGatherer(new string[5]);
        static GlobalSettings _settings = new GlobalSettings();

        static void Main(string[] args)
        {

            //Build Settings for Machine and Database
            if (File.Exists(@".\appsettings.json")) 
            {
                try
                {
                    _settings = JsonConvert.DeserializeObject<GlobalSettings>(File.ReadAllText(@".\appsettings.json"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    _settings = new GlobalSettings();
                }             

            }

            //rough check for settings
            if(_settings.nullSettingsExist())
            {
                //write to debug
            }

            _info = new InformationGatherer(_settings.getSettings());

            _info.updateAll();

            for (int i = 0; i < 10; i++)
            {
                _info.testWrite();
            }

        }
    }
}
