using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Newtonsoft.Json;
using SpeechCognitiveServices.SpeechIdentifier;
using static System.Console;
using static System.IO.Path;
using RecognitionResult = SpeechCognitiveServices.SpeechIdentifier.RecognitionResult;
using WebSocket = WebSocketSharp.WebSocket;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using SpeechCognitiveServices.Properties;
using UiPathWorkflow;
using WebSocketSharp;

namespace SpeechCognitiveServices
{
    internal class Program
    {
        private static readonly Settings Settings = Settings.Default;

        private static readonly string OutputFolder = Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "MeetingMinutes");

        private static AudioWriter _audioWriter;
        private static UiPathHttpClient _uiPathHttpClient;

        private static async Task Main()
        {
            _uiPathHttpClient = new UiPathHttpClient(Settings.UiPathUserName, Settings.UiPathPassword,Settings.UiPathTenancyName);
            // DirectLine requires at least TLS 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            WriteLine("== Initializing direct line connection ==");
            var tokenResponse = new DirectLineClient(Settings.BotSecret).Tokens
                .GenerateTokenForNewConversation();

            var conversation = await InitializeDirectLine(tokenResponse);
            var webSocketClient = new WebSocket(conversation.StreamUrl)
            {
                SslConfiguration = {EnabledSslProtocols = SslProtocols.Tls12}

            };
            webSocketClient.OnOpen += (sender, args) => WriteLine("Connected to direct line.");
            webSocketClient.OnMessage += async (sender, eventArgs) => { await WebSocketClientOnOnMessage(eventArgs); };
            webSocketClient.Connect();
            Read();
        }

        private static async Task<Conversation> InitializeDirectLine(Conversation tokenResponse)
        {
            var directLineClient = new DirectLineClient(tokenResponse.Token);
            var conversation = directLineClient.Conversations.StartConversation();
            var userMessage = new Activity
            {
                From = new ChannelAccount(id: "1", name: "AudioRecorder"),
                Text = "Connect to Azure Bot",
                Type = ActivityTypes.Message
            };
            await directLineClient.Conversations.PostActivityAsync(conversation.ConversationId, userMessage);
            WriteLine("Initializing socket");
            return conversation;
        }

        private static async Task CallCognitiveServices(MeetingMinutesUiPathArguments meetingMinutesUiPathArguments)
        {
            try
            {
                var wavFileCount = _audioWriter.WavFileCount;
                _audioWriter = new AudioWriter(OutputFolder);
                meetingMinutesUiPathArguments.KeyPhrasesFilePath = $"{wavFileCount}.keyphrases.txt";
                meetingMinutesUiPathArguments.MinutesFilePath = $"{wavFileCount}.minutes.txt";
                meetingMinutesUiPathArguments.SentimentFilePath = $"{wavFileCount}.sentiment.txt";
                meetingMinutesUiPathArguments.TranscribedFilePath = $"{wavFileCount}.transcribed.txt";
                var fullTranscribeSpeechPath = Combine(OutputFolder, meetingMinutesUiPathArguments.TranscribedFilePath);
                var meetingMinutesFilePath = Combine(OutputFolder, meetingMinutesUiPathArguments.MinutesFilePath);
                var keyExtractionFilePath = Combine(OutputFolder, meetingMinutesUiPathArguments.KeyPhrasesFilePath);
                var sentimentAnalysisFilePath = Combine(OutputFolder, meetingMinutesUiPathArguments.SentimentFilePath);

                var config = SpeechConfig.FromSubscription(Settings.SpeechServiceSubscriptionKey, "eastus");
                config.SpeechRecognitionLanguage = "en-US";

                WriteLine("===== Initializing Speech Identifier =====");
                var speakerIdentifierHttpRequests = new List<Task>();
                SpeechIdentifier.SpeechIdentifier speechIdentifier =
                    new SpeechIdentifier.SpeechIdentifier(_audioWriter.OutputFilePath, speakerIdentifierHttpRequests);
                await speechIdentifier.IdentifySpeakers();
                Task.WaitAll(speakerIdentifierHttpRequests.ToArray());
                WriteLine("===== Done Speaker Identification =====");
                WriteLine();
                WriteLine("===== Transcribing Identified Speakers =====");
                if (await TranscribeIdentifiedSpeakers(meetingMinutesFilePath, speechIdentifier,
                    _audioWriter.OutputFilePath,
                    config)
                ) return;
                WriteLine("===== Done Transcribing Identified Speakers =====");
                WriteLine();
                WriteLine("===== Transcribing entire audio =====");
                using (var fullTranscribeSpeechWriter = new StreamWriter(fullTranscribeSpeechPath))
                {
                    var transcriber = new Transcriber(_audioWriter.OutputFilePath, fullTranscribeSpeechWriter);
                    await transcriber.TranscribeSpeechFromWavFileInput(config);
                }

                WriteLine("===== Done Transcribing entire audio =====");
                WriteLine();
                WriteLine("===== Initializing Key Extraction and Sentiment Analysis =====");
                var textAnalytics =
                    new TextAnalytics.TextAnalytics(meetingMinutesFilePath, fullTranscribeSpeechPath);

                textAnalytics.KeyExtraction(keyExtractionFilePath);
                textAnalytics.SentimentAnalysis(sentimentAnalysisFilePath);
                WriteLine("===== Done Key Extraction and Sentiment Analysis =====");
                WriteLine();
            }
            catch (Exception e)
            {
                WriteLine(e);
                ReadLine();
                throw;
            }
        }

