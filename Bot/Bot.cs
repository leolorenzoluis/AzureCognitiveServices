using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace MeetingMinutesBot
{
    public class Bot : IBot
    {
        // Intents
        public const string StartRecording = "Record_Start";
        public const string StopRecording = "Record_Stop";
        public const string SalesForecast = "Sales_Forecast";
        public const string None = "None";

        public static readonly string LuisKey = "LuisBot";
        private readonly BotServices _services;
        private readonly AudioWriter _writer;
        private readonly ILogger _logger;

        public Bot(ILoggerFactory loggerFactory, BotServices services,
            AudioWriter writer)
        {
            _services = services ?? throw new System.ArgumentNullException(nameof(services));
            _writer = writer;
            if (!_services.LuisServices.ContainsKey(LuisKey))
            {
                throw new System.ArgumentException($"Invalid configuration....");
            }

            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<Bot>();
            _logger.LogTrace("Turn start.");
            _services = services;
        }

        public async Task OnTurnAsync(ITurnContext turnContext,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogTrace($"Activity Type: {turnContext.Activity.Type}");
            switch (turnContext.Activity.Type)
            {
                // Handle Message activity type, which is the main activity type for shown within a conversational interface
                // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
                // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
                case ActivityTypes.Message:
                {
                    // Check LUIS model
                    var recognizerResult =
                        await _services.LuisServices[LuisKey].RecognizeAsync(turnContext, cancellationToken);
                    var topIntent = recognizerResult?.GetTopScoringIntent();
                    if (topIntent != null && topIntent.Value.intent != "None")
                    {
                        switch (topIntent.Value.intent)
                        {
                            case StartRecording:
                                _writer.StartRecording();
                                await turnContext.SendActivityAsync("Boss I'm going to start the recording.", cancellationToken: cancellationToken);
                                break;
                            case SalesForecast:
                                await turnContext.SendActivityAsync("Boss, I'm querying finance systems right now. I'll get back to you.", cancellationToken: cancellationToken);
                                await Task.Delay(7000, cancellationToken);
                                await turnContext.SendActivityAsync(
                                    "Our sales forecast for this year will be $100 million. So work hard play hard!", cancellationToken: cancellationToken);
                                break;
                            case StopRecording:
                                _writer.StopRecording();
                                await turnContext.SendActivityAsync(
                                    "Boss I'm going to stop the recording and will start processing meeting minutes.", cancellationToken: cancellationToken);
                                var process = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "dotnet",
                                        Arguments = @"SpeechCognitiveServices.dll",
                                        UseShellExecute = true,
                                        WorkingDirectory = @"", // TODO: Populate
                                        RedirectStandardOutput = false,
                                        RedirectStandardError = false,
                                        CreateNoWindow = true
                                    }

                                };
                                process.Start();
                                break;
                            case None:
                            default:
                                // Help or no intent identified, either way, let's provide some help.
                                // to the user
                                await turnContext.SendActivityAsync("Boss I'm sorry but I didn't understand what you just said to me.", cancellationToken: cancellationToken);
                                break;
                        }
                    }
                    else
                    {
                        const string msg = @"No LUIS intents were found.
                        This sample is about identifying two user intents:
                        'Calendar.Add'
                        'Calendar.Find'
                        Try typing 'Add Event' or 'Show me tomorrow'.";
                        await turnContext.SendActivityAsync(msg, cancellationToken: cancellationToken);
                    }

                    break;
                }
                case ActivityTypes.ConversationUpdate:
                    // Send a welcome message to the user and tell them what actions they may perform to use this bot
                    await SendWelcomeMessageAsync(turnContext, cancellationToken);
                    break;
                default:
                    await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected",
                        cancellationToken: cancellationToken);
                    break;
            }
        }

        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync("Hello. Welcome to the Meeting Minutes bot!", cancellationToken: cancellationToken);
        }
    }
}