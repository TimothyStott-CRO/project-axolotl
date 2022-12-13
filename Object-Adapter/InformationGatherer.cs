using fanuc_adapter.Connection;
using fanuc_adapter.FanucFiles;
using InfluxDB.Client;
using InfluxDB.Client.Writes;
using Object_Adapter.IO;
using OnsrudFocasService;
using System.Diagnostics;

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
        public string[] machinePositions { get; set; }
        public string[] axisVoltages { get; set; }
        public double[] axisLoads { get; set; }
        public string[] motorTemps { get; set; }
        public string[] pulseEncoderTemps { get; set; }

        //IO Data

        private const string PATH = @"C:\FANUC\Symbols.txt";

        private List<InputOutput> iOs = new List<InputOutput>();

        //DB Info
        private InfluxDBClient _client = null;
        private DBwriterArgs writerArgs = new DBwriterArgs();
        private string _machineIP;
        private string _token;
        private string _bucket;
        private string _org;
        private string _host;





        /// <summary>
        /// </summary>
        /// <param name="args"></param>
        public InformationGatherer(string[] args)
        {
            try
            {
                parseArgs(args);
                buildClient();
                buildDBWriterArgs();
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


            updateAxisNames();

        }


        private void parseArgs(string[] args)
        {
            this._machineIP = args[0];
            this._token = args[1];
            this._bucket = args[2];
            this._org = args[3];
            this._host = args[4];
        }

        private void buildClient()
        {
            var options = new InfluxDBClientOptions(this._host);
            options.Bucket = this._bucket;
            options.Org = this._org;
            options.Token = this._token;
            this._client = new InfluxDBClient(options);
        }

        private void buildDBWriterArgs()
        {
            writerArgs.dBClient= this._client;
            writerArgs.bucket= this._bucket;
            writerArgs.org= this._org;
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

            if (_ret == Focas1.EW_OK)
            {
                this.status = FOCASdictionary.Status[_informationReader.run];

                writerArgs.field = "Status";
                writerArgs.value = this.status;
                OnChangeWrite(writerArgs);

                this.mode = FOCASdictionary.Modes[_informationReader.aut];

                writerArgs.field = "Mode";
                writerArgs.value = this.mode;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateAllCommandedData()
        {
            _ret = Focas1.cnc_rdcommand(_handle, -1, 1, ref numToRead, _commandReader);

            if (_ret==Focas1.EW_OK)
            {
                var commandsArray = _commandReader.FocasClassToArray(_commandReader.cmd0);

                foreach (var item in commandsArray)
                {

                    switch (Convert.ToChar(item.adrs))
                    {
                        case 'T':
                            this.activeToolNumber = item.cmd_val.ToString();
                            writerArgs.field = "ActiveTool";
                            writerArgs.value = this.activeToolNumber;
                            OnChangeWrite(writerArgs);

                            break;
                        case 'D':
                            this.activeRadiusOffset = item.cmd_val.ToString();
                            writerArgs.field = "ActiveRadiusOffset";
                            writerArgs.value = this.activeRadiusOffset;
                            OnChangeWrite(writerArgs);

                            break;
                        case 'F':
                            this.programmedFeedRate = item.cmd_val.ToString();
                            writerArgs.field = "ProgrammedFeedRate";
                            writerArgs.value = this.programmedFeedRate;
                            OnChangeWrite(writerArgs);
                            break;
                        case 'S':
                            this.programmedSpindleSpeed = item.cmd_val.ToString();
                            writerArgs.field = "ProgrammedSpindleSpeed";
                            writerArgs.value = this.programmedSpindleSpeed;
                            OnChangeWrite(writerArgs);
                            break;
                        case 'M':
                            this.activeMCodes.Add((int)item.cmd_val);
                            break;

                        default:
                            break;
                    }

                    foreach (var mcode in activeMCodes)
                    {
                        writerArgs.field = "ActiveMCode";
                        writerArgs.value = mcode.ToString();
                        OnChangeWrite(writerArgs);
                    }
                } 
            }
        }

        public void updateZShift()
        {
            var pmcReader = new Focas1.IODBPMC2();

            _ret = Focas1.pmc_rdpmcrng(_handle, 9, 2, 292, 295, 40, pmcReader);

            if (_ret==Focas1.EW_OK)
            {
                this.zShift = ((double)pmcReader.ldata[0] / 1000f).ToString();

                writerArgs.field = "ZShift";
                writerArgs.value = this.zShift;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateMessages() //needs tested with an actual message
        {
            short numOfMessages = short.MaxValue;

            Focas1.OPMSG3 messageReader = new Focas1.OPMSG3();

            _ret = Focas1.cnc_rdopmsg3(_handle, -1, ref numOfMessages, messageReader);

            if (_ret == Focas1.EW_OK)
            {
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
        }

        public void updateAlarms() //needs tested with an actual alarm
        {
            Focas1.ODBALMMSG2 alarmReader = new Focas1.ODBALMMSG2();
            try
            {
                short numberOfAlarms = short.MaxValue;

                _ret = Focas1.cnc_rdalmmsg2(_handle, -1, ref numberOfAlarms, alarmReader);

                if (_ret == Focas1.EW_OK)
                {
                    var alarmArray = alarmReader.FocasClassToArray(alarmReader.msg1);

                    this.alarms = new string[numberOfAlarms];


                    for (int i = 0; i < numberOfAlarms; i++) //alarm array returns objects up to 10 regardless of number of alarms so loop is dicated by the actual number of alarms
                    {
                        this.alarms[i] = alarmArray[i].alm_no + " : " + alarmArray[i].alm_msg.ToString();
                    } 
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

            if (_ret == Focas1.EW_OK)
            {
                var test = num_gcd;


                var codes = g_code.FocasClassToArray(g_code.gcd0);

                codes.ToList().ForEach(code =>
                {
                    if (code.code != "")
                        activeGCodes.Add(code.code);
                });

                this.activeGCodes = activeGCodes;

                foreach (var gcodes in activeGCodes)
                {
                    writerArgs.field = "ActiveGCode";
                    writerArgs.value = gcodes;
                    OnChangeWrite(writerArgs);
                } 
            }
        }

        public void readParametersFromArray()
        {
            //resetable part count, part count, machine hours, and spindle. 

            _ret = Focas1.cnc_rdparam_ext(_handle, parametersToBeRead, 4, _paramaterReaderMultiple);

            if (_ret == Focas1.EW_OK)
            {
                this.resetablePartCounter = _paramaterReaderMultiple.prm1.data.data1.prm_val.ToString();

                writerArgs.field = "ResetablePartCounter";
                writerArgs.value = this.resetablePartCounter;
                OnChangeWrite(writerArgs);

                this.partCounter = _paramaterReaderMultiple.prm2.data.data1.prm_val.ToString();

                writerArgs.field = "PartCounter";
                writerArgs.value = this.partCounter;
                OnChangeWrite(writerArgs);

                this.machineHours = _paramaterReaderMultiple.prm3.data.data1.prm_val.ToString();

                writerArgs.field = "MachineHours";
                writerArgs.value = this.machineHours;
                OnChangeWrite(writerArgs);

                this.spindleHours = _paramaterReaderMultiple.prm4.data.data1.prm_val.ToString();

                writerArgs.field = "SpindleHours";
                writerArgs.value = this.spindleHours;
                OnChangeWrite(writerArgs); 
            }

        }

        public void updateActualSpindleSpeed()
        {
            _ret = Focas1.cnc_acts(_handle, _actualDataReader);

            if (_ret == Focas1.EW_OK)
            {
                this.actualSpindleSpeed = _actualDataReader.data.ToString();

                writerArgs.field = "ActualSpindleSpeed";
                writerArgs.value = this.actualSpindleSpeed;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateSpindleSpeedOverride()
        {
            _ret = Focas1.pmc_rdpmcrng(_handle, 0, 0, (ushort)30, (ushort)30, 16, _pmcReader);

            if (_ret == Focas1.EW_OK)
            {
                this.spindleSpeedOverride = _pmcReader.cdata[0].ToString();

                writerArgs.field = "SpindleSpeedOverride";
                writerArgs.value = this.spindleSpeedOverride;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateFeedRateOverride()
        {
            _ret = Focas1.pmc_rdpmcrng(_handle, 0, 0, (ushort)12, (ushort)12, 16, _pmcReader);

            if (_ret == Focas1.EW_OK)
            {
                this.feedRateOverride = _pmcReader.cdata[0].ToString();

                writerArgs.field = "FeedRateOVerride";
                writerArgs.value = this.feedRateOverride;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateRapidRateOverride()
        {
            _ret = Focas1.pmc_rdpmcrng(_handle, 0, 0, (ushort)14, (ushort)14, 16, _pmcReader);

            if (_ret == Focas1.EW_OK)
            {
                switch (_pmcReader.cdata[0])
                {
                    case 0:
                        this.rapidOverride = "100";
                        writerArgs.field = "RapidRateOverride";
                        writerArgs.value = this.rapidOverride;
                        OnChangeWrite(writerArgs);
                        break;
                    case 1:
                        this.rapidOverride = "50";
                        writerArgs.field = "RapidRateOverride";
                        writerArgs.value = this.rapidOverride;
                        OnChangeWrite(writerArgs);
                        break;
                    case 2:
                        this.rapidOverride = "25";
                        writerArgs.field = "RapidRateOverride";
                        writerArgs.value = this.rapidOverride;
                        OnChangeWrite(writerArgs);
                        break;
                    case 3:
                        this.rapidOverride = "5";
                        writerArgs.field = "RapidRateOverride";
                        writerArgs.value = this.rapidOverride;
                        OnChangeWrite(writerArgs);
                        break;
                    default:
                        this.rapidOverride = "0";
                        writerArgs.field = "RapidRateOverride";
                        writerArgs.value = this.rapidOverride;
                        OnChangeWrite(writerArgs);
                        break;
                } 
            }
        }

        public void updateProgramName()
        {
            Focas1.ODBEXEPRG programReader = new Focas1.ODBEXEPRG();

            _ret = Focas1.cnc_exeprgname(_handle, programReader);

            if (_ret == Focas1.EW_OK)
            {
                this.programName = new string(programReader.name).Trim('\0');

                writerArgs.field = "Program Name";
                writerArgs.value = this.programName;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateBlockProgramData()
        {
            ushort length = 500;
            short blockNum = 0;
            var lineData = new char[499];

            _ret = Focas1.cnc_rdexecprog(_handle, ref length, out blockNum, lineData);

            if (_ret == Focas1.EW_OK)
            {
                var stringToEnque = new string(lineData);

                this.blockProgramData.Enqueue(stringToEnque);

                if (blockProgramData.Count > 2)
                {
                    blockProgramData.Dequeue();
                } 
            }
        }

        public void updateCurrentProgramLine()
        {
            ushort length = 600;
            short blockNum = 0;
            var lineData = new char[599];

            _ret = Focas1.cnc_rdexecprog(_handle, ref length, out blockNum, lineData);

            if (_ret == Focas1.EW_OK)
            {
                this.activeLine = new string(lineData).TrimEnd('\0').Split('\n').First();

                writerArgs.field = "CurrentProgramLine";
                writerArgs.value = this.activeLine;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateBlockNumber()
        {
            int block = 0;

            _ret = Focas1.cnc_rdblkcount(_handle, out block);

            if (_ret == Focas1.EW_OK)
            {
                this.activeBlockNumber = block.ToString();

                writerArgs.field = "BlockNumber";
                writerArgs.value = this.activeBlockNumber;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateSequenceNumber()
        {
            Focas1.ODBSEQ seqNum = new Focas1.ODBSEQ();

            _ret = Focas1.cnc_rdseqnum(_handle, seqNum);

            if (_ret == Focas1.EW_OK)
            {
                this.sequenceNumber = seqNum.data.ToString();

                writerArgs.field = "SequenceNumber";
                writerArgs.value = this.sequenceNumber;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateProgramNumber()
        {
            Focas1.ODBPRO reader = new Focas1.ODBPRO();

            _ret = Focas1.cnc_rdprgnum(_handle, reader);

            if (_ret == Focas1.EW_OK)
            {
                this.programNumber = reader.data.ToString();

                writerArgs.field = "ProgramNumber";
                writerArgs.value = this.programNumber;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateActualFeedRate()
        {
            _ret = Focas1.cnc_actf(_handle, _actualDataReader);

            if (_ret == Focas1.EW_OK)
            {
                this.actualFeedRate = _actualDataReader.data.ToString();

                writerArgs.field = "ActualFeedRate";
                writerArgs.value = this.actualFeedRate;
                OnChangeWrite(writerArgs); 
            }
        }

        public void updateAxisNames()
        {
            short ret = 0;
            short numberToGet = 32;  //This actually changes when past via reference but 32 is the max.

            Focas1.ODBEXAXISNAME namesReader = new Focas1.ODBEXAXISNAME();

            ret = Focas1.cnc_exaxisname(_handle, 0, ref numberToGet, namesReader);

            if (_ret == Focas1.EW_OK)
            {
                var namesArrayFromFocasClass = namesReader.FocasClassToArray(namesReader.ToString());

                string[] namesArray = new string[numberToGet];

                this._numOfAxes = numberToGet;

                for (int i = 0; i < numberToGet; i++)
                {
                    namesArray[i] = namesArrayFromFocasClass[i];
                    string axisNumberToWrite = "Axis" + i;
                    writerArgs.field = axisNumberToWrite;
                    writerArgs.value = namesArray[i];
                    OnChangeWrite(writerArgs);
                }


                this.axisNames = namesArray; 
            }
        }

        public void updateAbsolutePositions()
        {

            if (_numOfAxes == 0) return;

            this.absolutePositions = new string[_numOfAxes];

            for (int i = 0; i < _numOfAxes; i++)
            {
                _ret = Focas1.cnc_absolute2(_handle, (short)(i + 1), 12, _axisPositionReader); //axis IDs for this call are not 0 based.
                if (_ret == Focas1.EW_OK)
                {
                    this.absolutePositions[i] = _axisPositionReader.data[0].ToString();

                    string axisNumberToWrite = "Axis" + i + "AbsPos";
                    writerArgs.field = axisNumberToWrite;
                    writerArgs.value = this.absolutePositions[i];
                    OnChangeWrite(writerArgs); 
                }
            }


        }

        public void updateRelativePositions()
        {
            if (_numOfAxes == 0) return;

            this.relativePositions = new string[_numOfAxes];

            for (int i = 0; i < _numOfAxes; i++)
            {
                _ret = Focas1.cnc_relative2(_handle, (short)(i + 1), 12, _axisPositionReader); //axis IDs for this call are not 0 based.
                if (_ret == Focas1.EW_OK)
                {
                    this.relativePositions[i] = _axisPositionReader.data[0].ToString();

                    string axisNumberToWrite = "Axis" + i + "RelPos";
                    writerArgs.field = axisNumberToWrite;
                    writerArgs.value = this.relativePositions[i];
                    OnChangeWrite(writerArgs); 
                }
            }
        }

        public void updateMachinePostiions()
        {
            if (_numOfAxes == 0) return;

            this.machinePositions = new string[_numOfAxes];

            for (int i = 0; i < _numOfAxes; i++)
            {
                _ret = Focas1.cnc_machine(_handle, (short)(i + 1), 12, _axisPositionReader); //axis IDs for this call are not 0 based.
                if (_ret == Focas1.EW_OK)
                {
                    this.machinePositions[i] = _axisPositionReader.data[0].ToString();

                    string axisNumberToWrite = "Axis" + i + "MacPos";
                    writerArgs.field = axisNumberToWrite;
                    writerArgs.value = this.machinePositions[i];
                    OnChangeWrite(writerArgs); 
                }
            }
        }

        public void updateAxisVoltages()
        {
            if (_numOfAxes == 0) return;

            this.axisVoltages = new string[_numOfAxes];

            for (int i = 0; i < _numOfAxes; i++)
            {
                _ret = Focas1.cnc_diagnoss(_handle, 752, (short)(i + 1), (short)12, _diagnosticsReader);
                if (_ret == Focas1.EW_OK)
                {
                    this.axisVoltages[i] = _diagnosticsReader.ldata.ToString();

                    string axisNumberToWrite = "Axis" + i + "Voltage";
                    writerArgs.field = axisNumberToWrite;
                    writerArgs.value = this.axisVoltages[i];
                    OnChangeWrite(writerArgs); 
                }
            }


        }

        public void updateAxisLoads()
        {
            Focas1.ODBSVLOAD _motorLoadReader = new Focas1.ODBSVLOAD();

            short numOfAx = short.MaxValue;

            _ret = Focas1.cnc_rdsvmeter(_handle, ref numOfAx, _motorLoadReader);

            if (true)
            {
                this.axisLoads = new double[numOfAx];

                var loadsArray = _motorLoadReader.FocasClassToArray(_motorLoadReader.svload1);

                for (int i = 0; i < numOfAx; i++)
                {
                    this.axisLoads[i] = loadsArray[i].data;

                    string axisNumberToWrite = "Axis" + i + "Load";
                    writerArgs.field = axisNumberToWrite;
                    writerArgs.value = this.axisLoads[i].ToString();
                    OnChangeWrite(writerArgs);
                }

            }
        }

        public void updateMotorTemps()
        {
            if (_numOfAxes == 0) return;

            this.motorTemps = new string[this._numOfAxes];

            for (int i = 0; i < this._numOfAxes; i++)
            {
                _ret = Focas1.cnc_diagnoss(_handle, 308, (short)(i + 1), 12, _diagnosticsReader);
                if (_ret == Focas1.EW_OK)
                {
                    this.motorTemps[i] = _diagnosticsReader.ldata.ToString();


                    string axisNumberToWrite = "Axis" + i + "MotorTemp";
                    writerArgs.field = axisNumberToWrite;
                    writerArgs.value = this.motorTemps[i];
                    OnChangeWrite(writerArgs); 
                }
            }
        }

        public void updatePulseEncoderTemps()
        {
            if (_numOfAxes == 0) return;

            this.pulseEncoderTemps = new string[this._numOfAxes];

            for (int i = 0; i < this._numOfAxes; i++)
            {
                _ret = Focas1.cnc_diagnoss(_handle, 309, (short)(i + 1), 12, _diagnosticsReader);
                if (_ret == Focas1.EW_OK)
                {
                    this.pulseEncoderTemps[i] = _diagnosticsReader.ldata.ToString();

                    string axisNumberToWrite = "Axis" + i + "EncoderTemp";
                    writerArgs.field = axisNumberToWrite;
                    writerArgs.value = this.pulseEncoderTemps[i];
                    OnChangeWrite(writerArgs); 
                }
            }

        }

        public void updateAll()
        {
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

                        });
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

        public event EventHandler<DBwriterArgs> OnDetectedChange;

        public void DetectedChange(object sender, DBwriterArgs e)
        {
            var point = PointData.Measurement("MachineInformation").Field(e.field,e.value);
            using (var writeApi = e.dBClient.GetWriteApi())
            {
                writeApi.WritePoint(point,e.bucket,e.org);
            }
            Debug.WriteLine("Write attempted");
        }

        protected virtual void OnChangeWrite(DBwriterArgs e)
        {
            EventHandler<DBwriterArgs> handler = OnDetectedChange;
            if(handler != null)
            {
                handler(this, e);
            }
        }


    }
}
