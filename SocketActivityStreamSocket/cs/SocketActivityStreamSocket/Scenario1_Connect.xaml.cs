//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using SDKTemplate;
using Windows.Networking.Sockets;
using Windows.ApplicationModel.Background;
using System;
using Windows.Networking;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Foundation;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;
using System.Runtime.InteropServices.WindowsRuntime;

namespace SocketActivityStreamSocket
{
    public sealed partial class Scenario1_Connect : Page
    {
        private MainPage rootPage;
        private const string socketId = "SampleSocket";
        private StreamSocket socket = null;
        private IBackgroundTaskRegistration task = null;
        private const string port = "40404";
        private bool listening = false;
        private DataReader dataReader = null;

        public Scenario1_Connect()
        {
            this.InitializeComponent();
            App.HackThisSampleCode += ReleaseSocket;
        }

    

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            rootPage = MainPage.Current;
            try
            {
                foreach (var current in BackgroundTaskRegistration.AllTasks)
                {
                    if (current.Value.Name == "SocketActivityBackgroundTask")
                    {
                        task = current.Value;
                        break;
                    }
                }

                // If there is no task allready created, create a new one
                if (task == null)
                {
                    var socketTaskBuilder = new BackgroundTaskBuilder();
                    socketTaskBuilder.Name = "SocketActivityBackgroundTask";
                    socketTaskBuilder.TaskEntryPoint = "SocketActivityBackgroundTask.SocketActivityTask";
                    var trigger = new SocketActivityTrigger();
                    socketTaskBuilder.SetTrigger(trigger);
                    task = socketTaskBuilder.Register();
                }

                SocketActivityInformation socketInformation;
                if (SocketActivityInformation.AllSockets.TryGetValue(socketId, out socketInformation))
                {
                    // Application can take ownership of the socket and make any socket operation
                    // For sample it is just transfering it back.
                    socket = socketInformation.StreamSocket;
                    socket.TransferOwnership(socketId);
                    socket = null;
                    rootPage.NotifyUser("Connected. You may close the application", NotifyType.StatusMessage);
                    TargetServerTextBox.IsEnabled = false;
                    ConnectButton.IsEnabled = false;
                }

            }
            catch (Exception exception)
            {
                rootPage.NotifyUser(exception.Message, NotifyType.ErrorMessage);
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["hostname"] = TargetServerTextBox.Text;
            ApplicationData.Current.LocalSettings.Values["port"] = port;

            try
            {
                SocketActivityInformation socketInformation;
                if (!SocketActivityInformation.AllSockets.TryGetValue(socketId, out socketInformation))
                {
                    socket = new StreamSocket();
                    socket.Control.KeepAlive = true;
                    socket.Control.NoDelay = true;
                    socket.EnableTransferOwnership(task.TaskId, SocketActivityConnectedStandbyAction.Wake);
                    var targetServer = new HostName(TargetServerTextBox.Text);

                    /// issue happens using tls
                    await socket.ConnectAsync(targetServer, port, SocketProtectionLevel.Tls12);
                    /// not an issue 
                    //await socket.ConnectAsync(targetServer, port);
                    DataReceiver();
                }
                ConnectButton.IsEnabled = false;
                rootPage.NotifyUser("Connected. You may close the application", NotifyType.StatusMessage);
            }
            catch (Exception exception)
            {
                rootPage.NotifyUser(exception.Message, NotifyType.ErrorMessage);
            }
        }

        private async void DataReceiver()
        {
            listening = true;
            try
            {
                while (listening)
                {
                    dataReader = new DataReader(socket.InputStream);
                    dataReader.InputStreamOptions = InputStreamOptions.Partial;
                    // wait for first 4 bytes to get content size
                    UInt32 stringHeader = await dataReader.LoadAsync(4);
                    if (stringHeader != 0)
                    {
                        // getting content size
                        byte[] bytes = new byte[4];
                        dataReader.ReadBytes(bytes);
                        int length = BitConverter.ToInt32(bytes, 0);

                        // read avaliable content 
                        uint count = await dataReader.LoadAsync((uint)length);
                        byte[] receivedBytes = new byte[count];
                        dataReader.ReadBytes(receivedBytes);
                        // TO DO
                        // manipulate with received Bytes
                        Debug.WriteLine("Data received");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Exception][DataReceiver] " + ex.Message);
            }
        }

        private async void ReleaseSocket(object sender, EventArgs e)
        {
            if (socket != null)
            {
                listening = false;
                try
                {
                    dataReader?.DetachStream();
                    dataReader?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Exception][ReleaseSocket][DataReader] " + ex.Message);
                }
                await socket.CancelIOAsync();
                socket.TransferOwnership(socketId);
                socket.Dispose();
                socket = null;
            }
        }

    }
}
