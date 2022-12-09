using fanuc_adapter.Connection;
using fanuc_adapter.FanucFiles;
using Object_Adapter.IO;
using OnsrudFocasService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core;
using InfluxDB.Client.Writes;



namespace Object_Adapter
{
    

    public class InformationGatherer
    {
        //necessary variables
        private ushort _handle;
        private short _ret;
        private short _numOfAxes;
                
        private Focas1.ODBST _informationReader = new Focas1.ODBST(); //used for Status and Mode Call

        private Focas1.ODBCMD _commandReader = new Focas1.ODBCMD(); //used for cnc_rdcommand
        private short numToRead = short.MaxValue; //used with _commandReader

        private Focas1.IODBPRM _paramaterReaderMultiple = new Focas1.IODBPRM(); //used to read parameters with cnc_rdparam_ext 

        private Focas1.IODBPRMNO parametersToBeRead = new Focas1.IODBPRMNO(); //used as the parameters to be read array
        private Focas1.ODBACT _actualDataReader = new Focas1.ODBACT(); //used with actual feed rate and actual spindle speed

        private Focas1.IODBPMC0 _pmcReader = new Focas1.IODBPMC0(); //used with pmc_rdpmcrng

        private Focas1.ODBAXIS _axisPositionReader = new Focas1.ODBAXIS(); //used with axis position calls

        private Focas1.ODBDGN_1 _diagnosticsReader = new Focas1.ODBDGN_1(); //used with reading diagnostics with cnc_diagnoss

        public bool _isConnected { get; set; }

        //Basic Machine Information
        public string mode { get; set; } 
        public string status { get; set; } 
        public string zShift { get; set; } 
        public string machineHours { get; set; } 
        public string[] messages { get; set; } 
        public string[] alarms { get; set; } 
        public string scale { get; set; }
        public List<string> activeGCodes { get; set; } = new List<string>(); 
        public List<int> activeMCodes { get; set; } = new List<int>();
        public string partCounter { get; set; } 
        public string resetablePartCounter { get; set; } 
        public string activeToolNumber { get; set; } 
        public string activeHeightOffset { get; set; } 
        public string activeRadiusOffset { get; set; } 



        //Spindle Information
        public string programmedSpindleSpeed { get; set; } 
        public string actualSpindleSpeed { get; set; } 
        public string spindleSpeedOverride { get; set; } 
        public string spindleHours { get; set; } 


        //Program Information
        public string programName { get; set; }
        public Queue<string> blockProgramData { get; set; } = new Queue<string>();
        public string activeLine { get; set; } 
        public string activeBlockNumber { get; set; } 
        public string sequenceNumber { get; set; } 
        public string programNumber { get; set; } 
        public string programmedFeedRate { get; set; } 
        public string actualFeedRate { get; set; } 
        public string feedRateOverride { get; set; } 
        public string rapidOverride { get; set; } 


        //Axis Information

        public string[] axisNames { get; set; } 
        public string[] absolutePositions { get; set; } 
        public string[] relativePositions { get; set; } 
        public string[] machinePostiions { get; set; } 
        public string[] axisVoltages { get; set; } 
        public double[] axisLoads { get; set; } 
        public string[] motorTemps { get; set; } 
        public string[] pulseEncoderTemps { get; set; }

        //IO Data

        private const string PATH = @"C:\FANUC\Symbols.txt";

        private List<InputOutput> iOs = new List<InputOutput>();

        //Database Info
        private InfluxDBClient _client = null;
        private string _machineIP;
        private string _token;
        private string _bucket;
        private string _org;
        private string _host;


        /// <summary>
        /// Requires Machine IP if not null, and all DB building information.
        /// args in order IP, token, bucket, org, host. 
        /// </summary>
        /// <param name="args"></param>
        public InformationGatherer(string[] args)
        {
            try
            {
                parseArgs(args);
                buildClient();
            }
            catch (Exception)
            {
                //write to log.
            }
            MachineConnection _conn = new MachineConnection(args[0]);

            if (_conn.IsConnected)
            {
                _isConnected = true;
                _handle = _conn.Handle;
                buildParametersArray();
                readSymbolsTxtFile();
            }
            else
            {
                Debug.WriteLine("Connection Failed");
            }


        }
        public void testWrite()
        {
            ///*
            var point = PointData.Measurement("testData").Tag("SerialNumber", "RetroM").Field("Status", status);
            using (var writeApi = this._client.GetWriteApi())
            {
                writeApi.WritePoint(point,this._bucket,this._org);
            }
            //*/
            /*
            var data = $"mem,serialnumber=retroM status={this.status}";
            using (var writeApi = this._client.GetWriteApi())
            {
                writeApi.WriteRecord(data, WritePrecision.Ns, this._bucket, this._org);
            }
            */
        }

