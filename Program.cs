using System;
using System.Collections.Generic;
using System.Collections;
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

        public static SkeletonPoint[] normalizedCalibration = new SkeletonPoint[4];
        public static SkeletonPoint[] normalized = new SkeletonPoint[4];
        public static int k = 3;
        public static List<float[]> slouch = new List<float[]>();
        public static List<float[]> straight = new List<float[]>();
        public static bool slouchState = true;

        static void Main(string[] args)
        {
            ReadFromFile();
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
                    sensor.DepthStream.Range = DepthRange.Near;
                    sensor.SkeletonStream.EnableTrackingInNearRange = true;
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
                        if (totalDist < .0075f)
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
                                currentFrame[0] = calibration[0];
                                currentFrame[1] = calibration[1];
                                currentFrame[2] = calibration[2];
                                currentFrame[3] = calibration[3];
                                normalize();
                                normalizedCalibration = normalized;
                                state = State.Run;
                                timer.Interval = 1000;
                                Console.WriteLine("Calibration complete.");
                                state = State.Run;
                            }
                        }
                        else
                            calibrated = 0;

                        break;

                    // If the program is running, we will determin if the user has good or bad posture
                    case State.Run:
                        normalize();
                        float diff = 0;
                        diff += Distance(normalizedCalibration[0], normalized[0]);
                        diff += Distance(normalizedCalibration[1], normalized[1]);
                        //diff += Distance(normalizedCalibration[2], normalized[2]);
                        diff += Distance(normalizedCalibration[3], normalized[3]);
                        /*Console.WriteLine(diff.ToString());
                        Console.WriteLine(normalized[3].Y.ToString());
                        string data = PointToString(normalized[0]) + " " + PointToString(normalized[1]) + " " + PointToString(normalized[2]) + " " + PointToString(normalized[3]);
                        File.WriteAllText("C:\\Users\\Marysia\\Desktop\\point.txt", data);
                        Console.WriteLine("Calibration complete.");
                        */
                        classify();
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
            SkeletonPoint head = new SkeletonPoint();
            head.X = currentFrame[2].X;
            head.Y = currentFrame[2].Y;
            head.Z = currentFrame[2].Z;
            SkeletonPoint center = new SkeletonPoint();
            center.X = currentFrame[3].X;
            center.Y = currentFrame[3].Y;
            center.Z = currentFrame[3].Z;
            SkeletonPoint left = new SkeletonPoint();
            left.X = currentFrame[0].X;
            left.Y = currentFrame[0].Y;
            left.Z = currentFrame[0].Z;
            SkeletonPoint right = new SkeletonPoint();
            right.X = currentFrame[1].X;
            right.Y = currentFrame[1].Y;
            right.Z = currentFrame[1].Z;


            float deltaX = center.X;
            float deltaY = calibration[3].Y;
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

            float cons = (float)(2 / (Math.Sqrt(Math.Pow(left.X, 2) + Math.Pow(left.Y, 2) + Math.Pow(left.Z, 2)) + (Math.Sqrt(Math.Pow(right.X, 2) + Math.Pow(right.Y, 2) + Math.Pow(right.Z, 2)))));
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
        //returns true if slouch false if not
        public static void classify()
        {
            Console.WriteLine("" + slouch.Count + ", " + straight.Count);
            float[] point = new float[12];
            point[0] = normalized[0].X;
            point[1] = normalized[0].Y;
            point[2] = normalized[0].Z;
            point[3] = normalized[1].X;
            point[4] = normalized[1].Y;
            point[5] = normalized[1].Z;
            point[6] = normalized[2].X;
            point[7] = normalized[2].Y;
            point[8] = normalized[2].Z;
            point[9] = normalized[3].X;
            point[10] = normalized[3].Y;
            point[11] = normalized[3].Z;

            float[] distSlouch = new float[slouch.Count];
            float[] distStraight = new float[straight.Count];

            for (int i = 0; i < distSlouch.Length; i++)
            {
                distSlouch[i] = distance(point, slouch.ElementAt(i));
            }
            for (int i = 0; i < distStraight.Length; i++)
            {
                distStraight[i] = distance(point, straight.ElementAt(i));
            }
            //technically could merge and use k-select
            Array.Sort(distSlouch);
            Array.Sort(distStraight);

            float threshold = 20;

            if (distSlouch[0] > threshold && distStraight[0] > threshold)
                return;
            int countSlouch = 0;
            int countStraight = 0;
            for (int i = 0; i < k; i++)
            {
                if (countSlouch >= distSlouch.Length)
                    countStraight++;
                else if (countStraight >= distStraight.Length)
                    countSlouch++;
                else
                {
                    if (distSlouch[countSlouch] < distStraight[countStraight])
                    {
                        countSlouch++;
                    }
                    else
                    {
                        countStraight++;
                    }
                }
            }
            if ((countSlouch > countStraight) && !slouchState)
            {
                slouchState = true;
                Console.WriteLine("You're slouching!");
            }
            else if ((countSlouch < countStraight) && slouchState)
            {
                slouchState = false;
                Console.WriteLine("You're upright!");
            }

        }

        public static float distance(float[] ar1, float[] ar2)
        {
            float sumsqrs = 0;
            for (int i = 0; i < ar1.Length; i++)
            {
                sumsqrs += (float)Math.Pow(ar1[i] - ar2[i], 2);
            }
            return (float)Math.Sqrt(sumsqrs);
        }

        public static string PointToString(SkeletonPoint point)
        {
            return point.X + " " + point.Y + " " + point.Z;
        }

        public static void ReadFromFile()
        {
            StreamReader sr = new StreamReader("points.txt");
            String line;
            while ((line = sr.ReadLine()) != null)
            {

                string[] splitLine = line.Split(null);
                if (splitLine[0] == "0")
                {
                    float[] floatsToAdd = new float[12];
                    for (int i = 0; i < 12; i++)
                    {
                        floatsToAdd[i] = float.Parse(splitLine[i + 1]);
                    }
                    slouch.Add(floatsToAdd);
                }
                else
                {
                    float[] floatsToAdd = new float[12];
                    for (int i = 0; i < 12; i++)
                    {
                        floatsToAdd[i] = float.Parse(splitLine[i + 1]);
                    }
                    straight.Add(floatsToAdd);
                }
            }
        }
    }

}