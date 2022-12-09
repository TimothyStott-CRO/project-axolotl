using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fanuc_adapter.Connection
{
    internal class MachineConnection
    {
        private ushort _handle;
        private short _ret;
        private bool _isConnected;

        public ushort Handle { get { return _handle; } private set { } }
        public bool IsConnected { get { return _isConnected; } }
        public MachineConnection(string ip = null)
        {
            _isConnected = isConnectToMachine(ip);
        }

        public bool isConnectToMachine(string ip = null)
        {
            //Used for HSSB
            if (string.IsNullOrEmpty(ip)) { _ret = Focas1.cnc_allclibhndl(out _handle); }

            //Used for Ethernet/IP
            else { _ret = Focas1.cnc_allclibhndl3(ip, 8193, 6, out _handle); }

            if (_ret != Focas1.EW_OK)
            {
                return false;
            }
            return true;
        }

        public void Disconnect()
        {
            if (_handle == 0) return;

            Focas1.cnc_freelibhndl(_handle);
            _handle = 0;
        }
        
    }
}
