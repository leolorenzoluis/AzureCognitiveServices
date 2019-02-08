using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;

namespace SpeechCognitiveServices.SpeechToText
{
    internal class SpeechToTextRecognizer
    {
        private readonly string _person;
        private readonly StreamWriter _streamWriter;

        public SpeechToTextRecognizer(string person, StreamWriter streamWriter) : this(streamWriter)
        {
            _person = person;
        }

        public SpeechToTextRecognizer(StreamWriter streamWriter)
        {
            _streamWriter = streamWriter;

        }

        /// <summary>
        ///     Subscribes to Recognition Events
        ///     Starts the Recognition and waits until final result is received, then Stops recognition
        /// </summary>
        /// <param name="recognizer">Recognizer object</param>
        /// <param name="recognizerType">Type of Recognizer</param>
        /// <param name="source">Task completion source</param>
        /// <value>
        ///     <c>Base</c> if Baseline model; otherwise, <c>Custom</c>.
        /// </value>
        public async Task RunRecognizer(SpeechRecognizer recognizer, RecognizerType recognizerType,
            TaskCompletionSource<int> source)
        {
            //subscribe to events

            // ReSharper disable ImplicitlyCapturedClosure
            void RecognizingHandler(object sender, SpeechRecognitionEventArgs e) => RecognizingEventHandler(e, recognizerType);
            void RecognizedHandler(object sender, SpeechRecognitionEventArgs e) => RecognizedEventHandler(e, recognizerType);
            void CanceledHandler(object sender, SpeechRecognitionCanceledEventArgs e) => CanceledEventHandler(e, recognizerType, source);
            void SessionStartedHandler(object sender, SessionEventArgs e) => SessionStartedEventHandler(e, recognizerType);
            void SessionStoppedHandler(object sender, SessionEventArgs e) => SessionStoppedEventHandler(e, recognizerType, source);
            void SpeechStartDetectedHandler(object sender, RecognitionEventArgs e) => SpeechDetectedEventHandler(e, recognizerType, "start");
            void SpeechEndDetectedHandler(object sender, RecognitionEventArgs e) => SpeechDetectedEventHandler(e, recognizerType, "end");
            // ReSharper restore ImplicitlyCapturedClosure

            recognizer.Recognizing += RecognizingHandler;
            recognizer.Recognized += RecognizedHandler;
            recognizer.Canceled += CanceledHandler;
            recognizer.SessionStarted += SessionStartedHandler;
            recognizer.SessionStopped += SessionStoppedHandler;

            //start,wait,stop recognition
            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
            await source.Task.ConfigureAwait(false);
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

            recognizer.Recognizing -= RecognizingHandler;
            recognizer.Recognized -= RecognizedHandler;
            recognizer.Canceled -= CanceledHandler;
            recognizer.SessionStarted -= SessionStartedHandler;
            recognizer.SessionStopped -= SessionStoppedHandler;
            recognizer.SpeechStartDetected -= SpeechStartDetectedHandler;
            recognizer.SpeechEndDetected -= SpeechEndDetectedHandler;
            recognizer.SpeechStartDetected -= SpeechStartDetectedHandler;
            recognizer.SpeechEndDetected -= SpeechEndDetectedHandler;
        }


        /// <summary>
        ///     Logs intermediate recognition results
        /// </summary>
        private static void RecognizingEventHandler(SpeechRecognitionEventArgs e, RecognizerType rt)
        {
            Console.WriteLine("Intermediate result: {0} ", e.Result.Text);
        }
        /// <summary>
        ///     Logs the final recognition result
        /// </summary>
        private void RecognizedEventHandler(SpeechRecognitionEventArgs e, RecognizerType rt)
        {
            Console.WriteLine($" --- Final result received. Reason: {e.Result.Reason.ToString()}. --- ");
            if (!string.IsNullOrEmpty(e.Result.Text))
            {
                var outputWithPerson = $"{_person}: {e.Result.Text}";
                Console.WriteLine(outputWithPerson);
                _streamWriter.WriteLine(String.IsNullOrEmpty(_person) ? e.Result.Text : outputWithPerson);
            }
        }

        /// <summary>
        ///     Logs Canceled events
        ///     And sets the TaskCompletionSource to 0, in order to trigger Recognition Stop
        /// </summary>
        private static void CanceledEventHandler(SpeechRecognitionCanceledEventArgs e, RecognizerType rt,
            TaskCompletionSource<int> source)
        {
            source.TrySetResult(0);
            Console.WriteLine("--- recognition canceled ---");
            Console.WriteLine($"CancellationReason: {e.Reason.ToString()}. ErrorDetails: {e.ErrorDetails}.");
        }

        /// <summary>
        ///     Session started event handler.
        /// </summary>
        private static void SessionStartedEventHandler(SessionEventArgs e, RecognizerType rt)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Speech recognition: Session started event: {0}.",
            e.ToString()));
        }

        /// <summary>
        ///     Session stopped event handler. Set the TaskCompletionSource to 0, in order to trigger Recognition Stop
        /// </summary>
        private static void SessionStoppedEventHandler(SessionEventArgs e, RecognizerType rt, TaskCompletionSource<int> source)
        {
            Console.WriteLine(
                string.Format(CultureInfo.InvariantCulture, "Speech recognition: Session stopped event: {0}.",
                    e.ToString()));
            source.TrySetResult(0);
        }

        private static void SpeechDetectedEventHandler(RecognitionEventArgs e, RecognizerType rt, string eventType)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Speech recognition: Speech {0} detected event: {1}.",
                eventType, e.ToString()));
        }
    }
}