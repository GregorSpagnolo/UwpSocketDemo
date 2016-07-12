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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Notifications;

namespace SocketActivityBackgroundTask
{
    public sealed class SocketActivityTask : Windows.ApplicationModel.Background.IBackgroundTask
    {
        private const string socketId = "SampleSocket";

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            try
            {
                var details = taskInstance.TriggerDetails as SocketActivityTriggerDetails;
                var socketInformation = details.SocketInformation;
                switch (details.Reason)
                {
                    case SocketActivityTriggerReason.SocketActivity:
                        var socket = socketInformation.StreamSocket;
                        DataReader dataReader = new DataReader(socket.InputStream);
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
                            ShowToast("Data received");
                        }


                        socket.TransferOwnership(socketInformation.Id);
                        break;
                    case SocketActivityTriggerReason.KeepAliveTimerExpired:
                        socket = socketInformation.StreamSocket;
                        DataWriter writer = new DataWriter(socket.OutputStream);
                        writer.WriteBytes(Encoding.UTF8.GetBytes("Keep alive"));
                        await writer.StoreAsync();
                        writer.DetachStream();
                        writer.Dispose();
                        socket.TransferOwnership(socketInformation.Id);
                        break;
                    case SocketActivityTriggerReason.SocketClosed:
                        socket = new StreamSocket();
                        socket.EnableTransferOwnership(taskInstance.Task.TaskId, SocketActivityConnectedStandbyAction.Wake);
                        if (ApplicationData.Current.LocalSettings.Values["hostname"] == null)
                        {
                            break;
                        }
                        var hostname = (String)ApplicationData.Current.LocalSettings.Values["hostname"];
                        var port = (String)ApplicationData.Current.LocalSettings.Values["port"];
                        await socket.ConnectAsync(new HostName(hostname), port);
                        socket.TransferOwnership(socketId);
                        break;
                    default:
                        break;
                }
                deferral.Complete();
            }
            catch (Exception exception)
            {
                ShowToast(exception.Message);
                deferral.Complete();
            }
        }

        public void ShowToast(string text)
        {
            var toastNotifier = ToastNotificationManager.CreateToastNotifier();
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var textNodes = toastXml.GetElementsByTagName("text");
            textNodes.First().AppendChild(toastXml.CreateTextNode(text));
            var toastNotification = new ToastNotification(toastXml);
            toastNotifier.Show(new ToastNotification(toastXml));
        }

    }
}
