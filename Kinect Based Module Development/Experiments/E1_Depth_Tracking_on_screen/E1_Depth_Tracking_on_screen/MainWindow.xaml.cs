/*
 This experiment is used for tracking the depth of each pixel in a 320 x 240 frame captured by the kinect.
 The kinect captures 30 frames per second.
 The distance of the center pixel is displayed on the main window.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;

namespace WpfApplication2
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //Initialization process when main window is loaded, ie. when the program is run
        }

        KinectSensor newSensor, sensor;
        //Variables of type KinectSensor
        KinectSensorChooser chooser = new KinectSensorChooser(); 
        //Kinect chooser for determining the state of the kinect (disconnected, ready, not powered on etc)

        //This function is used for attaching a reference to the kinect sensor 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            chooser.KinectChanged += chooser_KinectChanged;
            chooserUI.KinectSensorChooser = chooser;
            chooser.Start();
            //Initialize the Chooser to find a Kinect to use
        }


        /* This function is used to stop any previous instances of a running kinect sensor
         * and starts a new reference under the name NewSensor.
         * if a kinect sensor is connected to the PC then its depth stream is enabled
         * to receive the depth data and the sensor is started. Exceptions may occur
         * during the process of starting the sensor which is taken care of in the try-
         * catch block.
         */ 

        void chooser_KinectChanged(object sender, KinectChangedEventArgs e)
        {
            KinectSensor oldSensor = (KinectSensor)e.OldSensor;
            //Get reference to any old sensor
            StopKinect(oldSensor);
            //Stop the previous instances if any
            KinectSensor newSensor = (KinectSensor)e.NewSensor;
            //Get a reference to a new (latest) kinect sensor
            if (newSensor == null)
            {
                //If no kinect sensor is connected simply return
                return;
            }
            //If a kinect sensor is attached then ...
            newSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
            //Enable the depth stream to recerive depth data of the frame of resolution 320 x 240
            newSensor.AllFramesReady += newSensor_AllFramesReady;
            //Initializing the AllFramesReady event
            try
            {
                newSensor.Start();
                //Start the kinect sensor
            }
            catch (System.IO.IOException)
            {
                //Some other application is streaming from the same Kinect sensor
                chooser.TryResolveConflict();
                //Allow chooser to resolve conflict if any
            }
        }

        //This function is a response to the event defined previously
        //This is an event handling code for the AllFramesReady event
        //Triggered when a new frame is available from the kinect
        void newSensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            //start the depth frame
            {
                if (depthFrame == null)
                {
                    return;
                }
                //if no frames are available then return because there is nothing to process
                byte[] pixels = GenerateColoredBytes(depthFrame);
                //array of bytes for the pixels
                int stride = depthFrame.Width * 4;
                //the data is in the format - Blue,Green,Red,Blank hence stride is width x4
                image1.Source = BitmapSource.Create(depthFrame.Width, depthFrame.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
                //video frame
            }
        }

        //This function reveives the depth frame as an input and based on the depth, 
        //it assigns Blue color for pixels less than 900mm away (minimum distance from kinect must be 800mm)
        //Red color for pixels in the range 900mm to 2000mm
        //Green color for pixels at a distance greater than 2000mm in the frame
        //it also displays the center pixel distance from the kinect

        private byte[] GenerateColoredBytes(DepthImageFrame depthFrame)
        {
            short[] rawDepthData = new short[depthFrame.PixelDataLength];
            //array of short for the raw data storage
            depthFrame.CopyPixelDataTo(rawDepthData);
            //copy the pixels data to the rawDepthData array
            Byte[] pixels = new byte[depthFrame.Height * depthFrame.Width * 4];
            //the data is in the format - Blue,Green,Red,Blank hence length of the array is width x height x 4

            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;
            //these constants are only for simple understanding of color assignments to pixels
            int centerpixel = rawDepthData[(depthFrame.Height * depthFrame.Width / 2) - (depthFrame.Width / 2)] >> DepthImageFrame.PlayerIndexBitmaskWidth;
            
            for (int depthIndex = 0, colorIndex = 0; depthIndex < rawDepthData.Length && colorIndex < pixels.Length; depthIndex++, colorIndex += 4)
            {
                int player = rawDepthData[depthIndex] & DepthImageFrame.PlayerIndexBitmask;
                //based on the depth the player index is extracted (there may be multiple players in the frame)
                int depth = rawDepthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                //get depth of the player
                if (depth <= 900)
                {
                    pixels[colorIndex + BlueIndex] = 255; //Blue color
                    pixels[colorIndex + RedIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                }
                else if (depth > 900 && depth <= 2000)
                {
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + RedIndex] = 255; //Green color
                    pixels[colorIndex + GreenIndex] = 0;
                }
                else if (depth > 2000)
                {
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + RedIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 255; //Red color
                }
            }
            label1.Content = "Distance of center pixel from kinect = " + centerpixel + " mm";
            //display the distance of center pixel from the kinect
            return pixels;
            //return pixels array
        }

        //this function disables the colorstream and stops the audiosource and the sensor itself
        //it is essential to call this function before terminating the program
        //if not called it may lead to unhandled exceptions

        void StopKinect(KinectSensor sensor)
        {
            try
            {
                if (sensor != null)
                {
                    sensor.Stop();
                    //stop kinect sensor
                    sensor.AudioSource.Stop();
                    //stop audio source
                    sensor.ColorStream.Disable();
                    //disable the colorstream
                }
            }
            catch (System.NullReferenceException)
            {
                chooser.TryResolveConflict();
            }

        }
        // When the main window is closed the execution is terminated
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopKinect(chooser.Kinect);
        }
    }
}