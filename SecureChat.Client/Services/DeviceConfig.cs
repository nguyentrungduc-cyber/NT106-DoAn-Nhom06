using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using NAudio.CoreAudioApi;
using OpenCvSharp;

namespace SecureChat.Client.Services
{
    public static class DeviceConfig
    {
        private const string FileName = "speakerscamera.config";

        public static string OutputDevice { get; private set; } = "Default";
        public static string InputDevice { get; private set; } = "Default";
        public static bool UseSameDevicesForCalls { get; private set; } = true;
        public static string CallOutputDevice { get; private set; } = "Default";
        public static string CallInputDevice { get; private set; } = "Default";
        public static string CameraInputDevice { get; private set; } = "HD Webcam";
        public static bool AcceptCallsOnThisDevice { get; private set; } = true;

        public static void Load()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, FileName);
                if (!File.Exists(path)) return;

                var text = File.ReadAllText(path, Encoding.UTF8);
                var parts = text.Contains('\u001F') ? text.Split('\u001F') : text.Split('|');
                if (parts.Length >= 7)
                {
                    OutputDevice = parts[0];
                    InputDevice = parts[1];
                    if (bool.TryParse(parts[2], out var b1)) UseSameDevicesForCalls = b1;
                    CallOutputDevice = parts[3];
                    CallInputDevice = parts[4];
                    CameraInputDevice = parts[5];
                    if (bool.TryParse(parts[6], out var b2)) AcceptCallsOnThisDevice = b2;
                }
            }
            catch { }
        }

        public static int GetOutputDeviceNumber()
        {
            var name = UseSameDevicesForCalls ? OutputDevice : CallOutputDevice;
            return GetDeviceNumber(name, DataFlow.Render);
        }

        public static int GetInputDeviceNumber()
        {
            var name = UseSameDevicesForCalls ? InputDevice : CallInputDevice;
            return GetDeviceNumber(name, DataFlow.Capture);
        }

        public static int GetCameraIndex()
        {
            var name = CameraInputDevice;
            if (string.IsNullOrWhiteSpace(name) ||
                string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase))
                return 0;

            var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int friendlyCursor = 0;
            var friendlyNames = GetFriendlyCameraNames();

            for (int i = 0; i < 8; i++)
            {
                try
                {
                    using var probe = new VideoCapture(i);
                    if (!probe.IsOpened()) continue;

                    string label = friendlyCursor < friendlyNames.Count
                        ? friendlyNames[friendlyCursor++]
                        : $"Camera {i + 1}";

                    if (nameToIndex.ContainsKey(label))
                        label = $"{label} ({i + 1})";

                    nameToIndex[label] = i;
                    probe.Release();
                }
                catch { }
            }

            if (nameToIndex.TryGetValue(name, out var idx))
                return idx;

            var digits = new string(name.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var oneBased) && oneBased > 0)
                return Math.Max(0, oneBased - 1);

            return 0;
        }

        private static int GetDeviceNumber(string deviceName, DataFlow flow)
        {
            if (string.IsNullOrWhiteSpace(deviceName) ||
                string.Equals(deviceName, "Default", StringComparison.OrdinalIgnoreCase))
                return 0;

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
                for (int i = 0; i < devices.Count; i++)
                {
                    if (string.Equals(devices[i].FriendlyName, deviceName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            catch { }

            return 0;
        }

        private static List<string> GetFriendlyCameraNames()
        {
            var names = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'Image'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var nameVal = obj["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(nameVal) && !names.Contains(nameVal))
                        names.Add(nameVal);
                }
            }
            catch { }
            return names;
        }
    }
}
