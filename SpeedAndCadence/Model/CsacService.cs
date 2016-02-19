using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;
using Windows.Storage.Streams;

namespace SpeedAndCadence.Model
{
    public delegate void ValueChangedHandler(CsacMeasurement newMeasurement);

    public delegate void StatusChangedHandler(CsacServiceStatus newStatus, string errorMessage = "");

    public class CsacService : ICsacService
    {
        private GattDeviceService service;
        private GattCharacteristic characteristic;
        private CsacServiceStatus _status;
        private DeviceWatcher connectionWatcher;
        private DeviceInformation device;
        private bool alreadyTriedRePairing = false;
        private string deviceId;

        public CsacServiceStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
            }
        }

        private void SetStatus(CsacServiceStatus value, string errorMessage = "")
        {
            Status = value;

            if (StatusChanged != null)
                StatusChanged(Status, errorMessage);
        }

        public event ValueChangedHandler ValueChanged;

        public event StatusChangedHandler StatusChanged;

        public CsacService()
        {
            App.Current.Suspending += Current_Suspending;
        }

        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            this.Disconnect();
        }

        public async Task InitializeServiceAsync(DeviceInformation deviceToConnect)
        {
            device = deviceToConnect;

            try
            {
                service = await GattDeviceService.FromIdAsync(device.Id);
                if (service != null)
                {
                    SetStatus(CsacServiceStatus.Initialized);
                    await ConfigureServiceForNotificationsAsync();
                }
                else
                {
                    SetStatus(CsacServiceStatus.Error, "Access to the device is denied, because the application was not granted access, " +
                        "or the device is currently in use by another application.");
                }
            }
            catch (Exception e)
            {
                SetStatus(CsacServiceStatus.Error, "ERROR: Accessing your device failed." + Environment.NewLine + e.Message);
            }
        }

        private void connectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (service.Device.ConnectionStatus == BluetoothConnectionStatus.Connected)
                SetStatus(CsacServiceStatus.Connected);
            else
                SetStatus(CsacServiceStatus.AwaitingConnection);
        }

        private async Task ConfigureServiceForNotificationsAsync()
        {
            try
            {
                IReadOnlyList<GattCharacteristic> characteristicsList = service.GetCharacteristics(GattCharacteristicUuids.CscMeasurement);

                characteristic = characteristicsList[0];
                characteristic.ProtectionLevel = GattProtectionLevel.EncryptionRequired;

                characteristic.ValueChanged += characteristicValueChanged;

                GattCommunicationStatus status =
                    await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (service.Device.ConnectionStatus == BluetoothConnectionStatus.Connected)
                    SetStatus(CsacServiceStatus.Connected);
                else
                    SetStatus(CsacServiceStatus.AwaitingConnection);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR: {ex.Message}");
                if (alreadyTriedRePairing)
                {
                    Debug.WriteLine("Second error. Flagging it up...");
                    SetStatus(CsacServiceStatus.Error, "Unable to connect to the Csac Device!");
                }
                else
                {
                    SetStatus(CsacServiceStatus.Pairing);
                    Debug.WriteLine("First go. Trying to recycle the pairing...");
                    var result = await CyclePairing();
                };
            }
        }

        private async Task<bool> CyclePairing()
        {
            bool success = false;
            alreadyTriedRePairing = true;
            int retryCount = 0;

            // Get the device's MAC address to use when finding the device later...
            deviceId = getUniqueCode(device.Id);

            if (device.Pairing.IsPaired)
            {
                Debug.WriteLine("Unpairing...");
                SetStatus(CsacServiceStatus.Unpairing);
                var unpairingResult = await device.Pairing.UnpairAsync();
                if (unpairingResult.Status == DeviceUnpairingResultStatus.Unpaired)
                {
                    Debug.WriteLine("Device is unpaired. Now to re-pair...");
                    var pairingResult = await device.Pairing.PairAsync();
                    if (pairingResult.Status == DevicePairingResultStatus.Paired)
                    {
                        //StartDeviceConnectionWatcher();
                        do
                        {
                            var devices = await DeviceInformation.FindAllAsync(
                                        GattDeviceService.GetDeviceSelectorFromUuid(GattServiceUuids.CyclingSpeedAndCadence),
                                        new string[] { "System.Devices.ContainerId" }); Debug.WriteLine($"Finding device attempt {retryCount + 1}");
#if DEBUG
                            Debug.WriteLine($"DeviceId to find: {deviceId}");
                            Debug.WriteLine("Timestamp: " + DateTime.UtcNow.ToString());
                            Debug.WriteLine("Devices found:");
                            foreach (var item in devices)
                            {
                                Debug.WriteLine(item.Id);
                            }
#endif
                            if (devices.Count > 0)
                            {
                                var newDevice = devices.Where(dev => dev.Id.Contains(deviceId)).SingleOrDefault();
                                if (newDevice != null)
                                {
                                    Debug.WriteLine("Device found!");
                                    device = newDevice;
                                    success = true;
                                    await InitializeServiceAsync(device);
                                }
                            }
                            // Wait a bit to allow the device to propagate into the system...
                            await Task.Delay(2000);
                            retryCount++;
                        } while (!success /*&& retryCount < 5 */);
                    }
                }
            }
            return success;
        }

        private string getUniqueCode(string id)
        {
            // The device Id has four sections, separated by a # character. The device's unique MAC
            // address is in the second section.

            var segments = id.Split('#');
            return segments[1];
        }

        private void characteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            Debug.WriteLine("Value changed!");
            var data = new byte[args.CharacteristicValue.Length];
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);

            var value = createCsacMeasurement(data, args.Timestamp);
            if (ValueChanged != null)
                ValueChanged(value);
        }

        private CsacMeasurement createCsacMeasurement(byte[] data, DateTimeOffset timestamp)
        {
            var measurement = new CsacMeasurement();

            measurement.Timestamp = timestamp;

            var flags = (CsacMeasurementFlags)Enum.ToObject(typeof(CsacMeasurementFlags), data[0]);

            var offset = 1;

            // Wheel revolutions...
            if (flags.HasFlag(CsacMeasurementFlags.WheelRevolutionDataPresent))
            {
                // Cumulative wheel revolutions - 4 bytes littleendian
                var cumWRBytes = data.Skip(offset).Take(4).ToArray();
                if (!BitConverter.IsLittleEndian)
                    cumWRBytes.Reverse();

                measurement.CumulativeWheelRevolutions = BitConverter.ToUInt32(cumWRBytes, 0);

                offset += 4;

                // LastWheelEventTime - 2 bytes little-endian
                var lastWheelBytes = data.Skip(offset).Take(2).ToArray();
                if (!BitConverter.IsLittleEndian)
                    lastWheelBytes.Reverse();

                measurement.LastWheelEventTime = BitConverter.ToUInt16(lastWheelBytes, 0);
                offset += 2;
            }

            // Crank revolutions...
            if (flags.HasFlag(CsacMeasurementFlags.CrankRevolutionDataPresent))
            {
                // Cumulative crank revolutions - 4 bytes littleendian
                var cumCRBytes = data.Skip(offset).Take(2).ToArray();
                if (!BitConverter.IsLittleEndian)
                    cumCRBytes.Reverse();

                measurement.CumulativeCrankRevolutions = BitConverter.ToUInt16(cumCRBytes, 0);

                offset += 2;

                // LastWheelEventTime - 2 bytes little-endian
                var lastCrankBytes = data.Skip(offset).Take(2).ToArray();
                if (!BitConverter.IsLittleEndian)
                    lastCrankBytes.Reverse();

                measurement.LastCrankEventTime = BitConverter.ToUInt16(lastCrankBytes, 0);
            }

            return measurement;
        }

        public void Disconnect()
        {
            SetStatus(CsacServiceStatus.UnInitialized);

            if (service != null)
            {
                service.Dispose();
                service = null;
            }

            if (characteristic != null)
                characteristic = null;

            if (connectionWatcher != null)
            {
                if (connectionWatcher.Status == DeviceWatcherStatus.Started)
                    connectionWatcher.Stop();
                connectionWatcher = null;
            }

            if (device != null)
                device = null;

            alreadyTriedRePairing = false;
        }
    }

    internal class ConfigureForNotificationsResult
    {
        public string Message { get; internal set; }
        public bool Success { get; internal set; }

        public ConfigureForNotificationsResult(bool success = true, string message = "")
        {
            Success = success;
            Message = message;
        }
    }

    public class CsacServiceStatusChangedEventArgs : EventArgs
    {
        public CsacServiceStatusChangedEventArgs(CsacServiceStatus status, string errorMessage = "")
        {
            Status = status;
            ErrorMessage = errorMessage;
        }

        public CsacServiceStatus Status { get; private set; }
        public String ErrorMessage { get; private set; }
    }

    public class CsacServiceValueChangedEventArgs : EventArgs
    {
        public CsacMeasurement Measurement;

        public CsacServiceValueChangedEventArgs(CsacMeasurement value)
        {
            this.Measurement = value;
        }
    }

    public enum CsacServiceStatus
    {
        UnInitialized = 0,
        Initialized,
        AwaitingConnection,
        Connected,
        Error,
        RequiresPairing,
        Pairing,
        Unpairing,
        Paired
    }
}