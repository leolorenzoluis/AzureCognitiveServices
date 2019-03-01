using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using UiPathWorkflow;
using Activity = Microsoft.Bot.Schema.Activity;

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
        private readonly StateBotAccessors _stateBotPropertyAccessors;
        private readonly JobState _jobState;


        private readonly IStatePropertyAccessor<JobStorage> _jobStatePropertyAccessor;
        private readonly ILogger _logger;

        /// <summary>Gets the bot app ID.</summary>
        /// <remarks>AppId required to continue a conversation.
        /// See <see cref="BotAdapter.ContinueConversationAsync"/> for more details.</remarks>
        private string AppId { get; }

        private string AppPassword { get; }
        private string ServiceEndpoint { get; }
        private static UiPathHttpClient _uiPathHttpClient;

        public Bot(ILoggerFactory loggerFactory, BotServices services, EndpointService endpointService,
            StateBotAccessors stateBotPropertyAccessors, JobState jobState, Config config)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _jobState = jobState ?? throw new ArgumentNullException(nameof(jobState));
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
            AppPassword = string.IsNullOrEmpty(endpointService.AppId) ? "1" : endpointService.AppPassword;
            ServiceEndpoint = endpointService.Endpoint;
            _uiPathHttpClient = new UiPathHttpClient(config.UiPathUserName, config.UiPathPassword, config.UiPathTenancyName);
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
                    $"Job {uiPathJobResponse.JobId} is complete. {uiPathJobResponse.Message}",
                    cancellationToken: token);
                _logger.LogDebug($"Received UI Path Job Response Type {uiPathJobResponse.Type}");
                Activity reply;
                switch (uiPathJobResponse.Type)
                {
                    case UiPathEventType.HelpDesk:
                        reply = turnContext.Activity.CreateReply(
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
                                }
                            }
                        };
                        await turnContext.SendActivityAsync(reply, cancellationToken: token);
                        break;
                    case UiPathEventType.Amazon:
                        reply = turnContext.Activity.CreateReply();
                        reply.Attachments.Add(GetReceiptCard().ToAttachment());
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
                                await turnContext.SendActivityAsync("Boss I'm going to start the recording.", inputHint: InputHints.IgnoringInput,
                                    cancellationToken: cancellationToken);
                                await turnContext.Adapter.ContinueConversationAsync(AppId,
                                    _stateBotPropertyAccessors.AudioRecorderConversationReference,
                                    async (context, token) =>
                                    {
                                        await context.SendActivityAsync(StartRecording, cancellationToken: token);
                                    },
                                    cancellationToken);
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
                                await turnContext.SendActivityAsync(
                                    "Boss I'm going to stop the recording and will start processing meeting minutes.",
                                    cancellationToken: cancellationToken);

                                job = await CreateJob(turnContext, cancellationToken);
                                await turnContext.Adapter.ContinueConversationAsync(AppId,
                                    _stateBotPropertyAccessors.AudioRecorderConversationReference,
                                    async (context, token) =>
                                    {
                                        await context.SendActivityAsync($"{StopRecording},{job.Id}", cancellationToken: token);
                                    },
                                    cancellationToken);
                                break;
                            case CreateHelpDeskTicket:
                                job = await CreateJob(turnContext, cancellationToken);
                                //var uiPathJobRequest = new UiPathJobRequest(job.Id, turnContext.Activity.ServiceUrl);

                                var uiPathArguments = new UiPathArguments
                                {
                                    BotAppId = AppId,
                                    BotAppPassword = AppPassword,
                                    JobId = job.Id.ToString(),
                                    ServiceUrl = ServiceEndpoint
                                };
                                await _uiPathHttpClient.SendUiPathJob(uiPathArguments, "963d294a-960b-4e16-b6af-3b5dd782f637",
                                    cancellationToken);
                                break;
                            case BuyAmazon:
                                job = await CreateJob(turnContext, cancellationToken);
                                var amazonJob = new AmazonUiPathArguments
                                {
                                    BotAppId = AppId,
                                    BotAppPassword = AppPassword,
                                    JobId = job.Id.ToString(),
                                    ServiceUrl = ServiceEndpoint,
                                    Products = new List<string> {"2QW1646 Cisco RV320"}
                                };
                                await _uiPathHttpClient.SendUiPathJob(amazonJob, "91497268-6c3a-4bef-8fdd-36a802386921",
                                    cancellationToken);
                                break;
                            case SendEmail:
                                job = await CreateJob(turnContext, cancellationToken);
                                var emailJob = new UiPathEmailJob(job.Id.ToString(), turnContext.Activity.ServiceUrl,
                                    $"Meeting minutes for {DateTime.Today.ToShortDateString()}", "Hello World",
                                    "lluis@psi-it.com");

                                await _uiPathHttpClient.SendUiPathJob(emailJob, "91497268-6c3a-4bef-8fdd-36a802386921",
                                    cancellationToken);
                                    break;
                            default:
                                // Help or no intent identified, either way, let's provide some help.
                                // to the user
                                await turnContext.SendActivityAsync(
                                    "Boss I'm sorry but I didn't understand what you just said to me.",
                                    inputHint: InputHints.IgnoringInput,
                                    cancellationToken: cancellationToken);
                                break;
                        }
                    }
                    else
                    {
                        const string msg =
                            @"No LUIS intents were found. Try typing 'Start Meeting' or 'Stop Meeting'.";
                        await turnContext.SendActivityAsync(msg, msg, InputHints.IgnoringInput, cancellationToken: cancellationToken);
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

                            if (member.Name == "AudioRecorder")
                            {
                                _stateBotPropertyAccessors.AudioRecorderConversationReference =
                                    turnContext.Activity.GetConversationReference();
                            }

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



        /// <summary>
        /// Creates a <see cref="ReceiptCard"/>.
        /// </summary>
        /// <returns>A <see cref="ReceiptCard"/> the user can view and/or interact with.</returns>
        /// <remarks>Related types <see cref="CardImage"/>, <see cref="CardAction"/>,
        /// <see cref="ActionTypes"/>, <see cref="ReceiptItem"/>, and <see cref="Fact"/>.</remarks>
        private static ReceiptCard GetReceiptCard()
        {
            var receiptCard = new ReceiptCard
            {
                Title = "John Doe",

                Facts = new List<Fact>
                    {new Fact("Order Number", "9378-4839-43289"), new Fact("Payment Method", "VISA 1234-****")},

                Items = new List<ReceiptItem>
                {
                    new ReceiptItem
                    {
                        Image = new CardImage(
                            "http://www.vmastoryboard.com/wp-content/uploads/2014/08/Amazon-A-Logo.jpg"),
                    },
                    new ReceiptItem(
                        "2QW1646 Cisco RV320",
                        subtitle:
                        "The Cisco RV320 provides reliable, highly secure access connectivity for you and your employees that is so transparent you will not know it is there.",
                        price: "104.50",
                        quantity: "1",
                        image: new CardImage(
                            url: "https://images-na.ssl-images-amazon.com/images/I/51ReTYTeQyL._SX425_.jpg")
                    ),
                },
                Tax = "7.50",
                Vat = "1.99",
                Total = "$113.99",
                Buttons = new List<CardAction>
                {
                    new CardAction(
                        ActionTypes.OpenUrl,
                        "More information",
                        "https://amazon.com",
                        "https://amazon.com",
                        "https://amazon.com",
                        "https://amazon.com"),
                },
            };

            return receiptCard;
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