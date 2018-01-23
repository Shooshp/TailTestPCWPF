using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Timers;
using Microsoft.Win32;
using Timer = System.Timers.Timer;

namespace TailTestPCWPF
{
    public class Ftdi232
    {
        public bool IsReady;
        private const string Vid = "0403";
        private const string Pid = "6001";

        private List<string> _comPorts;
        private readonly UsbDetector _detector;
        private readonly Timer _timer;

        private readonly List<string> _buffer;

        private SerialPort _port;
        private readonly int _baudrate;
        private readonly Parity _parity;
        private string _portname;

        public event DataEventHandler OnDataReceived;
        public delegate void DataEventHandler(object sender, DataEventArgs e);

        public event EventHandler PortIsConnected;
        public event EventHandler PortIsDisconnected;
        public event EventHandler BeginRecive;
        public delegate void EventHandler();

        public Ftdi232(int baudrate, Parity parity, int timeout)
        {
            IsReady = false;
            _baudrate = baudrate;
            _parity = parity;
            _portname = "";

            _comPorts = new List<string>();
            _buffer = new List<string>();
            _timer = new Timer(timeout) { AutoReset = false };
            _timer.Elapsed += TimerOnElapsed;

            _detector = new UsbDetector(vid: Vid, pid: Pid);
            if (_detector.IsConnected)
            {
                Connect();
            }
            _detector.DeviceConnected += Connect;
            _detector.DeviceDisconnected += Disconected;
        }

        public void SendLine(string line)
        {
            if (!string.IsNullOrEmpty(line) && IsReady)
            {
                _port.WriteLine(line);
            }
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (_buffer.Capacity >= 1)
            {
                OnDataReceived?.Invoke(sender: this, e: new DataEventArgs(_buffer));
            }
            _timer.Stop();
        }

        private void Connect()
        {
            GetPortNames(Vid, Pid);
            while (_detector.IsConnected && IsReady == false)
            {
                foreach (var portname in _comPorts)
                {
                    try
                    {
                        _port = new SerialPort(baudRate: _baudrate, parity: _parity, portName: portname);
                        if (_detector.IsConnected)
                        {
                            _port.Open();
                            _portname = portname;
                            Console.WriteLine(@"Connected with " + portname);
                            IsReady = true;
                            _port.DataReceived += _portDataReceived;
                            PortIsConnected?.Invoke();
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
        }

        private void _portDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_timer.Enabled)
            {
                _timer.Start();
                _buffer.Clear();
                BeginRecive?.Invoke();
            }
            _timer.Stop();
            _timer.Start();

            try
            {
                var data = _port.ReadLine();
                if (data.Contains("\r"))
                {
                    data = data.Replace("\r", "");
                }

                if (!string.IsNullOrEmpty(data))
                {
                    _buffer.Add(data);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

        }

        private void GetPortNames(String vid, String pid)
        {
            var pattern = $"^VID_{vid}.PID_{pid}";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var ports = new List<string>();

            var registryKey1 = Registry.LocalMachine;
            var registryKey2 = registryKey1.OpenSubKey("SYSTEM\\CurrentControlSet\\Enum");
            foreach (var subKeyName in registryKey2.GetSubKeyNames())
            {
                var registryKey3 = registryKey2.OpenSubKey(subKeyName);
                foreach (var entry in registryKey3.GetSubKeyNames())
                {
                    if (regex.Match(entry).Success)
                    {
                        var registryKey4 = registryKey3.OpenSubKey(entry);
                        foreach (var entry2 in registryKey4.GetSubKeyNames())
                        {
                            var registryKey5 = registryKey4.OpenSubKey(entry2);
                            var registryKey6 = registryKey5.OpenSubKey("Device Parameters");
                            var portanme = (string)registryKey6.GetValue("PortName");
                            if (!string.IsNullOrEmpty(portanme))
                            {
                                ports.Add(portanme);
                            }
                        }
                    }
                }
            }
            _comPorts.Clear();
            _comPorts = ports;
        }

        private void Disconected()
        {
            IsReady = false;
            _port.Dispose();
            GC.Collect();
            Console.WriteLine(@"Disconected from " + _portname);
            PortIsDisconnected?.Invoke();
        }
    }

    public class DataEventArgs : EventArgs
    {
        public List<string> Data { get; private set; }

        public DataEventArgs(List<string> data)
        {
            Data = data;
        }
    }
}