        private void parseArgs(string[] args)
        {
            this._machineIP = args[0];
            this._token = args[1];
            this._bucket= args[2];
            this._org= args[3];
            this._host= args[4];
        }

        private void buildClient()
        {
            var options = new InfluxDBClientOptions(this._host);
            options.Bucket= this._bucket;
            options.Org= this._org;
            options.Token= this._token;
            this._client= new InfluxDBClient(options);
        }

        public void buildParametersArray()
        {
            this.parametersToBeRead = new Focas1.IODBPRMNO();

            parametersToBeRead.prm[0] = 6711; //resetable part count
            parametersToBeRead.prm[1] = 6712; //part count
            parametersToBeRead.prm[2] = 6750; //Machine Hours
            parametersToBeRead.prm[3] = 6756; //spindle Hours

        }

        public void updateStatusandMode()
        {
            _ret = Focas1.cnc_statinfo(_handle, _informationReader);

            this.status = FOCASdictionary.Status[_informationReader.run];

            this.mode = FOCASdictionary.Modes[_informationReader.aut];

        }

        public void updateAllCommandedData()
        {
            _ret = Focas1.cnc_rdcommand(_handle, -1, 1, ref numToRead, _commandReader);

            var commandsArray = _commandReader.FocasClassToArray(_commandReader.cmd0);

            foreach (var item in commandsArray)
            {
                switch (Convert.ToChar(item.adrs))
                {
                    case 'T':
                        this.activeToolNumber = item.cmd_val.ToString();
                        break;
                    case 'D':
                        this.activeRadiusOffset = item.cmd_val.ToString();
                        break;
                    case 'F':
                        this.programmedFeedRate = item.cmd_val.ToString();
                        break;
                    case 'S':
                        this.programmedSpindleSpeed = item.cmd_val.ToString();
                        break;
                    case 'M':
                        this.activeMCodes.Add((int)item.cmd_val);
                        break;

                    default:
                        break;
                }
            }
        }

        public void updateZShift()
        {
            var pmcReader = new Focas1.IODBPMC2();

            _ret = Focas1.pmc_rdpmcrng(_handle, 9,2,292,295,40,pmcReader);

            this.zShift = ((double)pmcReader.ldata[0] / 1000f).ToString();
        }

        public void updateMessages() //needs tested with an actual message
        {
            short numOfMessages = short.MaxValue;

            Focas1.OPMSG3 messageReader = new Focas1.OPMSG3();

            _ret = Focas1.cnc_rdopmsg3(_handle, -1, ref numOfMessages, messageReader);

            var messageArray = messageReader.FocasClassToArray(messageReader.msg1);

            this.messages = new string[messageArray.Length];

            for (int i = 0; i < messageArray.Length; i++)
            {
                if (messageArray[i].datano != -1)
                {
                    this.messages[i] = messageArray[i].data.Substring(0, messageArray[i].char_num - 1);
                }
            }
        }

        public void updateAlarms() //needs tested with an actual alarm
        {
            Focas1.ODBALMMSG2 alarmReader = new Focas1.ODBALMMSG2();
            try
            {
                short numberOfAlarms = short.MaxValue;

                _ret = Focas1.cnc_rdalmmsg2(_handle, -1, ref numberOfAlarms, alarmReader);

                var alarmArray = alarmReader.FocasClassToArray(alarmReader.msg1);

                this.alarms = new string[numberOfAlarms];


                for (int i = 0; i<numberOfAlarms; i++) //alarm array returns objects up to 10 regardless of number of alarms so loop is dicated by the actual number of alarms
                {
                    this.alarms[i] = alarmArray[i].alm_no + " : " + alarmArray[i].alm_msg.ToString();
                }

            }
            catch (AccessViolationException ex)
            {
                return;
            }

        } 

        public void updateGCodes()
        {
            List<string> activeGCodes = new List<string>();
            var g_code = new Focas1.ODBGCD();
            short num_gcd = 27;


            _ret = Focas1.cnc_rdgcode(_handle, -1, 1, ref num_gcd, g_code);

            var test = num_gcd;


            var codes = g_code.FocasClassToArray(g_code.gcd0);

            codes.ToList().ForEach(code =>
            {
                if (code.code != "")
                    activeGCodes.Add(code.code);
            });

            this.activeGCodes = activeGCodes;
        }

        public void readParametersFromArray()
        {
            //resetable part count, part count, machine hours, and spindle. 

            _ret = Focas1.cnc_rdparam_ext(_handle, parametersToBeRead, 4, _paramaterReaderMultiple);

            this.resetablePartCounter = _paramaterReaderMultiple.prm1.data.data1.prm_val.ToString();
            this.partCounter = _paramaterReaderMultiple.prm2.data.data1.prm_val.ToString();
            this.machineHours = _paramaterReaderMultiple.prm3.data.data1.prm_val.ToString();
            this.spindleHours = _paramaterReaderMultiple.prm4.data.data1.prm_val.ToString();


        }

