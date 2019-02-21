namespace MeetingMinutesBot
{
    public class Config
    {
        public string UiPathCreateHelpDeskJobFileName { get; }
        public string UiPathWorkingDirectory { get; }
        public string UiRobotPath { get; }
        public string UiPathBuyProductsOnAmazonFileName { get; }
        public string UiPathSendEmailFileName { get; }
        public string SpeechRecognitionDll { get; }
        public string SpeechRecognitionWorkingDirectory { get; }

        public Config(string uiPathCreateHelpDeskJobFileName, string uiPathWorkingDirectory, string uiRobotPath, string uiPathBuyProductsOnAmazonFileName, string uiPathSendEmailFileName, string speechRecognitionDll, string speechRecognitionWorkingDirectory)
        {
            UiPathCreateHelpDeskJobFileName = uiPathCreateHelpDeskJobFileName;
            UiPathWorkingDirectory = uiPathWorkingDirectory;
            UiRobotPath = uiRobotPath;
            UiPathBuyProductsOnAmazonFileName = uiPathBuyProductsOnAmazonFileName;
            UiPathSendEmailFileName = uiPathSendEmailFileName;
            SpeechRecognitionDll = speechRecognitionDll;
            SpeechRecognitionWorkingDirectory = speechRecognitionWorkingDirectory;
        }
    }
}