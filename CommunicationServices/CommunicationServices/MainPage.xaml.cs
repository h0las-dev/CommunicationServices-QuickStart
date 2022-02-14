using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using Azure.WinRT.Communication;
using Azure.Communication.Calling;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.CognitiveServices.Speech;
using Azure;
using Azure.AI.TextAnalytics;
using Azure.Communication.Identity;
using Windows.ApplicationModel.DataTransfer;

namespace CallingQuickstart
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SpeechConfig config = SpeechConfig.FromSubscription("5a455886966742fdb92e913f72845c00", "eastus");
        private AzureKeyCredential azureSubscriptionKey = new AzureKeyCredential("9b4efa36752e43e88eedcf7e7e4894c4");
        private Uri endpoint = new Uri("https://emotions-test1.cognitiveservices.azure.com/");
        private string videoCallingToken;
        private string videoCallingId;

        CallClient callClient;
        CallAgent callAgent;
        Call call;
        DeviceManager deviceManager;
        LocalVideoStream[] localVideoStream;
        Dictionary<String, RemoteParticipant> remoteParticipantDictionary;

        public MainPage()
        {
            this.InitializeComponent();

            this.Loaded += MainPage_Loaded;

        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            var connectionString = "endpoint=https://videocalling-test.communication.azure.com/;accesskey=epRWpXC+VaYjc6izaVNkcA4IiFKkNMkhIffhqHvQsN9Bnz69n349PWWJMDYFWT5fA7yVAXjrMl+slnH1Lp4pcQ==";
            var client = new CommunicationIdentityClient(connectionString);

            var identityResponse = await client.CreateUserAsync();
            var identity = identityResponse.Value;

            // Issue an access token with the "voip" scope for an identity
            var tokenResponse = await client.GetTokenAsync(identity, scopes: new[] { CommunicationTokenScope.VoIP });

            // Get the token from the response
            videoCallingToken = tokenResponse.Value.Token;

            videoCallingId = identity.Id;
            YourNickTextBlock.Text = identity.Id;

            this.InitCallAgentAndDeviceManager();
            remoteParticipantDictionary = new Dictionary<string, RemoteParticipant>();
        }

        private async void InitCallAgentAndDeviceManager()
        {
            CallClient callClient = new CallClient();
            deviceManager = await callClient.GetDeviceManager();

            CommunicationTokenCredential token_credential = new CommunicationTokenCredential(videoCallingToken);

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

        private async void SpeechButton_Click(object sender, RoutedEventArgs e)
        {
            // Creates a speech synthesizer using the default speaker as audio output.
            using (var synthesizer = new SpeechSynthesizer(config))
            {
                SpeechEditBox.TextDocument.GetText(Windows.UI.Text.TextGetOptions.None, out var text);
   
                using (var result = await synthesizer.SpeakTextAsync(text))
                {
                }
            }
        }

        private async void RecognizeButton_Click(object sender, RoutedEventArgs e)
        {
            var client = new TextAnalyticsClient(endpoint, azureSubscriptionKey);
            var inputText = PhraseTextBox.Text;
            var analyzeResponse = client.AnalyzeSentiment(inputText);
            var documentSentiment = analyzeResponse.Value;

            SentimentTextBlock.Text = $"Sentiment: {documentSentiment.Sentiment.ToString()}";
            PositiveTextBlock.Text = $"Positive: {documentSentiment.ConfidenceScores.Positive.ToString("0.00")}";
            NegativeTextBlock.Text = $"Negative: {documentSentiment.ConfidenceScores.Negative.ToString("0.00")}";
            NeutralTextBlock.Text = $"Neutral: {documentSentiment.ConfidenceScores.Neutral.ToString("0.00")}";
        }

        private void TextBlock_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            DataPackage dataPackage = new DataPackage();
            // copy 
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            // or cut
            dataPackage.RequestedOperation = DataPackageOperation.Move;

            dataPackage.SetText(videoCallingId);

            Clipboard.SetContent(dataPackage);
        }
    }
}