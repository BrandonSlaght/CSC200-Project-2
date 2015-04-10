using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Timers;
using Microsoft.Kinect;

namespace KinectSkeletonData
{
    class Program
    {
        public static KinectSensor sensor;
        public static Timer timer;
        public static bool reset = true;
        public static int calibrated = 3;
        public static float[,] calibration= new float[3,3];//Usage:[0,1,2][,,] = left, center, right.  [,,][0,1,2] = x, y, z

        static void Main(string[] args)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }

            if (null != sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                sensor.SkeletonStream.Enable();



                // Add an event handler to be called whenever there is new color frame data
                sensor.SkeletonFrameReady += sensor_SkeletonFrameReady;

                // Start the sensor!
                try
                {
                    sensor.Start();
                    timer = new Timer(5000);
                    timer.AutoReset = true;
                    timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
                    timer.Start();
                    sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                catch (IOException)
                {
                    sensor = null;
                }
            }

            if (null == sensor)
            {
                Console.Write("No sensor");
            }

            Console.ReadKey();
        }

        static void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("Timer reset");
            reset = true;
        }

        static void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            if (calibrated<3)
            {
                Skeleton[] calskeletons = new Skeleton[0];

                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (skeletonFrame != null)
                    {
                        calskeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(calskeletons);
                    }
                }
                foreach (Skeleton calskel in calskeletons)
                {
                    if (calskel.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        Joint left = calskel.Joints[JointType.ShoulderLeft];
                        Joint head = calskel.Joints[JointType.Head];
                        Joint right = calskel.Joints[JointType.ShoulderRight];
                        if (calibrated == 0)
                        {
                            calibration[0, 0] = left.Position.X;
                            calibration[0, 1] = left.Position.Y;
                            calibration[0, 2] = left.Position.Z;
                        }
                        else
                        {
                            for (int x = 0; x < calibration.GetLength(0); x += 1)
                            {
                                for (int y = 0; x < calibration.GetLength(1); y += 1)
                                {

                                }
                            }
                        }
                    }
                }
                calibrated++;
            }




            else if (reset)
            {
                Skeleton[] skeletons = new Skeleton[0];

                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (skeletonFrame != null)
                    {
                        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);
                    }
                }

                Console.WriteLine(skeletons.Length.ToString() + " skeleton frames:");

                foreach (Skeleton skel in skeletons)
                {
                    if (skel.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        //Console.WriteLine("tracked");
                        Joint head = skel.Joints[JointType.Head];
                        Console.WriteLine(head.Position.X.ToString() + head.Position.Y.ToString() + head.Position.Z.ToString());
                    }
                    else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                    {
                        Console.WriteLine("position only");
                    }
                    else
                        Console.WriteLine("Not tracked");
                }

                reset = false;
            }
        }
    }
}
