I recently blogged about Robotic Process Automation and was curious as to how we can incorporate Machine Learning and make the robots smarter. In this post, I will guide you on how to run the meeting minutes transcriber. The idea is that if there is a meeting that is going to start, the meeting organizer can talk to a bot and the bot will start recording and will later transcribe the output to the meeting organizer's OneNote and Excel.
# Prerequisites
- Download the sample code from Github.
- Microsoft .NET Core 2.1
- Azure Subscription
- Azure Bot Emulator
- Speech Service Subscription Key
- Text Analytics Subscription Key
- Speaker Recognition Subscription Key
- Luis Subscription Key
- UIPath Studio

Before you proceed further, you would first want to create a UIPath that performs the basic functionality of reading the file and putting it in OneNote and Excel. There's a lot of great tutorials out there such as this article. After you have published your UIPath Workflow then you can proceed further down.
In LUIS.ai portal, create a new app and create an Intent. For this demo purposes, specify the following Intents
- Record_Start - Add dialogues you want the model to be trained with when you want to start recording
- Record_Stop - Add dialogues you want the model to be trained with when you want to stop recording
- Sales_Forecast - Add dialogues you want the model to be trained with when you want to simulate getting the sales forecast

# The architecture diagram
![alt text](https://leoluis.xyz/content/images/2019/02/ArchitectureDiagram.png "Architecture Diagram")

Open your favorite editor. In my case, I'll be using Visual Studio.
Open Config.cs and populate the following values

- UiPathArguments - Fill this up with the exported nuget package from UIPath. For example C:\Desktop\ThisWillBeTheFileNameOfTheExportedWorkflow.1.0.0.nupkg
- UiPathWorkingDirectory - Fill this up with the directory where the UIPath file is contained. For example C:\Desktop

Open MeetingMinutesBot.bot and populate the AppId, AuthoringKey, SubscriptionKey, and Region

Run the MeetingMinutesBot.csproject and you should be able to be see a page like this

Run the Bot Framework Emulator and load MeetingMinutesBot.bot. Once loaded, make sure you're connected to the web server that you deployed the bot with.

You can start talking to the bot based on what you recorded as your intent from LUIS.ai. For my example, I just used a basic dialogue of Start the meeting and Stop the meeting.

Once you mentioned to the bot that you want to stop the meeting, you should be able to see a console application that will launched to start calling different Azure Cognitive Services.

After the console application has finished aggregating the results from different cognitive services, you should be able to see it launching UIRobot to run the workflow for Robotic Process Automation to OneNote and Excel.
