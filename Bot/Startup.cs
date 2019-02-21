using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeetingMinutesBot
{
    /// <summary>
    /// The Startup class configures services and the request pipeline.
    /// </summary>
    public class Startup
    {
        private ILoggerFactory _loggerFactory;
        private readonly bool _isProduction;

        public Startup(IHostingEnvironment env)
        {
            _isProduction = env.IsProduction();
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        /// <summary>
        /// Gets the configuration that represents a set of key/value application configuration properties.
        /// </summary>
        /// <value>
        /// The <see cref="IConfiguration"/> that represents a set of key/value application configuration properties.
        /// </value>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> specifies the contract for a collection of service descriptors.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            var environment = _isProduction ? "production" : "development";
            var secretKey = Configuration.GetSection("botFileSecret")?.Value;
            var botFilePath = Configuration.GetSection("botFilePath")?.Value;
            // ReSharper disable NotResolvedInText
            var uiPathCreateHelpDeskJobFileName = Configuration.GetSection("uiPathCreateHelpDeskJobFileName")?.Value ?? throw new ArgumentNullException("uiPathCreateHelpDeskJobFileName is required. Check your appsettings.json");
            var uiPathBuyProductsOnAmazonFileName = Configuration.GetSection("uiPathBuyProductsOnAmazonFileName")?.Value ?? throw new ArgumentNullException("uiPathBuyProductsOnAmazonFileName is required. Check your appsettings.json");
            var uiPathSendEmailFileName = Configuration.GetSection("uiPathSendEmailFileName")?.Value ?? throw new ArgumentNullException("uiPathSendEmailFileName is required. Check your appsettings.json");
            var uiPathWorkingDirectory = Configuration.GetSection("uiPathWorkingDirectory")?.Value ?? throw new ArgumentNullException("uiPathWorkingDirectory is required. Check your appsettings.json");
            var uiRobotPath = Configuration.GetSection("uiRobotPath")?.Value ?? throw new ArgumentNullException("uiRobotPath is required. Check your appsettings.json");
            var speechRecognitionWorkingDirectory = Configuration.GetSection("uiPathBuyProductsOnAmazonFileName")?.Value ?? throw new ArgumentNullException("speechRecognitionWorkingDirectory is required. Check your appsettings.json");
            var speechRecognitionDll = Configuration.GetSection("speechRecognitionDll")?.Value ?? throw new ArgumentNullException("speechRecognitionDll is required. Check your appsettings.json");
            // ReSharper restore NotResolvedInText
            // Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
            var botConfig = BotConfiguration.Load(botFilePath ?? @".\MeetingMinutesBot.bot", secretKey);
            services.AddSingleton(sp =>
                botConfig ??
                throw new InvalidOperationException($"The .bot config file could not be loaded. ({(BotConfiguration) null})"));


            // Initialize Bot Connected Services clients.
            var connectedServices = new BotServices(botConfig);
            services.AddSingleton(sp => new Config(uiPathCreateHelpDeskJobFileName, uiPathWorkingDirectory, uiRobotPath, uiPathBuyProductsOnAmazonFileName, uiPathSendEmailFileName, speechRecognitionDll, speechRecognitionWorkingDirectory));
            services.AddSingleton(sp => connectedServices);
            services.AddSingleton(sp => botConfig);
            
            services.AddSingleton<AudioWriter>();

            var dataStore = new MemoryStorage();
            var conversationState = new ConversationState(dataStore);
            var jobState = new JobState(dataStore);
            var userState = new UserState(dataStore);

            services.AddBot<Bot>(options =>
            {
                // Retrieve current endpoint.
                var service = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == environment);
                if (!(service is EndpointService endpointService))
                {
                    throw new InvalidOperationException(
                        $"The .bot file does not contain an endpoint with name '{environment}'.");
                }
                
                options.CredentialProvider =
                    new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

                // Creates a logger for the application to use.
                ILogger logger = _loggerFactory.CreateLogger<Bot>();

                // Catches any errors that occur during a conversation turn and logs them.
                options.OnTurnError = async (context, exception) =>
                {
                    logger.LogError($"Exception caught : {exception}");
                    await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                };
            });


            services.AddSingleton(sp =>
            {
                var service = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == environment);
                if (!(service is EndpointService))
                {
                    throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
                }

                return (EndpointService)service;
            });
            services.AddSingleton(sp => jobState);
            services.AddSingleton(sp => new StateBotAccessors(conversationState, userState)
            {
                UserAccessor = userState.CreateProperty<User>(StateBotAccessors.UserName)
            });
        }

        // Note: ReSharper might think it's not used but ASP.NET core uses magic to call Configure to bootstrap the web app.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            var logger = _loggerFactory.CreateLogger<Startup>();
            app.Use(async (ctx, next) =>
            {
                try
                {
                    await next.Invoke();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex.ToString());
                }
            });
            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}