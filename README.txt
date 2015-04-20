House Tyrell Posture monitoring program
This program monitors the posture of a user using the Microsoft Kinect.
To use, make sure a Kinect is kinnected and the correct drivers are installed.
Position the Kinect so that it has a good, unpbstructed view of your entire
upper body from the front.  It helps if the Kinect is head level or higher and looking down at you.  A good tool
 to use to do this is an app from the Kinect developers toolkit.  Once it is
 properly positioned, run the program.  The calibration process will start as soon as it
 detects your skeleton in the frame.  If its having a hard time finding
 you, moving around a bit helps.  Remain sitting still and upright, in good 
 posture, for the calibration, which will succeed when it you remain still for
 for 6 consecutive seconds.  It then begins monitoring your posture
 using a K-NN comparison of your current position normalized to the calibrated
 points to a set of training points in the file "points.txt".  It will print a warning to the terminal when it detects that you 
 slouch or sit up straight again.