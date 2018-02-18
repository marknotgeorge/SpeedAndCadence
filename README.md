# SpeedAndCadence
A Windows UWP app which demonstrates how to connect to a Bluetooth LE Cycling Speed and Cadence (CSAC) device. This is a sample app I wrote when I was developing a cycling app for Windows Phone.

##Key features
* Utilises the MVVM (Model-View-ViewModel) pattern via the [MVVMLight framework](http://www.mvvmlight.net/)
* The Bluetooth LE code is based upon the then-current Microsoft sample code, modified in two main ways:
  * The sample was for a HeartRate device, so the data structures were change to suit a CSAC device.
  * At the time, there was a problem with reconnecting to certain CSAC devices under Windows. The only solution was to unpair and re-pair the device to Windows. This could be done programmatically, but was a user interface nightmare. This was one of the reasons I stopped developing the cycling app.
* The app takes the raw data from the CSAC device and converts it into both wheel and crank RPM values (both instantaneous and averaged) and speed in km/h (assuming a wheel circumference of 2.4m).