        public void updateActualSpindleSpeed()
        {
            _ret = Focas1.cnc_acts(_handle,_actualDataReader);

            this.actualSpindleSpeed = _actualDataReader.data.ToString();
        }

        public void updateSpindleSpeedOverride()
        {
            _ret = Focas1.pmc_rdpmcrng(_handle, 0, 0, (ushort)30, (ushort)30, 16, _pmcReader);

            this.spindleSpeedOverride = _pmcReader.cdata[0].ToString();
        }

        public void updateFeedRateOverride()
        {
            _ret = Focas1.pmc_rdpmcrng(_handle, 0, 0, (ushort)12, (ushort)12, 16, _pmcReader);

            this.feedRateOverride = _pmcReader.cdata[0].ToString();
        }

        public void updateRapidRateOverride()
        {
            _ret = Focas1.pmc_rdpmcrng(_handle, 0, 0, (ushort)14, (ushort)14, 16, _pmcReader);

            switch (_pmcReader.cdata[0])
            {
                case 0:
                    this.rapidOverride = "100";
                    break;
                case 1:
                    this.rapidOverride = "50";
                    break;
                case 2:
                    this.rapidOverride = "25";
                    break;
                case 3:
                    this.rapidOverride = "5";
                    break;
                default:
                    this.rapidOverride = "0";
                    break;
            }
        }

        public void updateProgramName()
        {
            Focas1.ODBEXEPRG programReader = new Focas1.ODBEXEPRG();

            _ret = Focas1.cnc_exeprgname(_handle, programReader);

            this.programName = new string(programReader.name).Trim('\0');
        }

        public void updateBlockProgramData()
        {
            ushort length = 500;
            short blockNum = 0;
            var lineData = new char[499];

            _ret = Focas1.cnc_rdexecprog(_handle, ref length, out blockNum, lineData);

            var stringToEnque = new string(lineData);

            this.blockProgramData.Enqueue(stringToEnque);

            if (blockProgramData.Count>2)
            {
                blockProgramData.Dequeue();
            }
        }

        public void updateCurrentProgramLine()
        {
            ushort length = 600;
            short blockNum = 0;
            var lineData = new char[599];

            _ret = Focas1.cnc_rdexecprog(_handle, ref length, out blockNum, lineData);


            this.activeLine =  new string(lineData).TrimEnd('\0').Split('\n').First();
        }

        public void updateBlockNumber()
        {
            int block = 0;

            _ret = Focas1.cnc_rdblkcount(_handle, out block);

            this.activeBlockNumber = block.ToString();
        }

        public void updateSequenceNumber()
        {
            Focas1.ODBSEQ seqNum = new Focas1.ODBSEQ();

            _ret = Focas1.cnc_rdseqnum(_handle, seqNum);

            this.sequenceNumber = seqNum.data.ToString();
        }

        public void updateProgramNumber()
        {
            Focas1.ODBPRO reader = new Focas1.ODBPRO();

            _ret = Focas1.cnc_rdprgnum(_handle, reader);

            this.programNumber = reader.data.ToString();
        }

        public void updateActualFeedRate()
        {
            _ret = Focas1.cnc_actf(_handle, _actualDataReader);

            this.actualFeedRate = _actualDataReader.data.ToString();
        }

        public void updateAxisNames()
        {
            short ret = 0;
            short numberToGet = 32;  //This actually changes when past via reference but 32 is the max.

            Focas1.ODBEXAXISNAME namesReader = new Focas1.ODBEXAXISNAME();

            ret = Focas1.cnc_exaxisname(_handle, 0, ref numberToGet, namesReader);

            var namesArrayFromFocasClass = namesReader.FocasClassToArray(namesReader.ToString());

            string[] namesArray = new string[numberToGet];

            this._numOfAxes = numberToGet;

            for (int i = 0; i < numberToGet; i++)
            {
                namesArray[i] = namesArrayFromFocasClass[i];
            }

            
            this.axisNames = namesArray;

        }

        public void updateAbsolutePositions()
        {

            if (_numOfAxes == 0) return;

            this.absolutePositions = new string[_numOfAxes];

            for (int i = 0; i < _numOfAxes; i++)
            {
                _ret = Focas1.cnc_absolute2(_handle, (short)(i+1), 12, _axisPositionReader); //axis IDs for this call are not 0 based.
                this.absolutePositions[i] = _axisPositionReader.data[0].ToString();
            }

        }

