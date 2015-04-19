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
        public static bool debug = true;
        public static State state = State.Calibrate;
        public static KinectSensor sensor;
        public static Timer timer;
        public static bool reset = true;
        public static int calibrated = 0;
        public static SkeletonPoint[] previousFrame = new SkeletonPoint[3];
        public static SkeletonPoint[] currentFrame = new SkeletonPoint[3];
        public static SkeletonPoint[,] calibrationPoints = new SkeletonPoint[3, 3];
        public static SkeletonPoint[] calibration = new SkeletonPoint[3];

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
                    timer = new Timer(2000);
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
                Console.Write("No sensor.");

            if (state == State.Calibrate)
                Console.WriteLine("Calibrating, please sit still with good posture.");

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
                Debug("Next Frame Ready");
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

                // Get the skeleton we want
                Skeleton ourSkel = null;
                Skeleton ourOtherSkel = null;
                foreach (Skeleton skel in skeletons)
                {
                    if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        if (ourSkel == null)
                            ourSkel = skel;
                        else
                            ourOtherSkel = skel;
                }
                if (ourSkel == null)
                {
                    Debug("No tracked skeleton.");
                    reset = true;
                    return;
                }
                else
                {
                    Debug("skeleton state = " + ourSkel.TrackingState);
                }

                // Get the joints we want
                Joint left = ourSkel.Joints[JointType.ShoulderLeft];
                Joint head = ourSkel.Joints[JointType.Head];
                Joint right = ourSkel.Joints[JointType.ShoulderRight];
                
                // We then set the previousFrame and currentFrame
                if (currentFrame[0] == null)
                {
                    // If this is the first frame, then we can't set the previousFrame
                    currentFrame[0] = left.Position;
                    currentFrame[1] = right.Position;
                    currentFrame[2] = head.Position;
                    // We will wait until the next frame so we have a previous frame
                    return;
                }
                previousFrame[0] = currentFrame[0];
                previousFrame[1] = currentFrame[1];
                previousFrame[2] = currentFrame[2];
                currentFrame[0] = left.Position;
                currentFrame[1] = right.Position;
                currentFrame[2] = head.Position;

                // What we do with the skeleton data is determined by the state the program is in
                switch (state)
                {
                    // If the program is calibrating, we will get the calibration data from this frame
                    case State.Calibrate:
                        float totalDist = 0;
                        totalDist += Distance(previousFrame[0], currentFrame[0]);
                        totalDist += Distance(previousFrame[1], currentFrame[1]);
                        totalDist += Distance(previousFrame[2], currentFrame[2]);
                        Debug("Total dist: " + totalDist);
                        if (totalDist < .005f)
                        {
                            if (calibrated < 3)
                            {
                                Debug("Calibration frame: " + (calibrated + 1) + "/3");
                                calibrationPoints[calibrated, 0] = left.Position;
                                calibrationPoints[calibrated, 1] = right.Position;
                                calibrationPoints[calibrated, 2] = head.Position;
                                calibrated++;
                            }
                            if (calibrated == 3)
                            {
                                calibration[0] = Center(calibrationPoints[0, 0], calibrationPoints[1, 0], calibrationPoints[2, 0]);
                                calibration[1] = Center(calibrationPoints[0, 1], calibrationPoints[1, 1], calibrationPoints[2, 1]);
                                calibration[2] = Center(calibrationPoints[0, 2], calibrationPoints[1, 2], calibrationPoints[2, 2]);
                                state = State.Run;
                                Console.WriteLine("Calibration complete.");
                            }
                        }
                        else
                            calibrated = 0;

                        break;

                    // If the program is running, we will determin if the user has good or bad posture
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

        public static float Distance(SkeletonPoint point1, SkeletonPoint point2)
        {
            float x = point1.X - point2.X;
            float y = point2.Y - point2.Y;
            float z = point2.Z - point2.Z;
            return (float)Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2) + Math.Pow(z, 2));
        }

        public static SkeletonPoint Center(SkeletonPoint point1, SkeletonPoint point2, SkeletonPoint point3)
        {
            float x = (point1.X + point2.X + point3.X) / 3;
            float y = (point2.Y + point2.Y + point3.Y) / 3;
            float z = (point2.Z + point2.Z + point3.Z) / 3;
            SkeletonPoint center = new SkeletonPoint();
            center.X = x;
            center.Y = y;
            center.Z = z;
            return center;
        }

        public static void Debug(string text)
        {
            if (debug)
                Console.WriteLine("DEBUG: " + text);
        }
    }

}
