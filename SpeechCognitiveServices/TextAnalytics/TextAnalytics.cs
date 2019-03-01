using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Rest;
using SpeechCognitiveServices.Properties;
using static System.Console;

namespace SpeechCognitiveServices.TextAnalytics
{
    public class TextAnalytics
    {
        private readonly string _meetingMinutesFilePath;
        private readonly string _fullTranscribeSpeechPath;
        private readonly ITextAnalyticsClient _client;

        public TextAnalytics(string meetingMinutesFilePath, string fullTranscribeSpeechPath)
        {
            _meetingMinutesFilePath = meetingMinutesFilePath;
            _fullTranscribeSpeechPath = fullTranscribeSpeechPath;
            _client = new TextAnalyticsClient(new TextAnalyticsApiKeyServiceClientCredentials())
            {
                Endpoint = "https://eastus.api.cognitive.microsoft.com"
            };
        }

        public void KeyExtraction(string keyExtractionFilePath)
        {
            // Getting key-phrases
            WriteLine("===== KEY-PHRASE EXTRACTION ======");
            try
            {
                using (var sw = new StreamWriter(keyExtractionFilePath))
                {
                    // Open the text file using a stream reader.
                    using (var sr = new StreamReader(_fullTranscribeSpeechPath)
                    )
                    {
                        // Read the stream to a string, and write the string to the console.
                        var textToAnalyze = sr.ReadToEnd();
                        var result = _client.KeyPhrasesAsync(new MultiLanguageBatchInput(
                            new List<MultiLanguageInput>
                            {
                                new MultiLanguageInput("en", "1", textToAnalyze)
                            })).Result;

                        // Printing key phrases
                        foreach (var document in result.Documents)
                        {
                            WriteLine($"Document ID: {document.Id} ");

                            WriteLine("\t Key phrases:");

                            foreach (var keyPhrase in document.KeyPhrases)
                            {
                                sw.WriteLine(keyPhrase);
                                WriteLine($"\t\t{keyPhrase}");
                            }
                        }

                        WriteLine(textToAnalyze);
                    }
                }
            }
            catch (IOException e)
            {
                WriteLine("The file could not be read:");
                WriteLine(e.Message);
            }
        }

        public void SentimentAnalysis(string sentimentAnalysisFilePath)
        {
            // Extracting sentiment
            WriteLine("\n\n===== SENTIMENT ANALYSIS ======");

            try
            {
                using (var sw = new StreamWriter(sentimentAnalysisFilePath))
                {
                    // Open the text file using a stream reader.
                    using (var sr =
                        new StreamReader(_meetingMinutesFilePath)
                    )
                    {
                        // Read in a file line-by-line, and store it all in a List.
                        var multiLanguageInputs = new List<MultiLanguageInput>();
                        string line;
                        var id = 0;
                        while ((line = sr.ReadLine()) != null)
                        {
                            multiLanguageInputs.Add(new MultiLanguageInput("en", id.ToString(), line)); // Add to list.
                            id++;
                        }

                        var result =
                            _client.SentimentAsync(new MultiLanguageBatchInput(multiLanguageInputs)).Result;

                        // Printing sentiment results
                        foreach (var document in result.Documents)
                        {
                            sw.WriteLine($"{document.Score:0.00}");
                            WriteLine($"Document ID: {document.Id} , Sentiment Score: {document.Score:0.00}");
                        }
                    }
                }
            }
            catch (IOException e)
            {
                WriteLine("The file could not be read:");
                WriteLine(e.Message);
            }
            catch (Exception e)
            {
                WriteLine("There was an error. Maybe your file is empty?");
                WriteLine(e.Message);
            }
        }

        public void IdentifyEntities()
        {
            try
            {
                WriteLine("\n\n===== ENTITIES ======");

                var entitiesResult = _client.EntitiesAsync(
                    new MultiLanguageBatchInput(
                        new List<MultiLanguageInput>
                        {
                            new MultiLanguageInput("en", "0",
                                "The Great Depression began in 1929. By 1933, the GDP in America fell by 25%.")
                        })).Result;

                // Printing entities results
                foreach (var document in entitiesResult.Documents)
                {
                    WriteLine($"Document ID: {document.Id} ");

                    WriteLine("\t Entities:");

                    foreach (EntityRecordV2dot1 entity in document.Entities)
                    {
                        WriteLine(
                            $"\t\t{entity.Name}\t\t{entity.WikipediaUrl}\t\t{entity.Type}\t\t{entity.SubType}");
                    }
                }
            }
            catch (Exception e)
            {
                WriteLine(e);
                throw;
            }
        }
    }

    public class TextAnalyticsApiKeyServiceClientCredentials : ServiceClientCredentials
    {
        public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", Settings.Default.TextAnalyticsSubscriptionKey);
            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}