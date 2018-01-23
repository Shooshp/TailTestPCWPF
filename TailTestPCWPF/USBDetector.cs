using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace TailTestPCWPF
{
    public class UsbDetector
    {
        private readonly string _vid;
        private readonly string _pid;

        public bool IsConnected;

        public event EventHandler DeviceConnected;
        public event EventHandler DeviceDisconnected;
        public delegate void EventHandler();

        public UsbDetector(string vid, string pid)
        {
            _vid = vid;
            _pid = pid;

            IsConnected = false;

            var connectWatcher = new ManagementEventWatcher();
            var disconnectWatcher = new ManagementEventWatcher();

            var connectQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
            var disconnectQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");

            connectWatcher.EventArrived += ConnectArrived;
            disconnectWatcher.EventArrived += DisconnectArrived;

            connectWatcher.Query = connectQuery;
            disconnectWatcher.Query = disconnectQuery;

            IsConnected = InitialChek();

            connectWatcher.Start();
            disconnectWatcher.Start();
        }

        private void DisconnectArrived(object sender, EventArrivedEventArgs e)
        {
            if (IsConnected)
            {
                if (!InitialChek())
                {
                    IsConnected = false;
                    DeviceDisconnected?.Invoke();
                } 
            }
        }

        private void ConnectArrived(object sender, EventArrivedEventArgs e)
        {
            if (!IsConnected)
            {
                IsConnected = true;
                DeviceConnected?.Invoke();
            }
        }

        private bool InitialChek()
        {
            var devices = new List<DeviceInfo>();
            ManagementObjectCollection collection;

            using (var sercher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
                collection = sercher.Get();

            foreach (var device in collection)
            {
                var deviceInfo = new DeviceInfo
                {
                    DeviceId = (string)device.GetPropertyValue("DeviceID"),
                    PNPDeviceID = (string)device.GetPropertyValue("PNPDeviceID"),
                    Description = (string)device.GetPropertyValue("Description"),
                    Name = (string)device.GetPropertyValue("Name"),
                    Caption = (string)device.GetPropertyValue("Caption")
                };
                if (deviceInfo.DeviceId.Contains("VID"))
                {
                    var info = deviceInfo.DeviceId.Split('\\')[1].Split('&');
                    deviceInfo.Vid = info[0].Split('_')[1];
                    deviceInfo.Pid = info[1].Split('_')[1];
                }
                else
                {
                    deviceInfo.Vid = "";
                    deviceInfo.Pid = "";
                }
                devices.Add(deviceInfo);
            }

            collection.Dispose();

            var index = devices.FindIndex(device => device.Vid == _vid && device.Pid == _pid);

            if (index >= 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        class DeviceInfo
        {
            public string DeviceId;
            public string PNPDeviceID;
            public string Description;
            public string Name;
            public string Caption;

            public string Vid;
            public string Pid;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine(Caption + ">> VID: " + Vid + " PID: " + Pid);
                return sb.ToString();
            }
        }
    }
}
