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
        }

        int counttowards = 0, countaway = 0, newvalue = 1500, farcount = 0, nearcount = 0;
        static int oldvalue = 1500;

        KinectSensor newSensor, sensor;
        KinectSensorChooser chooser = new KinectSensorChooser();

        int countnear, countfar = 0, msgflag = 1;
        MessageBox msgBox;

        System.IO.Ports.SerialPort myport = new System.IO.Ports.SerialPort("COM2");

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            chooser.KinectChanged += chooser_KinectChanged;
            chooserUI.KinectSensorChooser = chooser;
            chooser.Start();
            if (myport.IsOpen == false)
            {
                myport.Open();

            }
        }

        void chooser_KinectChanged(object sender, KinectChangedEventArgs e)
        {
            KinectSensor oldSensor = (KinectSensor)e.OldSensor;
            StopKinect(oldSensor);
            KinectSensor newSensor = (KinectSensor)e.NewSensor;
            if (newSensor == null)
            {
                return;
            }
            newSensor.ColorStream.Enable();
            newSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
            newSensor.SkeletonStream.Enable();
            newSensor.AllFramesReady += newSensor_AllFramesReady;

            try
            {
                newSensor.Start();
            }
            catch (System.IO.IOException)
            {
                chooser.TryResolveConflict();
            }
        }

        void newSensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame == null)
                {
                    return;
                }
                byte[] pixels = GenerateColoredBytes(depthFrame);
                int stride = depthFrame.Width * 4;

                image1.Source = BitmapSource.Create(depthFrame.Width, depthFrame.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);

            }
        }

        private byte[] GenerateColoredBytes(DepthImageFrame depthFrame)
        {
            short[] rawDepthData = new short[depthFrame.PixelDataLength];
            depthFrame.CopyPixelDataTo(rawDepthData);
            Byte[] pixels = new byte[depthFrame.Height * depthFrame.Width * 4];

            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;
            int raw = rawDepthData.Length;

            newvalue = rawDepthData[(raw / 2) - (depthFrame.Width) / 2] >> DepthImageFrame.PlayerIndexBitmaskWidth;

           if (newvalue < oldvalue)
                    counttowards++;
                else
                    countaway++;

            if (countaway > counttowards)
            {
                farcount++;
                if (farcount > 10)
                {
                    countaway = 0;
                    counttowards = 0;
                    label2.Content = "Direction of firebird - Away from Kinect";
                    farcount = 0;
                    try
                    {
                        if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                        {
                            if (myport.IsOpen)
                            {
                                myport.WriteLine("52");
                            }
                            else
                            {
                                label1.Content = "";
                                myport.Open();
                            }
                        }
                        else
                        {
                            label1.Content = "COM port does not exist";
                            label2.Content = "";
                        }
                    }
                    catch (System.InvalidOperationException)
                    {
                        MessageBox.Show("Unable to write to COM port");
                    }
                }
            }

            else
            {
                nearcount++;
                if (nearcount > 10)
                {
                    countaway = 0;
                    counttowards = 0;
                    label2.Content = "Direction of firebird - Towards Kinect";
                    nearcount = 0;
                    try
                    {
                        if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                        {
                            if (myport.IsOpen)
                            {
                                myport.WriteLine("58");
                            }
                            else
                            {
                                label1.Content = "";
                                myport.Open();
                            }
                        }
                        else
                        {
                            label1.Content = "COM port does not exist";
                            label2.Content = "";
                        }
                    }
                    catch (System.InvalidOperationException)
                    {
                        MessageBox.Show("Unable to write to COM port");
                    }
                }
            }
            oldvalue = newvalue;
            label1.Content = "Distance of center pixel from kinect = " + newvalue + " mm";

            for (int depthIndex = 0, colorIndex = 0; depthIndex < rawDepthData.Length && colorIndex < pixels.Length; depthIndex++, colorIndex += 4)
            {
                int player = rawDepthData[depthIndex] & DepthImageFrame.PlayerIndexBitmask;
                int depth = rawDepthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                if (depth <= 900)
                {
                    pixels[colorIndex + BlueIndex] = 255;
                    pixels[colorIndex + RedIndex] = 1;
                    pixels[colorIndex + GreenIndex] = 2;
                }
                else if (depth > 900 && depth <= 2000)
                {
                    pixels[colorIndex + BlueIndex] = 3;
                    pixels[colorIndex + RedIndex] = 254;
                    pixels[colorIndex + GreenIndex] = 4;
                }
                else if (depth > 2000)
                {
                    pixels[colorIndex + BlueIndex] = 5;
                    pixels[colorIndex + RedIndex] = 6;
                    pixels[colorIndex + GreenIndex] = 253;
                }
            }
            return pixels;
        }


        void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                {
                    return;
                }
                byte[] pixels = new byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(pixels);

                int stride = colorFrame.Width * 4;
                image1.Source = BitmapSource.Create(colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
            }
        }

        void StopKinect(KinectSensor sensor)
        {
            try
            {
                if (sensor != null)
                {
                    sensor.Stop();
                    sensor.AudioSource.Stop();
                    sensor.ColorStream.Disable();
                }
            }
            catch (System.NullReferenceException)
            {
                chooser.TryResolveConflict();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }
    }
}
