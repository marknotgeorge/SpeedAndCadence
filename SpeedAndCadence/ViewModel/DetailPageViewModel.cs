using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using GalaSoft.MvvmLight.Views;
using SpeedAndCadence.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Enumeration;

namespace SpeedAndCadence.ViewModel
{
    public class DetailPageViewModel : ViewModelBase, INavigable
    {
        private List<CsacMeasurement> datapoints;

        private ICsacService csacService;

        private List<double> averageWheelRPM;

        private List<double> averageCrankRPM;

        private int lastDatapoint;

        private DeviceInformation device;

        public DetailPageViewModel(IDialogService dialogService, INavigationService navigationService, ICsacService csacService)
        {
            this.dialogService = dialogService;
            this.navigationService = navigationService;
            this.csacService = csacService;
        }

        public async void Activate(object parameter)
        {
            device = parameter as DeviceInformation;

            datapoints = new List<CsacMeasurement>();

            averageCrankRPM = new List<double>();

            averageWheelRPM = new List<double>();

            csacService.ValueChanged += CsacService_ValueChanged;
            csacService.StatusChanged += csacServiceStatusChanged;

            await csacService.InitializeServiceAsync(device);
        }

        private void csacServiceStatusChanged(CsacServiceStatus status, string errorMessage)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(async () =>
            {
                if (status == CsacServiceStatus.Error)
                {
                    ServiceStatus = $"ERROR: {errorMessage}";
                    await dialogService.ShowError(errorMessage, "Error",
                        buttonText: "Ok",
                        afterHideCallback: () =>
                        {
                            navigationService.GoBack();
                        });
                }
                else
                    ServiceStatus = status.ToString();
            });
        }