        public void updateRelativePositions()
        {
            if (_numOfAxes == 0) return;

            this.relativePositions = new string[_numOfAxes];

            for (int i = 0; i < _numOfAxes; i++)
            {
                _ret = Focas1.cnc_relative2(_handle, (short)(i + 1), 12, _axisPositionReader); //axis IDs for this call are not 0 based.
                this.relativePositions[i] = _axisPositionReader.data[0].ToString();
            }


        }    
            
        public void updateMachinePostiions()
        {
            if (_numOfAxes == 0) return;

            this.machinePostiions = new string[_numOfAxes];

            for (int i = 0; i < _numOfAxes; i++)
            {
                _ret = Focas1.cnc_machine(_handle, (short)(i + 1), 12, _axisPositionReader); //axis IDs for this call are not 0 based.
                this.machinePostiions[i] = _axisPositionReader.data[0].ToString();
            }
        }

        public void updateAxisVoltages() 
        {
            if (_numOfAxes == 0) return;

            this.axisVoltages = new string[_numOfAxes];

            for (int i = 0; i < _numOfAxes; i++)
            {
                _ret = Focas1.cnc_diagnoss(_handle, 752, (short)(i+1), (short)12, _diagnosticsReader);
                this.axisVoltages[i] = _diagnosticsReader.ldata.ToString();          
            }


        }

        public void updateAxisLoads() 
        {
            Focas1.ODBSVLOAD _motorLoadReader = new Focas1.ODBSVLOAD();

            short numOfAx = short.MaxValue;

            _ret = Focas1.cnc_rdsvmeter(_handle, ref numOfAx, _motorLoadReader);

            this.axisLoads = new double[numOfAx];

            var loadsArray = _motorLoadReader.FocasClassToArray(_motorLoadReader.svload1);

            for (int i = 0; i < numOfAx; i++)
            {
                this.axisLoads[i] = loadsArray[i].data;
            }

        }

        public void updateMotorTemps()
        {
            if(_numOfAxes == 0)  return; 

            this.motorTemps = new string[this._numOfAxes];

            for (int i = 0; i < this._numOfAxes; i++)
            {
                _ret = Focas1.cnc_diagnoss(_handle, 308, (short)(i+1), 12, _diagnosticsReader);
                this.motorTemps[i] = _diagnosticsReader.ldata.ToString();
            }
        }

        public void updatePulseEncoderTemps()
        {
            if (_numOfAxes == 0) return;

            this.pulseEncoderTemps = new string[this._numOfAxes];

            for (int i = 0; i < this._numOfAxes; i++)
            {
                _ret = Focas1.cnc_diagnoss(_handle, 309, (short)(i + 1), 12, _diagnosticsReader);
                this.pulseEncoderTemps[i] = _diagnosticsReader.ldata.ToString();
            }

        }

        public void updateAll()
        {
            updateAxisNames();

            updateStatusandMode();

            updateAllCommandedData();

            updateZShift();

            updateMessages();

            updateAlarms();

            updateGCodes();

            readParametersFromArray();

            updateActualSpindleSpeed();

            updateSpindleSpeedOverride();

            updateFeedRateOverride();

            updateRapidRateOverride();

            updateProgramName();

            updateBlockProgramData();

            updateCurrentProgramLine();

            updateBlockNumber();

            updateSequenceNumber();

            updateProgramNumber();

            updateActualFeedRate();

            updateAbsolutePositions();

            updateRelativePositions();

            updateMachinePostiions();

            updateAxisVoltages();

            updateAxisLoads();

            updateMotorTemps();

            updatePulseEncoderTemps();

            updateAbsolutePositions();

            readIOData();
        }

        public void readSymbolsTxtFile()
        {
            var allIOs = System.IO.File.ReadAllLines(PATH);

            foreach (var io in allIOs)
            {
                var tempstringArray = io.Split(',');

                if (tempstringArray.Length > 1 && tempstringArray[0] != null && tempstringArray[0] != "")
                {
                    if (tempstringArray[1] != "" && tempstringArray[1].First() == 'X' || tempstringArray[1] != "" && tempstringArray[1].First() == 'Y')
                    {
                        iOs.Add(new InputOutput
                        {
                            Comment = tempstringArray[2],
                            Symbol = tempstringArray[1].First().ToString(),
                            Address = Convert.ToInt16(tempstringArray[1].Split(".")[0].Last().ToString()),
                            Bit = Convert.ToUInt16(tempstringArray[1].Split(".")[1].First().ToString()),
                            Value = false

                        }) ; 
                    }
                }
            }
        }

        public void readIOData()
        {
            foreach (var io in iOs)
            {
                _ret = Focas1.pmc_rdpmcrng(_handle, 3, 0, (ushort)io.Address, (ushort)io.Address, 16, _pmcReader);

                io.Value = _pmcReader.cdata[0].GetBit(io.Bit);
            }
        }

        
    }
}
