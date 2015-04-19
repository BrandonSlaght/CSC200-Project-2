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
        public static SkeletonPoint[] previousFrame = new SkeletonPoint[4];
        public static SkeletonPoint[] currentFrame = new SkeletonPoint[4];
        public static SkeletonPoint[,] calibrationPoints = new SkeletonPoint[3, 4];
        public static SkeletonPoint[] calibration = new SkeletonPoint[4];

        public SkeletonPoint[] normalized;
        public int k = 3;
        public static ArrayList<float[]> slouch = new ArrayList<float[]>();
        public static ArrayList<float[]> straight = new ArrayList<float[]>();

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
                Joint center = ourSkel.Joints[JointType.ShoulderCenter];

                // We then set the previousFrame and currentFrame
                if (currentFrame[0] == null)
                {
                    // If this is the first frame, then we can't set the previousFrame
                    currentFrame[0] = left.Position;
                    currentFrame[1] = right.Position;
                    currentFrame[2] = head.Position;
                    currentFrame[3] = center.Position;
                    // We will wait until the next frame so we have a previous frame
                    return;
                }
                previousFrame[0] = currentFrame[0];
                previousFrame[1] = currentFrame[1];
                previousFrame[2] = currentFrame[2];
                previousFrame[3] = currentFrame[3];
                currentFrame[0] = left.Position;
                currentFrame[1] = right.Position;
                currentFrame[2] = head.Position;
                currentFrame[3] = center.Position;

                // What we do with the skeleton data is determined by the state the program is in
                switch (state)
                {
                    // If the program is calibrating, we will get the calibration data from this frame
                    case State.Calibrate:
                        float totalDist = 0;
                        totalDist += Distance(previousFrame[0], currentFrame[0]);
                        totalDist += Distance(previousFrame[1], currentFrame[1]);
                        totalDist += Distance(previousFrame[2], currentFrame[2]);
                        totalDist += Distance(previousFrame[3], currentFrame[3]);
                        Debug("Total dist: " + totalDist);
                        if (totalDist < .005f)
                        {
                            if (calibrated < 3)
                            {
                                Debug("Calibration frame: " + (calibrated + 1) + "/3");
                                calibrationPoints[calibrated, 0] = left.Position;
                                calibrationPoints[calibrated, 1] = right.Position;
                                calibrationPoints[calibrated, 2] = head.Position;
                                calibrationPoints[calibrated, 3] = center.Position;
                                calibrated++;
                            }
                            if (calibrated == 3)
                            {
                                calibration[0] = Center(calibrationPoints[0, 0], calibrationPoints[1, 0], calibrationPoints[2, 0]);
                                calibration[1] = Center(calibrationPoints[0, 1], calibrationPoints[1, 1], calibrationPoints[2, 1]);
                                calibration[2] = Center(calibrationPoints[0, 2], calibrationPoints[1, 2], calibrationPoints[2, 2]);
                                calibration[3] = Center(calibrationPoints[0, 3], calibrationPoints[1, 3], calibrationPoints[2, 3]);
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

        static void normalize()
        {

            normalized = new SkeletonPoint[4];
            //get points
            SkeletonPoint head = new SkeletonPoint(currentFrame[2].X, currentFrame[2].Y, currentFrame[2].Z);
            SkeletonPoint center = new SkeletonPoint(currentFrame[3].X, currentFrame[3].Y, currentFrame[3].Z);
            SkeletonPoint left = new SkeletonPoint(currentFrame[0].X, currentFrame[0].Y, currentFrame[0].Z);
            SkeletonPoint right = new SkeletonPoint(currentFrame[1].X, currentFrame[1].Y, currentFrame[1].Z);


            float deltaX = center.X;
            float deltaY = calibration[4].Y;
            float deltaZ = center.Z;

            //shift points to appropriate positions
            head.X -= deltaX;
            head.Y -= deltaY;
            head.Z -= deltaZ;
            center.X -= deltaX;
            center.Y -= deltaY;
            center.Z -= deltaZ;
            left.X -= deltaX;
            left.Y -= deltaY;
            left.Z -= deltaZ;
            right.X -= deltaX;
            right.Y -= deltaY;
            right.Z -= deltaZ;

            //rotate
            double angle = Math.Atan((left.Z - right.Z) / (left.X - right.X));

            float tempX = head.X;
            float tempZ = head.Z;
            head.X = (float)((tempX * Math.Cos(angle)) + (tempZ * Math.Sin(angle)));
            head.Z = (float)((tempX * Math.Sin(angle)) - (tempZ * Math.Cos(angle)));
            tempX = center.X;
            tempZ = center.Z;
            center.X = (float)((tempX * Math.Cos(angle)) + (tempZ * Math.Sin(angle)));
            center.Z = (float)((tempX * Math.Sin(angle)) - (tempZ * Math.Cos(angle)));
            tempX = left.X;
            tempZ = left.Z;
            left.X = (float)((tempX * Math.Cos(angle)) + (tempZ * Math.Sin(angle)));
            left.Z = (float)((tempX * Math.Sin(angle)) - (tempZ * Math.Cos(angle)));
            tempX = right.X;
            tempZ = right.Z;
            right.X = (float)((tempX * Math.Cos(angle)) + (tempZ * Math.Sin(angle)));
            right.Z = (float)((tempX * Math.Sin(angle)) - (tempZ * Math.Cos(angle)));

            //scale

            float cons = 2 / (Math.Sqrt(Math.Pow(left.X, 2) + Math.Pow(left.Y, 2) + Math.Pow(left.Z, 2)) + (Math.Sqrt(Math.Pow(right.X, 2) + Math.Pow(right.Y, 2) + Math.Pow(right.Z, 2))));
            head.X = cons * head.X;
            head.Y = cons * head.Y;
            head.Z = cons * head.Z;
            center.X = cons * center.X;
            center.Y = cons * center.Y;
            center.Z = cons * center.Z;
            left.X = cons * left.X;
            left.Y = cons * left.Y;
            left.Z = cons * left.Z;
            right.X = cons * right.X;
            right.Y = cons * right.Y;
            right.Z = cons * right.Z;


            normalized[0] = left;
            normalized[1] = right;
            normalized[2] = head;
            normalized[3] = center;
        }

        //guesses whether the given data point is slouch or not
        public static boolean classify(int k)
        {
            float[] point = new float[12];

            float[] distSlouch = new float[slouch.size()];
            float[] distStraight = new float[straight.size()];

            for (int i = 0; i < slouch.size(); i++)
            {
                distSlouch[i] = distance(point, slouch.get(i));
            }
            for (int i = 0; i < mad.size(); i++)
            {
                distMad[i] = distance(point, mad.get(i));
            }
            //technically could merge and use k-select
            Arrays.sort(distHam);
            Arrays.sort(distMad);

            int countHam = 0;
            int countMad = 0;
            for (int i = 0; i < k; i++)
            {
                if (countHam >= distHam.length)
                    countMad++;
                else if (countMad >= distMad.length)
                    countHam++;
                else
                {
                    if (distHam[countHam] < distMad[countMad])
                    {
                        countHam++;
                    }
                    else if (distHam[countHam] > distMad[countMad])
                    {
                        countMad++;
                    }
                    else
                    {
                        if (Math.random() < .5)
                        {
                            countMad++;
                        }
                        else
                        {
                            countHam++;
                        }
                    }
                }
            }
            if (countHam > countMad)
                return true;
            if (countMad > countHam)
                return false;

            return Math.random() < .5;

        }
    }

}