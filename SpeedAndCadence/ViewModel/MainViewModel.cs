using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Views;
using System;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace SpeedAndCadence.ViewModel
{
    public class MainViewModel : ViewModelBase, INavigable
    {
        private INavigationService _navigationService;
        private IDialogService _dialogService;

        public MainViewModel(INavigationService navigationService, IDialogService dialogService)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
        }

        /// <summary>
        /// The <see cref="DeviceList" /> property's name.
        /// </summary>
        public const string DeviceListPropertyName = "DeviceList";

        private DeviceInformationCollection _deviceList = null;

        /// <summary>
        /// Sets and gets the DeviceList property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public DeviceInformationCollection DeviceList
        {
            get
            {
                return _deviceList;
            }

            set
            {
                if (_deviceList == value)
                {
                    return;
                }

                _deviceList = value;
                RaisePropertyChanged(DeviceListPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="SelectedService" /> property's name.
        /// </summary>
        public const string SelectedServicePropertyName = "SelectedService";

        private DeviceInformation _selectedService = null;

        /// <summary>
        /// Sets and gets the SelectedService property.
        /// Changes to that property's value raise the PropertyChanged event.
        /// </summary>
        public DeviceInformation SelectedService
        {
            get
            {
                return _selectedService;
            }

            set
            {
                if (_selectedService == value)
                {
                    return;
                }

                _selectedService = value;
                RaisePropertyChanged(SelectedServicePropertyName);
                _navigationService.NavigateTo(ViewModelLocator.DetailPageKey, value);
            }
        }

        public async void Activate(object parameter)
        {
            SelectedService = null;
            var devices = await DeviceInformation.FindAllAsync(
                GattDeviceService.GetDeviceSelectorFromUuid(GattServiceUuids.CyclingSpeedAndCadence),
                new string[] { "System.Devices.ContainerId" });

            if (devices.Count > 0)
                DeviceList = devices;
            else
            {
                await _dialogService.ShowMessage("There are no Cycling Speed & Cadence devices paired.", "Error");
            }
        }

        public void Deactivate(object parameter)
        {
        }
    }
}