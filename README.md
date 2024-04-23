# RLForwarder Console Application

## Overview
`RLForwarder` (Receipt Lines Forwarder) is a .NET 4.7.2 console application designed to forward sales transaction data from a POS (Point of Sale) system to CCTV cameras or NVR (Network Video Recorder) systems utilizing the OPEN Intelligent Security Application Programming Interface (ISAPI). ISAPI is a text-based RESTful protocol that operates over HTTP, facilitating communication between various security devices and servers, such as cameras, DVRs, and NVRs.

## Features
- Captures and processes sales transaction data transmitted via a COM port.
- Transmits processed data to security devices using ISAPI over HTTP.
- Configurable settings via `App.config` for easy deployment adjustments.

## Prerequisites
- .NET Framework 4.7.2 installed on the host machine.
- A null modem emulator setup with configured COM port pairs.
- Access and credentials for the target CCTV Camera or NVR supporting ISAPI.

## Configuration
Edit the `App.config` file to tailor the following settings:
- `ComPort`: Specify the COM port for receiving data from the POS.
- `DeviceIP`: IP address of the target CCTV/NVR system.
- `ChannelID`: Channel ID for the ISAPI overlays.
- `ApiUsername` and `ApiPassword`: Authentication credentials for ISAPI.
- `MaxLinesToSend`: Sets the maximum number of transaction lines to send in each API request.

## Installation
1. Ensure the .NET Framework 4.7.2 is installed.
2. Set up the null modem emulator for the COM port pairing (https://com0com.sourceforge.net/).
3. Deploy the application files to the host system.
4. Update the `App.config` with the necessary configurations.

## Usage
Execute `RLForwarderConsole.exe` to launch the application. It will start logging both to the console and to the log files in the application directory, detailing operational status and any processing activities.

## Data Handling
The application processes incoming POS data, removing unnecessary elements and translating specific terms into a standard format before forwarding to the security system:
- Common POS terms are standardized for clarity and consistency.
- Data lines are formatted to meet the overlay requirements of ISAPI.
