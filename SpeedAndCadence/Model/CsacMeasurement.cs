using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeedAndCadence.Model
{
    public class CsacMeasurement
    {
        public UInt16 CumulativeCrankRevolutions;
        public UInt32 CumulativeWheelRevolutions;
        public UInt16 LastWheelEventTime;
        public UInt16 LastCrankEventTime;
        public DateTimeOffset Timestamp;
    }
}
