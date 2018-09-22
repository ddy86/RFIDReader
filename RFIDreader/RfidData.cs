using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFIDreader
{
    class RfidData
    {
        public byte readerID { get; set; }
        public byte value1 { get; set; }
        public byte value2 { get; set; }
        public uint tagID { get; set; }
        public bool bVoltLow { get; set; }
        public byte type { get; set; }
        public string strReaderIP { get; set; }
        public DateTime time { get; set; }

        public RfidData() { }

        public RfidData(uint tagID, byte readerID, DateTime time, string strReaderIP, byte type, bool bVoltLow, byte value1, byte value2)
        {
            this.tagID = tagID;
            this.readerID = readerID;
            this.time = time;
            this.strReaderIP = strReaderIP;
            this.type = type;
            this.bVoltLow = bVoltLow;
            this.value1 = value1;
            this.value2 = value2;
        }
    }
}
