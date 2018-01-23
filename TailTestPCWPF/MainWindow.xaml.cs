using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.IO.Ports;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Threading;

namespace TailTestPCWPF
{
    public partial class MainWindow : Window
    {
        public List<string> DisconnectList;
        public List<ShortCircut> ShortCircutsList;
        public List<Errors> ErrorsList;

        public MainWindow()
        {
            DisconnectList = new List<string>();
            ShortCircutsList = new List<ShortCircut>();
            ErrorsList = new List<Errors>();

            InitializeComponent();

            var serial = new Ftdi232(baudrate: 14400, parity: Parity.None, timeout: 2000);
            serial.OnDataReceived += SerialOnDataReceived;
            serial.PortIsConnected += PortReady;
            serial.PortIsDisconnected += PortIsNotReady;
            serial.BeginRecive += ReceivingData;

            if (serial.IsReady)
            {
                PortReady();
            }
            else
            {
                PortIsNotReady();
            }

            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                gridResults.ItemsSource = ErrorsList;
            }));
        }

        private void SerialOnDataReceived(object sender, DataEventArgs dataEventArgs)
        {
            var arguments = dataEventArgs.Data;
            var disconnectPattern = new Regex(@"DISCONNECT: (?<Chanel>\w+)");
            var shortCircutPattern = new Regex(@"SHORT CIRCUIT: (?<Chanel1>\w+) AND (?<Chanel2>\w+)");

            if (arguments.Any(s => s.Contains("STARTING SCANING OPERATION...")) && arguments.Any(s => s.Contains("SCANING COMPLITE")))
            {
                DisconnectList.Clear();
                ShortCircutsList.Clear();
                ErrorsList.Clear();

                foreach (var line in arguments)
                {
                    var match = disconnectPattern.Match(line);
                    if (match.Success)
                    {
                        DisconnectList.Add(match.Groups["Chanel"].Value);
                    }

                    match = shortCircutPattern.Match(line);
                    if (match.Success &&
                        !ShortCircutsList.Exists(a => a.First == match.Groups["Chanel2"].Value &&
                                                      a.Second == match.Groups["Chanel1"].Value))
                    {
                        ShortCircutsList.Add(new ShortCircut(match.Groups["Chanel1"].Value, match.Groups["Chanel2"].Value));
                    }
                }

                while (DisconnectList.Count > 0)
                {
                    var disconnect = DisconnectList.ElementAt(0);

                    if (ShortCircutsList.Exists(a => a.First == disconnect && DisconnectList.Exists(b => b == a.Second)))
                    {
                        var firstItemToRemove = ShortCircutsList.Single(a => a.First == disconnect);
                        var secondItemToRemove = DisconnectList.Single(b => b == firstItemToRemove.Second);

                        ErrorsList.Add(new Errors("Missmatch", firstItemToRemove.First, firstItemToRemove.Second));

                        ShortCircutsList.Remove(firstItemToRemove);
                        DisconnectList.Remove(secondItemToRemove);
                    }
                    else
                    {
                        if (ShortCircutsList.Exists(a => a.Second == disconnect && DisconnectList.Exists(b => b == a.First)))
                        {
                            var firstItemToRemove = ShortCircutsList.Single(a => a.Second == disconnect);
                            var secondItemToRemove = DisconnectList.Single(b => b == firstItemToRemove.First);

                            ErrorsList.Add(new Errors("Missmatch", firstItemToRemove.First, firstItemToRemove.Second));

                            ShortCircutsList.Remove(firstItemToRemove);
                            DisconnectList.Remove(secondItemToRemove);
                        }
                        else
                        {
                            ErrorsList.Add(new Errors("Disconnect", disconnect, ""));
                        }
                    }
                    DisconnectList.Remove(disconnect);
                }

                while (ShortCircutsList.Count > 0)
                {
                    var shortcircut = ShortCircutsList.ElementAt(0);

                    ErrorsList.Add(new Errors("ShortCircut", shortcircut.First, shortcircut.Second));
                    ShortCircutsList.Remove(shortcircut);
                }

                if (ErrorsList.Count == 0)
                {
                    Dispatcher.BeginInvoke(new ThreadStart(delegate
                    {
                        HeaderText.Text = "TAIL IS OK";
                    }));
                }
                else
                {
                    foreach (var error in ErrorsList)
                    {
                        Console.WriteLine(error.ToString());
                    }

                    if (ErrorsList.Count == 1)
                    {
                        Dispatcher.BeginInvoke(new ThreadStart(delegate
                        {
                            HeaderText.Text = "SINGLE ERROR";
                        }));
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(new ThreadStart(delegate
                        {
                            HeaderText.Text = ErrorsList.Count + " ERRORS";
                        }));
                    }
                    Dispatcher.BeginInvoke(new ThreadStart(delegate
                    {
                        ProgessBar.Visibility = Visibility.Hidden;
                        gridResults.Visibility = Visibility.Visible;

                        foreach (var column in gridResults.Columns)
                        {
                            column.Width = new DataGridLength(1.0, DataGridLengthUnitType.Auto);
                        }
                    }));
                }
            }
            else
            {
                Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    ProgessBar.Visibility = Visibility.Hidden;
                    gridResults.Visibility = Visibility.Hidden;
                    HeaderText.Text = "SORRY COMMUNICATION ERROR, TRY AGAIN";
                }));
            }
        }

        public void PortReady()
        {
            Dispatcher.BeginInvoke(new ThreadStart(delegate { UsbText.Text = "Device is ready!"; }));
        }

        public void PortIsNotReady()
        {
            Dispatcher.BeginInvoke(new ThreadStart(delegate { UsbText.Text = "Device disconnected!"; }));
        }

        public void ReceivingData()
        {
            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                HeaderText.Text = "SCANNING...";
                gridResults.Visibility = Visibility.Hidden;
                ProgessBar.Visibility = Visibility.Visible;
            }));
        }

        public class ShortCircut
        {
            public readonly string First;
            public readonly string Second;

            public ShortCircut(string first, string second)
            {
                First = first;
                Second = second;
            }
        }

        public class Errors
        {
            public string Type { get; }
            public string First { get; }
            public string Second { get; }

            public Errors(string type, string first, string second)
            {
                Type = type;
                First = first;
                Second = second;
            }

            public override string ToString()
            {
                var line = Type + ": " + First;
                if (!string.IsNullOrEmpty(Second))
                {
                    line += " and " + Second;
                }
                return line;
            }
        }
    }
}
