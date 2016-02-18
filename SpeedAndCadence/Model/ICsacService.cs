using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace SpeedAndCadence.Model
{
    public interface ICsacService
    {
        CsacServiceStatus Status { get; set; }

        event StatusChangedHandler StatusChanged;

        event ValueChangedHandler ValueChanged;

        void Disconnect();

        Task InitializeServiceAsync(DeviceInformation device);
    }
}