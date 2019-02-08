using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using SpeechCognitiveServices.SpeechIdentifier;
using static System.Console;
using static System.IO.Path;
using RecognitionResult = SpeechCognitiveServices.SpeechIdentifier.RecognitionResult;

namespace SpeechCognitiveServices
{
    internal class Program
    {
        private static async Task Main()
        {
            var outputFolder = Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "MeetingMinutes");
            var wavFilesPath = Directory.GetFiles(outputFolder, "*.wav", SearchOption.TopDirectoryOnly);
            var wavFileCount = wavFilesPath.Length;
            var fullTranscribeSpeechPath = Combine(outputFolder, $"{wavFileCount}.transcribed.txt");
            var meetingMinutesFilePath = Combine(outputFolder, $"{wavFileCount}.minutes.txt");
            var keyExtractionFilePath = Combine(outputFolder, $"{wavFileCount}.keyphrases.txt");
            var sentimentAnalysisFilePath = Combine(outputFolder, $"{wavFileCount}.sentiment.txt");
            try
            {
                foreach (var wavFilePath in wavFilesPath)
                {
                    var config = SpeechConfig.FromSubscription(Config.SpeechServiceSubscriptionKey, "westus");
                    config.SpeechRecognitionLanguage = "en-US";

                    WriteLine("===== Initializing Speech Identifier =====");
                    var speakerIdentifierHttpRequests = new List<Task>();
                    SpeechIdentifier.SpeechIdentifier speechIdentifier = new SpeechIdentifier.SpeechIdentifier(wavFilePath, speakerIdentifierHttpRequests);
                    await speechIdentifier.IdentifySpeakers();
                    Task.WaitAll(speakerIdentifierHttpRequests.ToArray());
                    WriteLine("===== Done Speaker Identification =====");

                    WriteLine("===== Transcribing Identified Speakers =====");
                    if (await TranscribeIdentifiedSpeakers(meetingMinutesFilePath, speechIdentifier, wavFilePath, config)) continue;
                    WriteLine("===== Done Transcribing Identified Speakers =====");

                    WriteLine("===== Transcribing entire audio =====");
                    using (var fullTranscribeSpeechWriter = new StreamWriter(fullTranscribeSpeechPath))
                    {
                        var transcriber = new Transcriber(wavFilePath, fullTranscribeSpeechWriter);
                        await transcriber.TranscribeSpeechFromWavFileInput(config);
                    }
                    WriteLine("===== Done Transcribing entire audio =====");


                    WriteLine("===== Initializing Key Extraction and Sentiment Analysis =====");
                    var textAnalytics = new TextAnalytics.TextAnalytics(meetingMinutesFilePath, fullTranscribeSpeechPath);

                    textAnalytics.KeyExtraction(keyExtractionFilePath);
                    textAnalytics.SentimentAnalysis(sentimentAnalysisFilePath);
                    WriteLine("===== Done Key Extraction and Sentiment Analysis =====");
                }
            }
            catch (Exception e)
            {
                WriteLine(e);
                throw;
            }

            WriteLine("===== Initializing UIPath Robot =====");
            CallUiPathRobot();
        }

        private static void CallUiPathRobot()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "uirobot",
                    Arguments = $@"/file ""{Config.UiPathArguments}""",
                    UseShellExecute = true,
                    WorkingDirectory = Config.UiPathWorkingDirectory,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
        }

        private static async Task<bool> TranscribeIdentifiedSpeakers(string meetingMinutesFilePath, SpeechIdentifier.SpeechIdentifier speechIdentifier,
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
            // TODO: Implement get speaker name for depending on results
            return "";
        }
    }
}