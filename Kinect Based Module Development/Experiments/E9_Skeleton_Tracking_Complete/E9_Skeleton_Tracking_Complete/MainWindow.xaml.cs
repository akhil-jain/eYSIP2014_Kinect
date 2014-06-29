/*
 * This experiment is used for simple gesture recognition 
 * by tracking the angles between the various body joints.
 * The gestures are as follows :
 * stand still - stop gesture
 * raise right hand - turn right gesture
 * raise left hand - turn left gesture
 * lean forward - forward gesture
 * raise both hands - back gesture
 * the commands transmitted serially are as follows :
 * 2 - backward
 * 4 - left
 * 5 - stop
 * 6 - right
 * 8 - forward
 * 9 - buzzer on
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Kinect;
using System.IO;
using System.Text;

namespace E9_Skeleton_Tracking_Complete
{
    // Interaction logic for MainWindow.xaml
    public partial class MainWindow : Window
    {
        KinectSensor sensor = KinectSensor.KinectSensors[0];
        #region "Variables"
        // Thickness of body center ellipse
        private const double BodyCenterThickness = 10;
        // Thickness of clip edge rectangles
        private const double ClipBoundsThickness = 10;
        // Brush used to draw skeleton center point
        private readonly Brush centerPointBrush = Brushes.Blue;
        // Brush used for drawing joints that are currently tracked
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        // Brush used for drawing joints that are currently inferred
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        // Pen used for drawing bones that are currently tracked
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);
        // Pen used for drawing bones that are currently inferred
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);
        // Drawing image that we will display
        private DrawingImage imageSource;
        // Thickness of drawn joint lines
        private const double JointThickness = 3;
        // Drawing group for skeleton rendering output
        private DrawingGroup drawingGroup;
        // Width of output drawing
        private const float RenderWidth = 640.0f;
        // Height of our output drawing
        private const float RenderHeight = 480.0f;
        #endregion

        // Serial port to transfer data serially
        // This may be different everytime the user plugs in the zigbee module hence appropriate changes are required
        // Verify the COM port on XCTU software and use the same here
        System.IO.Ports.SerialPort myport = new System.IO.Ports.SerialPort("COM2");
        public static Skeleton skeleton = new Skeleton();

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

        /*This function is used to get the angle between 3 joints input to the function
        Vector algebra in 3D is used for the purpose.
        The variable joint2 is used a fulcrum.
        The absolute x, y and z coordinates of the joints obtainted.
        their relative distance is calculated.
        using dot product concept
        angle cos(x) = (vect(A).vect(B)) / (mag(vect(A)) x mag(vect(B)))
         */
        double GetAngle(Joint joint1, Joint joint2, Joint joint3)
        {
            double x1 = joint1.Position.X;
            // x coordinate of joint1 
            double y1 = joint1.Position.Y;
            // y coordinate of joint1 
            double z1 = joint1.Position.Z;
            // z coordinate of joint1 

            double x2 = joint2.Position.X;
            // x coordinate of joint2 
            double y2 = joint2.Position.Y;
            // y coordinate of joint2 
            double z2 = joint2.Position.Z;
            // z coordinate of joint2 

            double x3 = joint3.Position.X;
            // x coordinate of joint3 
            double y3 = joint3.Position.Y;
            // y coordinate of joint3 
            double z3 = joint3.Position.Z;
            // z coordinate of joint3 

            double shiftx1 = x1 - x2;
            // x coordinate - Relative position of joint1 with respect to joint2
            double shifty1 = y1 - y2;
            // y coordinate - Relative position of joint1 with respect to joint2
            double shiftz1 = z1 - z2;
            // z coordinate - Relative position of joint1 with respect to joint2

            double shiftx2 = x3 - x2;
            // x coordinate - Relative position of joint3 with respect to joint2
            double shifty2 = y3 - y2;
            // y coordinate - Relative position of joint3 with respect to joint2
            double shiftz2 = z3 - z2;
            // z coordinate - Relative position of joint3 with respect to joint2

            double product = shiftx1 * shiftx2 + shifty1 * shifty2 + shiftz1 * shiftz2;
            // dot product
            double mag1 = Math.Abs(Math.Sqrt(Math.Pow(shiftx1, 2) + Math.Pow(shifty1, 2) + Math.Pow(shiftz1, 2)));
            // magnitude of vector 1
            double mag2 = Math.Abs(Math.Sqrt(Math.Pow(shiftx2, 2) + Math.Pow(shifty2, 2) + Math.Pow(shiftz2, 2)));
            // magnitude of Vector 2
            double temp = product / (mag1 * mag2);
            double angle = (180 / Math.PI) * Math.Acos(temp);
            return angle;
        }

        //get the skeleton closest to the Kinect sensor
        private static Skeleton GetPrimarySkeleton(Skeleton[] skeletons)
        {
            Skeleton skeleton = null;
            if (skeletons != null)
            {
                for (int i = 0; i < skeletons.Length; i++)
                {
                    // if skeletons exits in the frame
                    if (skeletons[i].TrackingState == SkeletonTrackingState.Tracked)
                    {
                        if (skeleton == null)
                        {
                            skeleton = skeletons[i];
                        }
                        else
                        {
                            if (skeleton.Position.Z > skeletons[i].Position.Z)
                            {
                                skeleton = skeletons[i];
                            }
                        }
                    }
                }
            }
            return skeleton;
        }

        void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            //declare an array of Skeletons
            Skeleton[] skeletons = new Skeleton[6];

            //Opens a SkeletonFrame object, which contains one frame of skeleton data.
            using (SkeletonFrame skeletonframe = e.OpenSkeletonFrame())
            {
                //Check if the Frame is Indeed open 
                if (skeletonframe != null)
                {

                    skeletons = new Skeleton[skeletonframe.SkeletonArrayLength];

                    // Copies skeleton data to an array of Skeletons, where each Skeleton contains a collection of the joints.
                    skeletonframe.CopySkeletonDataTo(skeletons);

                    Skeleton skeleton = GetPrimarySkeleton(skeletons);
                    if (skeleton != null)
                    {
                        // Get 3 joints - rightelbow, rightwrist and rightshoulder
                        Joint rightelbow = skeleton.Joints[JointType.ElbowRight];
                        Joint rightwrist = skeleton.Joints[JointType.WristRight];
                        Joint rightshoulder = skeleton.Joints[JointType.ShoulderRight];
                        // Get 3 joints - leftelbow, leftwrist and leftshoulder
                        Joint leftelbow = skeleton.Joints[JointType.ElbowLeft];
                        Joint leftwrist = skeleton.Joints[JointType.Spine];
                        Joint leftshoulder = skeleton.Joints[JointType.ShoulderLeft];
                        // Get 2 joints - hipcenter, head and spine
                        Joint hipcenter = skeleton.Joints[JointType.HipCenter];
                        Joint head = skeleton.Joints[JointType.Head];
                        Joint spine = skeleton.Joints[JointType.Spine];
                        // get angles between the 3 sets defined
                        double rightangle = GetAngle(spine, rightshoulder, rightelbow);
                        double leftangle = GetAngle(leftwrist, leftshoulder, leftelbow);
                        double angle = GetAngle(hipcenter, spine, head);

                        // based on the angles obtained, gestures are defined 
                        label4.Content = " Action Performed by Firebird -  ";
                        if (hipcenter.Position.Z - rightwrist.Position.Z > 0.35)
                        {
                            // Punch gesture
                            try
                            {
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    if (myport.IsOpen)
                                    {
                                        myport.WriteLine("57");
                                        label2.Content = " PUNCH !!!";
                                    }
                                    else
                                    {
                                        label2.Content = "";
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
                            // lean forward for 'move forward' gesture
                        else if (angle > 165)
                        {
                            try
                            {
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    if (myport.IsOpen)
                                    {
                                        myport.WriteLine("958");
                                        label2.Content = " Forward";

                                    }
                                    else
                                    {
                                        label2.Content = "";
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
                            // stand straight with hands down for 'stop' gesture
                        else if (rightangle < 150 && leftangle < 150 && rightangle > 100 && leftangle > 100)
                        {
                            try
                            {
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    if (myport.IsOpen)
                                    {
                                        myport.WriteLine("95");
                                        label2.Content = " Stop";

                                    }
                                    else
                                    {
                                        label2.Content = "";
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
                            // raise both hands for 'back' gesture
                        else if (rightangle > 150 && leftangle > 150)
                        {
                            try
                            {
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    if (myport.IsOpen)
                                    {
                                        myport.WriteLine("952");
                                        label2.Content = " Back";

                                    }
                                    else
                                    {
                                        label2.Content = "";
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
                            // raise right hand parallel to ground for 'right' gesture
                        else if (rightangle > 100 && rightangle < 150)
                        {
                            try
                            {
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    if (myport.IsOpen)
                                    {
                                        myport.WriteLine("956");
                                        label2.Content = " Right";

                                    }
                                    else
                                    {
                                        label2.Content = "";
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
                        // raise right hand parallel to ground for 'left' gesture
                        else if (leftangle > 100 && leftangle < 150)
                        {
                            try
                            {
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    if (myport.IsOpen)
                                    {
                                        myport.WriteLine("954");
                                        label2.Content = " Left";

                                    }
                                    else
                                    {
                                        label2.Content = "";
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

                        else if (rightangle < 100 && leftangle < 100)
                        {
                            try
                            {
                                if (System.IO.Ports.SerialPort.GetPortNames().Contains("COM2"))
                                {
                                    if (myport.IsOpen)
                                    {
                                        myport.WriteLine("95");
                                        label2.Content = " Stop";

                                    }
                                    else
                                    {
                                        label2.Content = "";
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

        /// Draws indicators to show which edges are clipping skeleton data

        // Draws indicators to show which edges are clipping skeleton data
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            // if the skeleton goes out of frame at the bottom
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }
            // if the skeleton goes out of frame at the top
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }
            // if the skeleton goes out of frame at the left
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }
            // if the skeleton goes out of frame at the right
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


        // Maps a SkeletonPoint to lie within our render space and converts to Point
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }
    }
}
