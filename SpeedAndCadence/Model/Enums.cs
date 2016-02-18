using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeedAndCadence.Model
{
    [Flags]
    public enum CsacMeasurementFlags
    {
        WheelRevolutionDataPresent = 1,
        CrankRevolutionDataPresent = 2
    }
}
