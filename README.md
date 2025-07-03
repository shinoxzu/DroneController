# DroneController

A CLI utility for controlling UAVs using the MAVLink protocol, built with the [Asv.Mavlink implementation](https://github.com/asv-soft/asv-mavlink).

## Installation

### Downloading

Download the latest binary for your platform from the [releases section](https://github.com/shinoxzu/DroneController/releases). 

### Building

You can also build program for yourself. You have to download dotnet SDK (we're targeting at least 8.x versions). The application requires no additional dependencies except some from NuGet, so you can publish it like this:
```
dotnet publish -c Release -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None
```
The final binary will be available at path `./DroneController/bin/Release/<your_sdk_version>/<your_platform>/publish/`

## Usage

Run the utility with the --help flag to get information about available options.

Generally, to start the utility, run it with the -p (port) and -h (host) flags, specifying the UAV's TCP connection details. For example:
```
./DroneController -h 127.0.0.1 -p 5760
```
The program uses default values from the example, so if they match your setup, you can run the utility without additional parameters.
