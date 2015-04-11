using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Timers;
using Microsoft.Kinect;

namespace KinectSkeletonData
{
    enum State { Calibrate, Run, Test };

    class Program
    {
        public static State state = State.Calibrate;
        public static KinectSensor sensor;
        public static Timer timer;
        public static bool reset = true;
        public static int calibrated = 3;
        public static float[,] calibration= new float[3,3];//Usage:[0,1,2][,,] = left, center, right.  [,,][0,1,2] = x, y, z

        static void Main(string[] args)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
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

                // Add an event handler to be called whenever there is new skeleton frame data
                sensor.SkeletonFrameReady += sensor_SkeletonFrameReady;

                // Start the sensor and the timer
                try
                {
                    timer = new Timer(1000);
                    timer.AutoReset = true;
                    timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
                    timer.Start();
                    sensor.Start();
                    sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                catch (IOException)
                {
                    Console.Write("Sensor failed to start.");
                }
            }

            if (null == sensor)
            {
                Console.Write("No sensor.");
            }

            Console.ReadKey();
        }

        static void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            reset = true;
        }

        static void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            // We only use the frame if the timer reset (this is to limit the frame rate)
            if (reset)
            {
                // Here we get all 6 skeletons (even if they are not all being used)
                Skeleton[] skeletons = new Skeleton[0];
                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (skeletonFrame != null)
                    {
                        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);
                    }
                }

                // What we do with the skeleton data is determined by the state the program is in
                switch (state)
                {
                    // If the program is calibrating, we will get the calibration data from this frame
                    case State.Calibrate:
                        if (calibrated < 3)
                        {
                            foreach (Skeleton skel in skeletons)
                            {
                                if (skel.TrackingState == SkeletonTrackingState.Tracked)
                                {
                                    Joint left = skel.Joints[JointType.ShoulderLeft];
                                    Joint head = skel.Joints[JointType.Head];
                                    Joint right = skel.Joints[JointType.ShoulderRight];
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
                        break;

                    // If the program is running, we will determin if the use has good or bad posture
                    case State.Run:
                        break;

                    // This state is for testing.  Here we put whatever we want and it will not interfere with the rest of the program.
                    // Use this state by setting state = State.Testing where it is declared.
                    case State.Test:
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
                        break;

                    default:
                        break;
                }
                reset = false;
            }
        }
    }
}
