using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Kinect;
using System.IO;
using System.Text;

namespace E6_Skeleton_Tracking_firebird_front_back
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor sensor = KinectSensor.KinectSensors[0];

        #region "Variables"
        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;


        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;
        #endregion

        System.IO.Ports.SerialPort myport = new System.IO.Ports.SerialPort("COM2");
        public static Skeleton skeleton = new Skeleton();
        Joint rightHand = skeleton.Joints[JointType.HandRight];
        static double oldvalue = 1.5;
        float newvalue;
        int countaway = 0, counttowards = 0, count1 = 1, count2 = 0;

        public MainWindow()
        {
            InitializeComponent();
            //After Initialization subscribe to the loaded event of the form 
            Loaded += MainWindow_Loaded;

            //After Initialization subscribe to the unloaded event of the form
            //We use this event to stop the sensor when the application is being closed.
            Unloaded += MainWindow_Unloaded;


        }

        void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            //stop the Sestor 
            sensor.Stop();

        }


        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            //Create a Drawing Group that will be used for Drawing 
            this.drawingGroup = new DrawingGroup();

            //Create an image Source that will display our skeleton
            this.imageSource = new DrawingImage(this.drawingGroup);

            //Display the Image in our Image control
            Image.Source = imageSource;

            try
            {
                //Check if the Sensor is Connected
                if (sensor.Status == KinectStatus.Connected)
                {
                    //Start the Sensor
                    sensor.Start();
                    //Tell Kinect Sensor to use the Default Mode(Human Skeleton Standing) || Seated(Human Skeleton Sitting Down)
                    sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                    //Subscribe to te  Sensor's SkeletonFrameready event to track the joins and create the joins to display on our image control
                    sensor.SkeletonFrameReady += sensor_SkeletonFrameReady;
                    //nice message with Colors to alert you if your sensor is working or not
                    Message.Text = "  Kinect Ready";
                    Message.Background = new SolidColorBrush(Colors.Green);
                    Message.Foreground = new SolidColorBrush(Colors.White);

                    // Turn on the skeleton stream to receive skeleton frames
                    this.sensor.SkeletonStream.Enable();
                }
                else if (sensor.Status == KinectStatus.Disconnected)
                {
                    //nice message with Colors to alert you if your sensor is working or not
                    Message.Text = " Kinect Sensor is not Connected";
                    Message.Background = new SolidColorBrush(Colors.Orange);
                    Message.Foreground = new SolidColorBrush(Colors.Black);

                }
                else if (sensor.Status == KinectStatus.NotPowered)
                {//nice message with Colors to alert you if your sensor is working or not
                    Message.Text = " Kinect Sensor is not Powered";
                    Message.Background = new SolidColorBrush(Colors.Red);
                    Message.Foreground = new SolidColorBrush(Colors.Black);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);

            }
        }
        /// <summary>
        //When the Skeleton is Ready it must draw the Skeleton
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            //declare an array of Skeletons
            Skeleton[] skeletons = new Skeleton[1];

            //Opens a SkeletonFrame object, which contains one frame of skeleton data.
            using (SkeletonFrame skeletonframe = e.OpenSkeletonFrame())
            {
                //Check if the Frame is Indeed open 
                if (skeletonframe != null)
                {

                    skeletons = new Skeleton[skeletonframe.SkeletonArrayLength];
                    skeletons = new Skeleton[skeletonframe.SkeletonArrayLength];

                    // Copies skeleton data to an array of Skeletons, where each Skeleton contains a collection of the joints.
                    skeletonframe.CopySkeletonDataTo(skeletons);

                    double rightX = rightHand.Position.X;
                    double rightY = rightHand.Position.Y;
                    double rightZ = rightHand.Position.Z;

                    Skeleton skeleton = skeletons[0];
                    Joint j = skeleton.Joints[JointType.HipCenter];
                    newvalue = j.Position.Z;

                        if (newvalue < oldvalue)
                            counttowards++;
                        else
                            countaway++;

                    if (countaway > counttowards)
                    {
                        count1++;
                        if (count1 > 10)
                        {
                            countaway = 0;
                            counttowards = 0;
                            count1 = 0;
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
                                        label4.Content = "";
                                        myport.Open();
                                    }
                                }
                                else
                                {
                                    label4.Content = "COM port does not exist";
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
                        count2++;
                        if (count2 > 10)
                        {
                            countaway = 0;
                            counttowards = 0;
                            count2 = 0;
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
                                        label4.Content = "";
                                        myport.Open();
                                    }
                                }
                                else
                                {
                                    label4.Content = "COM port does not exist";
                                }
                            }
                            catch (System.InvalidOperationException)
                            {
                                MessageBox.Show("Unable to write to COM port");
                            }
                        }
                    }

                    oldvalue = newvalue;

                    label1.Content = " X - " + j.Position.X;
                    label2.Content = " Y - " + j.Position.Y;
                    label3.Content = " Z - " + j.Position.Z;
                    label4.Content = "Coordinates of Hip Center";

                    // Copies skeleton data to an array of Skeletons, where each Skeleton contains a collection of the joints.
                    skeletonframe.CopySkeletonDataTo(skeletons);
                    //draw the Skeleton based on the Default Mode(Standing), "Seated"
                    if (sensor.SkeletonStream.TrackingMode == SkeletonTrackingMode.Default)
                    {
                        //Draw standing Skeleton
                        DrawStandingSkeletons(skeletons);
                    }
                    else if (sensor.SkeletonStream.TrackingMode == SkeletonTrackingMode.Seated)
                    {
                        //Draw a Seated Skeleton with 10 joints
                        DrawSeatedSkeletons(skeletons);
                    }
                }
            }
        }

        //Thi Function Draws the Standing  or Default Skeleton
        private void DrawStandingSkeletons(Skeleton[] skeletons)
        {

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                //Draw a Transparent background to set the render size or our Canvas
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                //If the skeleton Array has items 
                if (skeletons.Length != 0)
                {
                    //Loop through the Skeleton joins
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);


                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(this.centerPointBrush,
                                           null,
                                           this.SkeletonPointToScreen(skel.Position), BodyCenterThickness, BodyCenterThickness);

                        }

                    }


                }

                //Prevent Drawing outside the canvas 
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));

            }
        }


        private void DrawSeatedSkeletons(Skeleton[] skeletons)
        {

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                //Draw a Transparent background to set the render size 
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);


                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(this.centerPointBrush,
                                           null,
                                           this.SkeletonPointToScreen(skel.Position), BodyCenterThickness, BodyCenterThickness);

                        }

                    }


                }

                //Prevent Drawing outside the canvas 
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));

            }
        }



        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked || joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred && joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;

            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }


        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }
    }
}
