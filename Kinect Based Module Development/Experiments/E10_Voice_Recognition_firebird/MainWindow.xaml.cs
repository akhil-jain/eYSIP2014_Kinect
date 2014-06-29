/*
 * This experiment is used for voice recognition and deciphering 
 * the commands given. This is followed by interfacing to firbird V
 * to follow the commands.
 * The commands include : 
 * "Forward" - To move the robot forward
 * "Back"    - To move the robot backward
 * "Left"    - To move the robot left
 * "Right"   - To move move the robot right 
 * The grammar is available in SpeechGrammer.xml file
 *  * the commands transmitted serially are as follows :
 * 2 - backward
 * 4 - left
 * 5 - stop
 * 6 - right
 * 8 - forward
 */

namespace Microsoft.Samples.Kinect.SpeechBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Windows.Documents;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using Microsoft.Speech.AudioFormat;
    using Microsoft.Speech.Recognition;

    // Interaction logic for MainWindow.xaml
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "In a full-fledged application, the SpeechRecognitionEngine object should be properly disposed. For the sake of simplicity, we're omitting that code in this sample.")]
    public partial class MainWindow : Window
    {      
        // Serial port to transfer data serially
        // This may be different everytime the user plugs in the zigbee module hence appropriate changes are required
        // Verify the COM port on XCTU software and use the same here
        System.IO.Ports.SerialPort myport = new System.IO.Ports.SerialPort("COM2");
        // Resource key for medium-gray-colored brush.
        private const string MediumGreyBrushKey = "MediumGreyBrush";       
        // Active Kinect sensor.
        private KinectSensor sensor;
        // Speech recognition engine using audio data from Kinect.
        private SpeechRecognitionEngine speechEngine;
        // List of all UI span elements used to select recognized text.
        private List<Span> recognitionSpans;
        // Initializes a new instance of the MainWindow class.

        public MainWindow()
        {
            InitializeComponent();
        }
        
        // Gets the metadata for the speech recognizer (acoustic model) most suitable to
        // process audio from Kinect device.
        // RecognizerInfo if found, null otherwise.

        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }
            
            return null;
        }

        // Execute initialization tasks.

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                try
                {
                    // Start the sensor
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    // Some other application is streaming from the same Kinect sensor
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
                return;
            }

            RecognizerInfo ri = GetKinectRecognizer();

            if (null != ri)
            {
                recognitionSpans = new List<Span> { forwardSpan, backSpan, stopSpan , rightSpan, leftSpan };
                //Add the spans relating to the commands to be displayed on the main window
                this.speechEngine = new SpeechRecognitionEngine(ri.Id);

                // Create a grammar from grammar definition XML file.
                using (var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(Properties.Resources.SpeechGrammar)))
                {
                    var g = new Grammar(memoryStream);
                    speechEngine.LoadGrammar(g);
                }

                speechEngine.SpeechRecognized += SpeechRecognized;
                speechEngine.SpeechRecognitionRejected += SpeechRejected;

                //For long recognition sessions (a few hours or more), it may be beneficial to turn off adaptation of the acoustic model. 
                //This will prevent recognition accuracy from degrading over time.

                speechEngine.SetInputToAudioStream(sensor.AudioSource.Start(), new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                this.statusBarText.Text = Properties.Resources.NoSpeechRecognizer;
            }
        }

        //this function disables the colorstream and stops the audiosource and the sensor itself
        //it is essential to call this function before terminating the program
        //if not called it may lead to unhandled exceptions        
        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.AudioSource.Stop();
                //stop audio stream
                this.sensor.Stop();
                //stop the kinect sensor
                this.sensor = null;
            }

            if (null != this.speechEngine)
            {
                this.speechEngine.SpeechRecognized -= SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected -= SpeechRejected;
                this.speechEngine.RecognizeAsyncStop();
            }
        }

        // Remove any highlighting from recognition instructions.
        private void ClearRecognitionHighlights()
        {
            foreach (Span span in recognitionSpans)
            {
                span.Foreground = (Brush)this.Resources[MediumGreyBrushKey];
                span.FontWeight = FontWeights.Normal;
            }
        }

        // Handler for recognized speech events.
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Speech utterance confidence below which we treat speech as if it hadn't been heard
            const double ConfidenceThreshold = 0.25;

            ClearRecognitionHighlights();
            // to clear the color on the spans
            if (e.Result.Confidence >= ConfidenceThreshold)
            {
                switch (e.Result.Semantics.Value.ToString())
                {
                    case "FORWARD":
                        // When the user speaks "Forward"
                        try
                        {
                            //Check if the serial port is available. 
                            //This may be different everytime the user plugs in the zigbee module hence appropriate changes are required
                            if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                            {
                                //check if the port is open
                                if (myport.IsOpen)
                                {
                                    // 5 - stop, 8 - forward
                                    myport.WriteLine("58");
                                    //display the same on the label
                                    label1.Content = " Direction of Firebird - Forward";
                                }
                                else
                                {
                                    //if the port is closed then open it
                                    myport.Open();
                                }
                            }
                            else
                            {
                                //do nothing
                            }
                        }
                        catch (System.InvalidOperationException)
                        {
                            //when an exception exists a message box is shown
                            MessageBox.Show("Unable to write to COM port");
                        }                          
                        forwardSpan.Foreground = Brushes.DeepSkyBlue;
                        forwardSpan.FontWeight = FontWeights.Bold;
                        //Highlighting the spans
                            break;

                    case "BACKWARD":
                            try
                            {
                                // When the user speaks "back"
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    //Check if the serial port is available. 
                                    //This may be different everytime the user plugs in the zigbee module hence appropriate changes are required
                                    if (myport.IsOpen)
                                    {
                                        //if the serial port is open 
                                        myport.WriteLine("52");
                                        // 5 - stop, 2 - back
                                        label1.Content = " Direction of Firebird - Backward";
                                        //display the same on a label
                                    }
                                    else
                                    {
                                        //if the port is closed then open it
                                        myport.Open();
                                    }
                                }
                                else
                                {
                                    //do nothing
                                }
                            }
                            catch (System.InvalidOperationException)
                            {
                                //when an exception exists a message box is shown
                                MessageBox.Show("Unable to write to COM port");
                            }
                        backSpan.Foreground = Brushes.DeepSkyBlue;
                        backSpan.FontWeight = FontWeights.Bold;                        
                          break;

                    case "STOP" :
                          // When the user speaks "stop"
                        stopSpan.Foreground = Brushes.DeepSkyBlue;
                        stopSpan.FontWeight = FontWeights.Bold;
                        //Highlighting the spans
                           try
                            {
                                //Check if the serial port is available. 
                                //This may be different everytime the user plugs in the zigbee module hence appropriate changes are required
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    if (myport.IsOpen)
                                    {
                                        //if the serial port is open 
                                        myport.WriteLine("5");
                                        // 5 - stop
                                        label1.Content = " Function of Firebird - Stop";
                                    }
                                    else
                                    {
                                        //if the port is closed then open it
                                        myport.Open();
                                    }
                                }
                                else
                                {
                                    //do nothing
                                }
                            }
                            catch (System.InvalidOperationException)
                            {
                                //when an exception exists a message box is shown
                                MessageBox.Show("Unable to write to COM port");
                            }                       
                            break;

                    case "LEFT":
                            // When the user speaks "left"
                        try
                        {
                            //Check if the serial port is available. 
                            //This may be different everytime the user plugs in the zigbee module hence appropriate changes are required
                            if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                            {
                                if (myport.IsOpen)
                                {
                                    //if the serial port is open 
                                    myport.WriteLine("54");
                                    // 5 - stop, 4 - left
                                    label1.Content = " Direction of Firebird - Left";
                                    //display the same on the label
                                }
                                else
                                {
                                    //if the port is closed then open it
                                    myport.Open();
                                }
                            }
                            else
                            {
                                //do nothing
                            }
                        }
                        catch (System.InvalidOperationException)
                        {
                            //when an exception exists a message box is shown
                            MessageBox.Show("Unable to write to COM port");
                        }
                        leftSpan.Foreground = Brushes.DeepSkyBlue;
                        leftSpan.FontWeight = FontWeights.Bold;
                        //Highlighting the spans
                        break;

                    case "RIGHT":
                        //when the user speaks "right"
                        try
                        {
                            if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                            {
                                //Check if the serial port is available. 
                                //This may be different everytime the user plugs in the zigbee module hence appropriate changes are required
                                if (myport.IsOpen)
                                {
                                    //if the port is open
                                    myport.WriteLine("56");
                                    // 5 - stop, 6 - right
                                    label1.Content = " Direction of Firebird - Right";
                                    //display the same on the label
                                }
                                else
                                {
                                    //if the port is closed then open it
                                    myport.Open();
                                }
                            }
                            else
                            {
                                //do nothing
                            }
                        }
                        catch (System.InvalidOperationException)
                        {
                            //when an exception exists a message box is shown
                            MessageBox.Show("Unable to write to COM port");
                        }
                        rightSpan.Foreground = Brushes.DeepSkyBlue;
                        rightSpan.FontWeight = FontWeights.Bold;  
                        //Highlighting the spans
                        break;
                }
            }
        }

        // Handler for rejected speech events.
        private void SpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            ClearRecognitionHighlights();
        }
    }
}