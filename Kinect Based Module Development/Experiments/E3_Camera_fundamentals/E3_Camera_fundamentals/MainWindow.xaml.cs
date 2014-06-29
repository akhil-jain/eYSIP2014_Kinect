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

        KinectSensor newSensor, sensor;
        KinectSensorChooser chooser = new KinectSensorChooser();

        System.IO.Ports.SerialPort myport = new System.IO.Ports.SerialPort("COM2");


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            chooser.KinectChanged += chooser_KinectChanged;
            chooserUI.KinectSensorChooser = chooser;
            chooser.Start();
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
                label1.Content = "Live Stream Video (320 x 240)";

            }
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
                
                Console.WriteLine(colorFrame.PixelDataLength/2 - colorFrame.Width/2);
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
            StopKinect(chooser.Kinect);

        }
    }
}
