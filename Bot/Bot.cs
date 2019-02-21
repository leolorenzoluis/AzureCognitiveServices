using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using MeetingMinutesBot.UiPathDomain;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MeetingMinutesBot
{
    public class Bot : IBot
    {
        // Intents
        public const string StartRecording = "Record_Start";
        public const string StopRecording = "Record_Stop";
        public const string SalesForecast = "Sales_Forecast";
        public const string CreateHelpDeskTicket = "Create_HelpDeskTicket";
        public const string BuyAmazon = "Buy_Amazon";
        public const string SendEmail = "Communication_SendEmail";

        // The name of events that signal that a job has completed. This is sent by UIPath
        public const string JobCompleteEventName = "UIPathJobComplete";

        public static readonly string LuisKey = "LuisBot";
        private readonly BotServices _services;
        private readonly AudioWriter _writer;
        private readonly StateBotAccessors _stateBotPropertyAccessors;
        private readonly JobState _jobState;
        private readonly Config _config;


        private readonly IStatePropertyAccessor<JobStorage> _jobStatePropertyAccessor;
        private readonly ILogger _logger;

        /// <summary>Gets the bot app ID.</summary>
        /// <remarks>AppId required to continue a conversation.
        /// See <see cref="BotAdapter.ContinueConversationAsync"/> for more details.</remarks>
        private string AppId { get; }

        public Bot(ILoggerFactory loggerFactory, BotServices services, EndpointService endpointService,
            AudioWriter writer, StateBotAccessors stateBotPropertyAccessors, JobState jobState, Config config)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _jobState = jobState ?? throw new ArgumentNullException(nameof(jobState));
            _config = config;
            _writer = writer;
            _stateBotPropertyAccessors = stateBotPropertyAccessors;
            _jobStatePropertyAccessor = jobState.CreateProperty<JobStorage>(nameof(JobState));
            if (!_services.LuisServices.ContainsKey(LuisKey))
            {
                throw new ArgumentException("Missing LUIS configuration....");
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<Bot>();
            _services = services;
            AppId = string.IsNullOrEmpty(endpointService.AppId) ? "1" : endpointService.AppId;
        }

        private async Task CompleteJobAsync(BotAdapter adapter, string botId, JobStorage jobStorage,
            UiPathJobResponse uiPathJobResponse,
            CancellationToken cancellationToken)
        {
            jobStorage.TryGetValue(uiPathJobResponse.JobId, out var jobInfo);
            if (jobInfo != null)
            {
                await adapter.ContinueConversationAsync(botId, jobInfo.ConversationReference,
                    CompleteJobHandler(uiPathJobResponse),
                    cancellationToken);
            }
        }

        private BotCallbackHandler CompleteJobHandler(UiPathJobResponse uiPathJobResponse)
        {
            return async (turnContext, token) =>
            {
                var jobStorage = await _jobStatePropertyAccessor.GetAsync(turnContext, () => new JobStorage(), token);
                jobStorage[uiPathJobResponse.JobId].Completed = true;
                await _jobStatePropertyAccessor.SetAsync(turnContext, jobStorage, token);
                await _jobState.SaveChangesAsync(turnContext, cancellationToken: token);
                await turnContext.SendActivityAsync(
                    $"Job {uiPathJobResponse.JobId} is complete. {uiPathJobResponse.Message}",
                    cancellationToken: token);
                _logger.LogDebug($"Received UI Path Job Response Type {uiPathJobResponse.Type}");
                switch (uiPathJobResponse.Type)
                {
                    case UiPathEventType.HelpDesk:
                        var reply = turnContext.Activity.CreateReply(
                            "Networking team has approved your help desk ticket. Which of the following approved routers would you like to purchase?");
                        reply.Type = ActivityTypes.Message;
                        reply.TextFormat = TextFormatTypes.Plain;
                        reply.SuggestedActions = new SuggestedActions()
                        {
                            Actions = new List<CardAction>()
                            {
                                new CardAction()
                                {
                                    Title = "2QW1646 Cisco RV320", Type = ActionTypes.ImBack,
                                    Value = "Please purchase 2QW1646 Cisco RV320"
                                },
                                new CardAction()
                                {
                                    Title = "NETGEAR R6700 Nighthawk", Type = ActionTypes.ImBack,
                                    Value = "Please purchase  NETGEAR R6700 Nighthawk"
                                },
                                new CardAction()
                                {
                                    Title = "TP-Link AC1200", Type = ActionTypes.ImBack,
                                    Value = "Please purchase TP-Link AC1200"
                                },
                            },
                        };
                        await turnContext.SendActivityAsync(reply, cancellationToken: token);
                        break;
                }
            };
        }

        public async Task OnTurnAsync(ITurnContext turnContext,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var user =
                await _stateBotPropertyAccessors.UserAccessor.GetAsync(turnContext,
                    () => new User {DidBotWelcomeUser = false},
                    cancellationToken);
            if (turnContext.Activity.Conversation != null)
            {
                _logger.LogDebug(
                    $"From JobId: {turnContext.Activity.From.Id} - Activity JobId: {turnContext.Activity.Id} - Conversation JobId: {turnContext.Activity.Conversation.Id} - Activity Type: {turnContext.Activity.Type} - Message: {turnContext.Activity.Text}");
            }

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
                    if (topIntent != null && topIntent.Value.score >= 0.5)
                    {
                        Job job;
                        switch (topIntent.Value.intent)
                        {
                            case StartRecording:
                                _writer.StartRecording();
                                await turnContext.SendActivityAsync("Boss I'm going to start the recording.",
                                    cancellationToken: cancellationToken);
                                break;
                            case SalesForecast:
                                await turnContext.SendActivityAsync(
                                    "Boss, I'm querying finance systems right now. I'll get back to you.",
                                    cancellationToken: cancellationToken);
                                await Task.Delay(7000, cancellationToken);
                                await turnContext.SendActivityAsync(
                                    "Our sales forecast for this year will be $100 million. So work hard play hard!",
                                    cancellationToken: cancellationToken);
                                break;
                            case StopRecording:
                                _writer.StopRecording();
                                await turnContext.SendActivityAsync(
                                    "Boss I'm going to stop the recording and will start processing meeting minutes.",
                                    cancellationToken: cancellationToken);
                                StartProcess("dotnet", _config.SpeechRecognitionDll,
                                    _config.SpeechRecognitionWorkingDirectory);
                                break;
                            case CreateHelpDeskTicket:
                                job = await CreateJob(turnContext, cancellationToken);
                                var uiPathJobRequest = new UiPathJobRequest(job.Id, turnContext.Activity.ServiceUrl);
                                StartProcess(_config.UiRobotPath,
                                    CreateUiPathProcessArguments(_config.UiPathCreateHelpDeskJobFileName,
                                        uiPathJobRequest),
                                    _config.UiPathWorkingDirectory);
                                break;
                            case BuyAmazon:
                                job = await CreateJob(turnContext, cancellationToken);
                                var amazonJob = new UiPathAmazonJob(job.Id,
                                    turnContext.Activity.ServiceUrl,
                                    new List<string> {"2QW1646 Cisco RV320"});
                                StartProcess(_config.UiRobotPath,
                                    CreateUiPathProcessArguments(_config.UiPathBuyProductsOnAmazonFileName, amazonJob),
                                    _config.UiPathWorkingDirectory);
                                break;
                            case SendEmail:
                                job = await CreateJob(turnContext, cancellationToken);
                                var emailJob = new UiPathEmailJob(job.Id, turnContext.Activity.ServiceUrl,
                                    $"Meeting minutes for {DateTime.Today.ToShortDateString()}", "Hello World",
                                    "lluis@psi-it.com");
                                StartProcess(_config.UiRobotPath,
                                    CreateUiPathProcessArguments(_config.UiPathSendEmailFileName,
                                        emailJob),
                                    _config.UiPathWorkingDirectory);
                                break;
                            default:
                                // Help or no intent identified, either way, let's provide some help.
                                // to the user
                                await turnContext.SendActivityAsync(
                                    "Boss I'm sorry but I didn't understand what you just said to me.",
                                    cancellationToken: cancellationToken);
                                break;
                        }
                    }
                    else
                    {
                        const string msg =
                            @"No LUIS intents were found. Try typing 'Start Meeting' or 'Stop Meeting'.";
                        await turnContext.SendActivityAsync(msg, cancellationToken: cancellationToken);
                    }

                    break;
                }

                // This will be true if it was an external send a type event and value is the job id.
                case ActivityTypes.Event:
                {
                    _logger.LogDebug("Received an activity type event");
                    var jobStorage =
                        await _jobStatePropertyAccessor.GetAsync(turnContext, () => new JobStorage(),
                            cancellationToken);
                    var activity = turnContext.Activity.AsEventActivity();
                    if (activity.Name == JobCompleteEventName &&
                        activity.Value is JObject value)
                    {
                        var jobEvent = value.ToObject<UiPathJobResponse>();
                        if (jobEvent != null)
                        {
                            if (jobStorage.ContainsKey(jobEvent.JobId) &&
                                !jobStorage[jobEvent.JobId].Completed)
                            {
                                _logger.LogDebug("Completing Job...");
                                await CompleteJobAsync(turnContext.Adapter, AppId, jobStorage, jobEvent,
                                    cancellationToken);
                            }
                        }
                    }

                    break;
                }
                case ActivityTypes.ConversationUpdate:
                    if (turnContext.Activity.MembersAdded.Any())
                    {
                        // Iterate over all new members added to the conversation
                        foreach (var member in turnContext.Activity.MembersAdded)
                        {
                            // Greet anyone that was not the target (recipient) of this message
                            // the 'bot' is the recipient for events from the channel,
                            // turnContext.Activity.MembersAdded == turnContext.Activity.Recipient.JobId indicates the
                            // bot was added to the conversation.
                            if (member.Id == turnContext.Activity.Recipient.Id) continue;
                            _logger.LogTrace($"Member Name: {member.Name} & Member JobId: {member.Id}");
                            user.DidBotWelcomeUser = true;
                            await _stateBotPropertyAccessors.UserAccessor.SetAsync(turnContext, user,
                                cancellationToken);
                            await _stateBotPropertyAccessors.UserState.SaveChangesAsync(turnContext,
                                cancellationToken: cancellationToken);
                            // Send a welcome message to the user and tell them what actions they may perform to use this bot
                            await SendWelcomeMessageAsync(turnContext, cancellationToken);
                        }
                    }

                    break;
                default:
                    await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected",
                        cancellationToken: cancellationToken);
                    break;
            }
        }

        private string CreateUiPathProcessArguments(string fileName, object argument)
        {
            var uiPathProcessArguments =
                $@"/file ""C:\Users\lluisadmin\Documents\Pyramid Labs\RPA\{fileName}"" /input ""{HttpUtility.JavaScriptStringEncode(JsonConvert.SerializeObject(argument))}"" ";
            _logger.LogDebug($"Ui Path Process Arguments {uiPathProcessArguments}");
            return
                uiPathProcessArguments;
        }

        private static void StartProcess(string fileName, string processArguments, string workingDirectory)
        {
            var callUiRobot = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = processArguments,
                    UseShellExecute = true,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            };
            callUiRobot.Start();
        }

        private async Task<Job> CreateJob(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var jobStorage = await _jobStatePropertyAccessor.GetAsync(turnContext,
                () => new JobStorage(), cancellationToken);
            var job = new Job
            {
                Id = jobStorage.Count + 1,
                ConversationReference = turnContext.Activity.GetConversationReference()
            };
            jobStorage.TryAdd(jobStorage.Count + 1, job);
            await _jobStatePropertyAccessor.SetAsync(turnContext, jobStorage, cancellationToken);
            await _jobState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync(
                $"Job {job.Id} is created. Delegating task to an RPA Robot. We'll notify you when it's complete.",
                cancellationToken: cancellationToken);
            return job;
        }

        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext,
            CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync("Hello. Welcome to the Meeting Minutes bot!",
                cancellationToken: cancellationToken);
        }
    }
}