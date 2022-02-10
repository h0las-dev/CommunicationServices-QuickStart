using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using Azure.WinRT.Communication;
using Azure.Communication.Calling;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CallingQuickstart
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            this.InitCallAgentAndDeviceManager();
            remoteParticipantDictionary = new Dictionary<string, RemoteParticipant>();
        }

        private async void InitCallAgentAndDeviceManager()
        {
            CallClient callClient = new CallClient();
            deviceManager = await callClient.GetDeviceManager();

            CommunicationTokenCredential token_credential = new CommunicationTokenCredential("eyJhbGciOiJSUzI1NiIsImtpZCI6IjEwNCIsIng1dCI6IlJDM0NPdTV6UENIWlVKaVBlclM0SUl4Szh3ZyIsInR5cCI6IkpXVCJ9.eyJza3lwZWlkIjoiYWNzOmJhOWRiZDA0LWNkNTgtNDM4YS05YWEzLWVmMjk4Yzg3MTEyOV8wMDAwMDAwZi04MzgwLTk0MmItOTJmZC04YjNhMGQwMDI0ZGUiLCJzY3AiOjE3OTIsImNzaSI6IjE2NDQ0OTYzNTEiLCJleHAiOjE2NDQ1ODI3NTEsImFjc1Njb3BlIjoidm9pcCIsInJlc291cmNlSWQiOiJiYTlkYmQwNC1jZDU4LTQzOGEtOWFhMy1lZjI5OGM4NzExMjkiLCJpYXQiOjE2NDQ0OTYzNTF9.AYUKd4xBUVeAOfOpRcwJj2SSY0cRxp1HdL9WXc2cwThdv7C44cqozgTuUS_o9BWFRkXRSswg9CmbbMRqMLSIlD4JIgA___aF8UVbe7QwNkd8ULG_nOys7sEfjwaqtMOsDG6vx1L6GhFZVj_lIYBtgbmWH_BAuuwiY-8aCKBY5NdqHvBMNrv7tK00U1dcWTUISWZatFybr6SHDIawQgtLxHCa3m3gyz3miTolQNcqYSA18TWUlT9Cd2wD6iqnfwMOEAJTsWBUA7VsVxcuRVbr3Rhp_6jK5GniO5gwlLjRyOqbpg-oZEFFTdTP8yl-mVr-9Q1IRQ7AdV8GMcKWKh6bsw");

            CallAgentOptions callAgentOptions = new CallAgentOptions()
            {
                DisplayName = "<DISPLAY_NAME>"
            };
            callAgent = await callClient.CreateCallAgent(token_credential, callAgentOptions);
            callAgent.OnCallsUpdated += Agent_OnCallsUpdated;
            callAgent.OnIncomingCall += Agent_OnIncomingCall;
        }

        private async void CallButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            Debug.Assert(deviceManager.Microphones.Count > 0);
            Debug.Assert(deviceManager.Speakers.Count > 0);
            Debug.Assert(deviceManager.Cameras.Count > 0);

            if (deviceManager.Cameras.Count > 0)
            {
                VideoDeviceInfo videoDeviceInfo = deviceManager.Cameras[0];
                localVideoStream = new LocalVideoStream[1];
                localVideoStream[0] = new LocalVideoStream(videoDeviceInfo);

                Uri localUri = await localVideoStream[0].CreateBindingAsync();

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    LocalVideo.Source = localUri;
                    LocalVideo.Play();
                });

            }

            StartCallOptions startCallOptions = new StartCallOptions();
            startCallOptions.VideoOptions = new VideoOptions(localVideoStream);
            ICommunicationIdentifier[] callees = new ICommunicationIdentifier[1]
            {
                new CommunicationUserIdentifier(CalleeTextBox.Text)
            };

            call = await callAgent.StartCallAsync(callees, startCallOptions);
        }

        private async void Agent_OnIncomingCall(object sender, IncomingCall incomingcall)
        {
            Debug.Assert(deviceManager.Microphones.Count > 0);
            Debug.Assert(deviceManager.Speakers.Count > 0);
            Debug.Assert(deviceManager.Cameras.Count > 0);

            if (deviceManager.Cameras.Count > 0)
            {
                VideoDeviceInfo videoDeviceInfo = deviceManager.Cameras[0];
                localVideoStream = new LocalVideoStream[1];
                localVideoStream[0] = new LocalVideoStream(videoDeviceInfo);

                Uri localUri = await localVideoStream[0].CreateBindingAsync();

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    LocalVideo.Source = localUri;
                    LocalVideo.Play();
                });

            }
            AcceptCallOptions acceptCallOptions = new AcceptCallOptions();
            acceptCallOptions.VideoOptions = new VideoOptions(localVideoStream);

            call = await incomingcall.AcceptAsync(acceptCallOptions);
        }

        private async void HangupButton_Click(object sender, RoutedEventArgs e)
        {
            var hangUpOptions = new HangUpOptions();
            await call.HangUpAsync(hangUpOptions);
        }

        private async void Agent_OnCallsUpdated(object sender, CallsUpdatedEventArgs args)
        {
            foreach (var call in args.AddedCalls)
            {
                foreach (var remoteParticipant in call.RemoteParticipants)
                {
                    String remoteParticipantMRI = remoteParticipant.Identifier.ToString();
                    if (!remoteParticipantDictionary.ContainsKey(remoteParticipantMRI))
                        remoteParticipantDictionary.Add(remoteParticipantMRI, remoteParticipant);

                    await AddVideoStreams(remoteParticipant.VideoStreams);
                    remoteParticipant.OnVideoStreamsUpdated += async (s, a) => await AddVideoStreams(a.AddedRemoteVideoStreams);
                }
                call.OnRemoteParticipantsUpdated += Call_OnRemoteParticipantsUpdated;
                call.OnStateChanged += Call_OnStateChanged;
            }
        }

        private async void Call_OnRemoteParticipantsUpdated(object sender, ParticipantsUpdatedEventArgs args)
        {
            foreach (var remoteParticipant in args.AddedParticipants)
            {
                String remoteParticipantMRI = remoteParticipant.Identifier.ToString();
                if (!remoteParticipantDictionary.ContainsKey(remoteParticipantMRI))
                    remoteParticipantDictionary.Add(remoteParticipantMRI, remoteParticipant);

                await AddVideoStreams(remoteParticipant.VideoStreams);
                remoteParticipant.OnVideoStreamsUpdated += async (s, a) => await AddVideoStreams(a.AddedRemoteVideoStreams);
            }
        }

        private async Task AddVideoStreams(IReadOnlyList<RemoteVideoStream> streams)
        {

            foreach (var remoteVideoStream in streams)
            {
                var remoteUri = await remoteVideoStream.CreateBindingAsync();

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    RemoteVideo.Source = remoteUri;
                    RemoteVideo.Play();
                });
                remoteVideoStream.Start();
            }
        }

        private async void Call_OnStateChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (((Call)sender).State)
            {
                case CallState.Disconnected:
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        LocalVideo.Source = null;
                        RemoteVideo.Source = null;
                    });
                    break;
                default:
                    Debug.WriteLine(((Call)sender).State);
                    break;
            }
        }

        CallClient callClient;
        CallAgent callAgent;
        Call call;
        DeviceManager deviceManager;
        LocalVideoStream[] localVideoStream;
        Dictionary<String, RemoteParticipant> remoteParticipantDictionary;
    }
}