        private void CsacService_ValueChanged(CsacMeasurement newMeasurement)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
           {
               processMeasurement(newMeasurement);
           });
        }

        private void processMeasurement(CsacMeasurement measurement)
        {
            datapoints.Add(measurement);

            MeasurementsReceived = datapoints.Count;

            if (datapoints.Count > 1)
            {
                var lastButOneMeasurement = datapoints[datapoints.Count - 2];

                if ((DateTimeOffset.Now - lastButOneMeasurement.Timestamp).TotalSeconds > 64)
                {
                    // The last data is too old. Reset the counters...
                    lastDatapoint = datapoints.Count - 1;
                    averageCrankRPM.Clear();
                    averageWheelRPM.Clear();
                }
                else
                {
                    var wheelRevsDelta = measurement.CumulativeWheelRevolutions - lastButOneMeasurement.CumulativeWheelRevolutions;
                    double timeDelta;
                    double wheelRPM;

                    if (measurement.LastWheelEventTime >= lastButOneMeasurement.LastWheelEventTime)
                        timeDelta = (measurement.LastWheelEventTime - lastButOneMeasurement.LastWheelEventTime) / 1024.0;
                    else
                        timeDelta = (65536 - measurement.LastWheelEventTime + lastButOneMeasurement.LastWheelEventTime) / 1024.0;

                    if (timeDelta > 0)
                        wheelRPM = wheelRevsDelta / (timeDelta / 60);
                    else wheelRPM = 0;
                    CycleSpeed = (wheelRPM * 2.4 * 60) / 1000;

                    averageWheelRPM.Add(wheelRPM);
                    InstantaneousWheelRPM = wheelRPM;
                    WheelRPMAverage = averageWheelRPM.Average();

                    var crankRevsDelta = measurement.CumulativeCrankRevolutions - lastButOneMeasurement.CumulativeCrankRevolutions;
                    double crankRPM;

                    if (measurement.LastCrankEventTime >= lastButOneMeasurement.LastCrankEventTime)
                        timeDelta = (measurement.LastCrankEventTime - lastButOneMeasurement.LastCrankEventTime) / 1024.0;
                    else
                        timeDelta = (65536 - measurement.LastCrankEventTime + lastButOneMeasurement.LastCrankEventTime) / 1024.0;

                    if (timeDelta > 0)
                        crankRPM = crankRevsDelta / (timeDelta / 60);
                    else crankRPM = 0;

                    averageCrankRPM.Add(crankRPM);
                    InstantaneousCrankRPM = crankRPM;
                    CrankRPMAverage = averageCrankRPM.Average();
                }
            }
        }

        /// <summary>
        /// The <see cref="ServiceStatus" /> property's name.
        /// </summary>
        public const string ServiceStatusPropertyName = "ServiceStatus";

        private string _serviceStatus = string.Empty;

        /// <summary>
        /// Sets and gets the ServiceStatus property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public string ServiceStatus
        {
            get
            {
                return _serviceStatus;
            }

            set
            {
                if (_serviceStatus == value)
                {
                    return;
                }

                _serviceStatus = value;
                RaisePropertyChanged(ServiceStatusPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="WheelRPMAverage" /> property's name.
        /// </summary>
        public const string WheelRPMAveragePropertyName = "WheelRPMAverage";

        private double _wheelRPMAverage = 0;

        /// <summary>
        /// Sets and gets the WheelRPMAverage property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public double WheelRPMAverage
        {
            get
            {
                return _wheelRPMAverage;
            }

            set
            {
                if (_wheelRPMAverage == value)
                {
                    return;
                }

                _wheelRPMAverage = value;
                RaisePropertyChanged(WheelRPMAveragePropertyName);
            }
        }

        /// <summary>
        /// The <see cref="InstantaneousWheelRPM" /> property's name.
        /// </summary>
        public const string InstantaneousWheelRPMPropertyName = "InstantaneousWheelRPM";

        private double _instantaneousWheelRPM = 0;

        /// <summary>
        /// Sets and gets the InstantaneousWheelRPM property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public double InstantaneousWheelRPM
        {
            get
            {
                return _instantaneousWheelRPM;
            }

            set
            {
                if (_instantaneousWheelRPM == value)
                {
                    return;
                }

                _instantaneousWheelRPM = value;
                RaisePropertyChanged(InstantaneousWheelRPMPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="InstantaneousCrankRPM" /> property's name.
        /// </summary>
        public const string InstantaneousCrankRPMPropertyName = "InstantaneousCrankRPM";

        private double _instantaneousCrankRPM = 0;

        /// <summary>
        /// Sets and gets the InstantaneousCrankRPM property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public double InstantaneousCrankRPM
        {
            get
            {
                return _instantaneousCrankRPM;
            }

            set
            {
                if (_instantaneousCrankRPM == value)
                {
                    return;
                }

                _instantaneousCrankRPM = value;
                RaisePropertyChanged(InstantaneousCrankRPMPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="CrankRPMAverage" /> property's name.
        /// </summary>
        public const string CrankRPMAveragePropertyName = "CrankRPMAverage";

        private double _crankRPMAverage = 0;

        /// <summary>
        /// Sets and gets the CrankRPMAverage property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public double CrankRPMAverage
        {
            get
            {
                return _crankRPMAverage;
            }

            set
            {
                if (_crankRPMAverage == value)
                {
                    return;
                }

                _crankRPMAverage = value;
                RaisePropertyChanged(CrankRPMAveragePropertyName);
            }
        }

        /// <summary>
        /// The <see cref="Output" /> property's name.
        /// </summary>
        public const string OutputPropertyName = "Output";

        private string _output = string.Empty;

        /// <summary>
        /// Sets and gets the Output property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public string Output
        {
            get
            {
                return _output;
            }

            set
            {
                if (_output == value)
                {
                    return;
                }

                _output = value;
                RaisePropertyChanged(OutputPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="CycleSpeed" /> property's name.
        /// </summary>
        public const string CycleSpeedPropertyName = "CycleSpeed";

        private double _cycleSpeed = 0;
        private IDialogService dialogService;
        private INavigationService navigationService;

        /// <summary>
        /// The <see cref="MeasurementsReceived" /> property's name.
        /// </summary>
        public const string MeasurementsReceivedPropertyName = "MeasurementsReceived";

        private int _measurementsReceived = 0;

        /// <summary>
        /// Sets and gets the MeasurementsReceived property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public int MeasurementsReceived
        {
            get
            {
                return _measurementsReceived;
            }

            set
            {
                if (_measurementsReceived == value)
                {
                    return;
                }

                _measurementsReceived = value;
                RaisePropertyChanged(MeasurementsReceivedPropertyName);
            }
        }

        /// <summary>
        /// Sets and gets the CycleSpeed property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public double CycleSpeed
        {
            get
            {
                return _cycleSpeed;
            }

            set
            {
                if (_cycleSpeed == value)
                {
                    return;
                }

                _cycleSpeed = value;
                RaisePropertyChanged(CycleSpeedPropertyName);
            }
        }

        private RelayCommand _goBack;

        /// <summary>
        /// Gets the GoBack.
        /// </summary>
        public RelayCommand GoBack
        {
            get
            {
                return _goBack
                    ?? (_goBack = new RelayCommand(ExecuteGoBack));
            }
        }

        private void ExecuteGoBack()
        {
            navigationService.GoBack();
        }

        public void Deactivate(object parameter)
        {
            csacService.Disconnect();
        }
    }
}