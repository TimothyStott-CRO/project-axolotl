using Newtonsoft.Json;
using Object_Adapter;
using Object_Adapter.Settings;
using System.Diagnostics;

namespace Application
{
    class ObjectAdapter
    {
        static InformationGatherer _info;
        static GlobalSettings _settings;


        static void Main(string[] args)
        {

            if (File.Exists(@".\appsettings.json"))
            {
                _settings = JsonConvert.DeserializeObject<GlobalSettings>(File.ReadAllText(@".\appsettings.json"));

            }
            _info = new InformationGatherer(_settings.getSettings()); //create class to gather info

            _info.OnDetectedChange += _info.DetectedChange; //subscribe to change detection

            _info.updateAll(); //update information for first time

            while (_info._isConnected) //loop to keep updating. 
            {
                _info.updateAll();

                Thread.Sleep(1000);
            }
        }
    }
}