        private static async Task UploadToAzureBlob(MeetingMinutesUiPathArguments meetingMinutesUiPathArguments)
        {
            var storageConnectionString = Settings.AzureStorageConnectionString;

            if (CloudStorageAccount.TryParse(storageConnectionString, out var storageAccount))
            {
                WriteLine("== Creating Cloud Blob Client ==");
                var cloudBlobClient = storageAccount.CreateCloudBlobClient();
                var cloudBlobContainer = cloudBlobClient.GetContainerReference("cognitiveserviceoutput");

                await cloudBlobContainer.CreateIfNotExistsAsync();

                var permissions = new BlobContainerPermissions
                    {PublicAccess = BlobContainerPublicAccessType.Blob};
                await cloudBlobContainer.SetPermissionsAsync(permissions);

                var outputFilesPath = Directory.GetFiles(OutputFolder, "*.txt", SearchOption.TopDirectoryOnly);
                foreach (var outputFilePath in outputFilesPath.Where(x =>
                    x.Contains(_audioWriter.WavFileCount.ToString())))
                {
                    var fileName = GetFileName(outputFilePath);
                    WriteLine($"== Uploading {fileName} to Azure Blob (cognitiveservicesoutput)");
                    var cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);
                    await cloudBlockBlob.UploadFromFileAsync(outputFilePath);
                }

                WriteLine("== Done uploading files to Azure Blob ==");
                WriteLine("List blobs in container.");
                BlobContinuationToken blobContinuationToken = null;
                do
                {
                    var results = await cloudBlobContainer.ListBlobsSegmentedAsync(null, blobContinuationToken);
                    // Get the value of the continuation token returned by the listing call.
                    blobContinuationToken = results.ContinuationToken;
                    var cloudBlockBlobs = results.Results.OfType<CloudBlockBlob>().ToList();
                    meetingMinutesUiPathArguments.DownloadUrls.AddRange(cloudBlockBlobs.Select(x => x.Uri.AbsoluteUri)
                        .Where(x => x.Contains(_audioWriter.WavFileCount.ToString())));
                    foreach (var item in meetingMinutesUiPathArguments.DownloadUrls)
                    {
                        WriteLine(item);
                    }
                } while (blobContinuationToken != null);
            }
            else
            {
                WriteLine("A connection string has not been defined in the system environment variable.");
                WriteLine("Press any key to exist.");
                ReadLine();
            }
        }


        private static async Task WebSocketClientOnOnMessage(MessageEventArgs e)
        {
            try
            {
                if (e.Data == null) return;
                var activitySet = JsonConvert.DeserializeObject<ActivitySet>(e.Data);

                if (activitySet == null) return;
                foreach (var activity in activitySet.Activities)
                {
                    if (activity.Type != ActivityTypes.Message || string.IsNullOrEmpty(activity.Text)) continue;

                    if (activity.Text.Contains("Record_Start"))
                    {
                        if (_audioWriter != null)
                        {
                            WriteLine(
                                "Tried to record again but can only have one recording at a time. Stop the other recorder first.");
                            continue;
                        }

                        _audioWriter = new AudioWriter(OutputFolder);
                        _audioWriter.StartRecording();
                    }
                    else if (activity.Text.Contains("Record_Stop"))
                    {
                        var messages = activity.Text.Split(',');
                        var jobId = messages[1];
                        _audioWriter?.StopRecording();
                        var uiPathDownloadArguments = new MeetingMinutesUiPathArguments
                        {
                            JobId = jobId,
                            ServiceUrl = Settings.BotServiceUrl,
                            BotAppId = Settings.BotAppId,
                            BotAppPassword = Settings.BotAppPassword,
                            EmailToSend = Settings.EmailToSend,
                            EmailBody = Settings.EmailBody,
                            EmailSubject = $"Meeting minutes for {DateTime.Today.ToShortDateString()}",
                        };
                        await CallCognitiveServices(uiPathDownloadArguments);
                        await UploadToAzureBlob(uiPathDownloadArguments);
                        await _uiPathHttpClient.SendUiPathJob(uiPathDownloadArguments,
                            Settings.UiPathMeetingMinutesJobKey);

                        _audioWriter = null;
                    }
                }
            }
            catch (Exception exception)
            {
                WriteLine(exception);
                throw;
            }
        }

        private static async Task<bool> TranscribeIdentifiedSpeakers(string meetingMinutesFilePath,
            SpeechIdentifier.SpeechIdentifier speechIdentifier,
            string wavFilePath, SpeechConfig config)
        {
            using (var meetingMinutesWriter = new StreamWriter(meetingMinutesFilePath))
            {
                var recognitionResults = speechIdentifier.RecognitionResults.ToList();
                var startIndex = 0;
                var currentResult = recognitionResults.FirstOrDefault(x => x.Succeeded);
                if (currentResult == null)
                {
                    WriteLine("No recognized speaker identified. Skipping");
                    return true;
                }

                for (var index = 0; index < recognitionResults.Count; index++)
                {
                    var result = recognitionResults[index];
                    if (!result.Succeeded || result.Value.IdentifiedProfileId == default) continue;
                    if (index != recognitionResults.Count - 1 &&
                        currentResult.Value.IdentifiedProfileId == result.Value.IdentifiedProfileId) continue;
                    WriteLine("Transcribing from {0} to {1}", startIndex, index);

                    var transcriber = new Transcriber(wavFilePath, meetingMinutesWriter);
                    var person = GetSpeakerName(currentResult);
                    currentResult = result;
                    await transcriber.TranscribeSpeechFromAudioStream(config, person, startIndex, index);
                    startIndex = index;
                }
            }

            return false;
        }

        private static string GetSpeakerName(RecognitionResult currentResult)
        {
            var person = "Unknown";
            var s = currentResult.Value.IdentifiedProfileId.ToString();
            if (s.Contains("1aef1c90-8936-49ed-aaf0-0b4843f5f95b"))
            {
                person = "Judith";
            }
            else if (s.Contains("5d68a5aa-4426-4c4b-b8f9-2de573492dcb"))
            {
                person = "Leo";
            }
            else if (s.Contains("7af47c2c-7165-4cbc-997a-b3f8fc5bbbbb"))
            {
                person = "Meeting Messenger Bot";
            }

            return person;
        }
    }
}