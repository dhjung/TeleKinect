using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Research.Kinect.Nui;
using Coding4Fun.Kinect.Wpf;
using System.Diagnostics;
using System.ServiceModel;
using System.Configuration;
using System.Windows.Threading;
using System.Threading;
using System.Net;

using System.Collections;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Media.Animation;

namespace KinectWhiteboard
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window, IChatCallback
    {
        public static MainWindow Instance { get; private set; }

        /// <summary>
        /// Gets the Kinect runtime object
        /// </summary>
        public Microsoft.Research.Kinect.Nui.Runtime NuiRuntime { get; private set; }

        // Variables for the remote user's information
        public Rectangle remoteRec = new Rectangle();
        public bool isUserConnected = false;

        // This variable will be used to tract which rectangle the user selected.
        public int rectangleNumber;

        // Timer
        DispatcherTimer timer;

        #region Skeletion Variables

        public ArrayList BodySegments = new ArrayList();
        public ArrayList BodyJoints = new ArrayList();
        public ArrayList ellipseCursor = new ArrayList(30);

        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;
        byte[] depthFrame32 = new byte[320 * 240 * 4];

        DateTime startTime = DateTime.MaxValue;
        DateTime endTime = DateTime.MaxValue;

        public bool showSkeletons = false;

        Dictionary<JointID, Brush> jointColors = new Dictionary<JointID, Brush>() { 
            {JointID.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {JointID.Head, new SolidColorBrush(Color.FromRgb(200, 0,   0))},
            {JointID.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79,  84,  33))},
            {JointID.ElbowLeft, new SolidColorBrush(Color.FromRgb(84,  33,  42))},
            {JointID.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HandLeft, new SolidColorBrush(Color.FromRgb(215,  86, 0))},
            {JointID.ShoulderRight, new SolidColorBrush(Color.FromRgb(33,  79,  84))},
            {JointID.ElbowRight, new SolidColorBrush(Color.FromRgb(33,  33,  84))},
            {JointID.WristRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.HandRight, new SolidColorBrush(Color.FromRgb(37,   69, 243))},
            {JointID.HipLeft, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.KneeLeft, new SolidColorBrush(Color.FromRgb(69,  33,  84))},
            {JointID.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {JointID.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {JointID.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222,  76))},
            {JointID.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {JointID.FootRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))}
        };

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            // Make sure only one MainWindow ever gets created
            Debug.Assert(Instance == null);

            Instance = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                /*
                NuiRuntime = Microsoft.Research.Kinect.Nui.Runtime.Kinects[0];
                NuiRuntime.Initialize(RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
                NuiRuntime.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                */

                // Set up the Kinects
                NuiRuntime = Microsoft.Research.Kinect.Nui.Runtime.Kinects[0];
                NuiRuntime.Initialize(RuntimeOptions.UseDepth | RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseColor | RuntimeOptions.UseSkeletalTracking);
                NuiRuntime.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_VideoFrameReady);
                NuiRuntime.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady);
                NuiRuntime.SkeletonEngine.TransformSmooth = true;

                var parameters = new TransformSmoothParameters
                {
                    Smoothing = 0.3f,
                    Correction = 0.0f,
                    Prediction = 0.0f,
                    JitterRadius = 1.0f,
                    MaxDeviationRadius = 0.5f
                };
                NuiRuntime.SkeletonEngine.SmoothParameters = parameters;

                try
                {
                    NuiRuntime.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color); //PoolSize = 2 buffers.  One for queuing and one for displaying
                }
                catch (InvalidOperationException)
                {
                    System.Windows.MessageBox.Show("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                    return;
                }

                try
                {
                    NuiRuntime.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.Depth);
                }
                catch (InvalidOperationException)
                {
                    System.Windows.MessageBox.Show("Failed to open depth stream. Please make sure to specify a supported image type and resolution.");
                    return;
                }

            }
            catch (Exception)
            {
                // Failed to set up the Kinect. Show the error onscreen (app will switch to using mouse movement)
                NuiRuntime = null;
                PART_ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (NuiRuntime != null)
                NuiRuntime.Uninitialize();
            Environment.Exit(0);
        }

        // Called when the user presses the 'Quit' button
        private void OnQuit(object sender, RoutedEventArgs args)
        {
            //if (_imageUnsaved)
            //    CurrentPopup = new ConfirmationPopup("Quit without saving?", ActionAwaitingConfirmation.Close, this);
            //else
            Close();
        }

        // Called when the user presses the 'Reset' button
        private void OnReset(object sender, RoutedEventArgs args)
        {
            // Button control
            Start.Visibility = Visibility.Visible;
            Reset.Visibility = Visibility.Collapsed;

            // Stop timer
            timer.Stop();

            try
            {
                CommunicationState cs = proxy.State;
                proxy.Reset(myNick);
            }
            catch (Exception e)
            {
                // Configure the message box to be displayed
                string messageBoxText = e.Message;
                string caption = "Connection Error : OnReset";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;

                // Display message box
                MessageBox.Show(messageBoxText, caption, button, icon);
            }
        }

        private void timer_Task(object sender, EventArgs e)
        {
            TimerLabel.Content = Convert.ToString(DateTime.Now);
        }

        // Called when the user persses the 'Start' button
        private void OnStart(object sender, RoutedEventArgs args)
        {
            // Button control
            Reset.Visibility = Visibility.Visible;
            Start.Visibility = Visibility.Collapsed;

            // Start timer

            timer = new DispatcherTimer(new TimeSpan(0, 0, 0, 0, 1), DispatcherPriority.Normal, delegate
            {
                this.TimerLabel.Content = DateTime.Now.ToString("HH:mm:ss:ff");
            }, this.Dispatcher);

            timer.Start();

            try
            {
                CommunicationState cs = proxy.State;
                proxy.Start(myNick);
            }
            catch (Exception e)
            {
                // Configure the message box to be displayed
                string messageBoxText = e.Message;
                string caption = "Connection Error : OnStart";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;

                // Display message box
                MessageBox.Show(messageBoxText, caption, button, icon);
            }
        }

        private void OnSkeleton(object sender, RoutedEventArgs args)
        {
            // Button control
            NoSkeleton.Visibility = Visibility.Visible;
            Skeleton.Visibility = Visibility.Collapsed;

            showSkeletons = true;
        }

        private void OffSkeleton(object sender, RoutedEventArgs args)
        {
            // Button control
            Skeleton.Visibility = Visibility.Visible;
            NoSkeleton.Visibility = Visibility.Collapsed;

            showSkeletons = false;
        }

        #region Client Events and Methods

        private ChatProxy proxy;
        private string myNick;

        // private PleaseWaitDialog pwDlg;
        private delegate void HandleDelegate(string[] list);
        private delegate void HandleErrorDelegate();

        private void OnConnect(object sender, RoutedEventArgs args)
        {
            InstanceContext site = new InstanceContext(this);
            proxy = new ChatProxy(site);

            // Get Local IP Address
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            myNick = localIPs[1].ToString();

            // Use IP Address as a user name
            IAsyncResult iar = proxy.BeginJoin(myNick, new AsyncCallback(OnEndJoin), null);

            // DisEnable "Connect" button & Enable "Disconnect" button
            // when client is connected with server
            Connect.IsEnabled = false;
            Connect.Visibility = Visibility.Collapsed;

            Disconnect.IsEnabled = true;
            Disconnect.Visibility = Visibility.Visible;

            #region Fade in
            // Create a storyboard to contain the animations.
            Storyboard storyboard = new Storyboard();
            TimeSpan duration = new TimeSpan(0, 0, 5);

            // Create a DoubleAnimation to fade the not selected option control
            DoubleAnimation animation = new DoubleAnimation();

            animation.From = 1.0;
            animation.To = 0.0;
            animation.Duration = new Duration(duration);
            // Configure the animation to target de property Opacity
            Storyboard.SetTargetName(animation, SayHello.Name);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Control.OpacityProperty));
            // Add the animation to the storyboard
            storyboard.Children.Add(animation);

            // Begin the storyboard
            storyboard.Begin(this);

            #endregion
        }

        private void OnEndJoin(IAsyncResult iar)
        {
            try
            {
                string[] list = proxy.EndJoin(iar);

                // Add KinectCursor Handler
                AddRectangleHandler();

                // Set my connection status
                isUserConnected = true;

                HandleEndJoin(list);
            }
            catch (Exception e)
            {
                // Configure the message box to be displayed
                string messageBoxText = e.Message;
                string caption = "Connection Error : OnEndJoin";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;

                // Display message box
                MessageBox.Show(messageBoxText, caption, button, icon);
            }

        }

        private void HandleEndJoin(string[] list)
        {
            if (list == null)
            {
                // pwDlg.ShowError("Error: Existing User Name");
                // ExitChatSession();
            }
            else
            {
                foreach (string name in list)
                {
                    //lstChatters.Items.Add(name);
                }
                // AppendText("Connected " + DateTime.Now.ToString() + " User: " + myNick + Environment.NewLine);
            }
        }

        private void OnDisconnect(object sender, RoutedEventArgs args)
        {
            InfoLabel.Content = "Disconnect";
            try
            {
                proxy.Leave();
            }
            catch (Exception e)
            {
                // Configure the message box to be displayed
                string messageBoxText = e.Message;
                string caption = "Connection Error : OnDisconnect";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;

                // Display message box
                MessageBox.Show(messageBoxText, caption, button, icon);
            }
            finally
            {
                AbortProxyAndUpdateUI();

                // DisEnable "Connect" button & Enable "Disconnect" button
                // when client is connected with server
                Connect.IsEnabled = true;
                Connect.Visibility = Visibility.Visible;

                Disconnect.IsEnabled = false;
                Disconnect.Visibility = Visibility.Collapsed;
            }
        }

        // Abort and Close Proxy and Update UI?
        private void AbortProxyAndUpdateUI()
        {
            if (proxy != null)
            {
                proxy.Abort();
                proxy.Close();
                proxy = null;
            }
            // ShowConnectMenuItem(true);
        }

        // Send current cursor position to server (Server will resend it to client)
        public void SendCursorPosition(int x, int y, string z)
        {
            try
            {
                //InfoLabel.Content = x.ToString() + " " + y.ToString();
                Console.WriteLine("SendCursorPosition: " + x.ToString() + " " + y.ToString() + " ");

                CommunicationState cs = proxy.State;
                proxy.Say(x, y, z);
            }
            catch (Exception e)
            {
                // Configure the message box to be displayed
                string messageBoxText = e.Message;
                string caption = "Connection Error : SendCursorPosition";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;

                // Display message box
                MessageBox.Show(messageBoxText, caption, button, icon);
            }
        }

        public void SendMovingImage(int imageNumber, bool isMoving)
        {
            try
            {
                Console.WriteLine("SendMovingImage: " + imageNumber + "," + isMoving);

                CommunicationState cs = proxy.State;
                proxy.Whisper(imageNumber, isMoving);
            }
            catch (Exception e)
            {
                // Configure the message box to be displayed
                string messageBoxText = e.Message;
                string caption = "Connection Error : SendMovingImage";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;

                // Display message box
                MessageBox.Show(messageBoxText, caption, button, icon);
            }
        }

        #endregion

        #region Implementation IChatCallback (Message from the server)

        // Cursor location from the server
        public int cursorX;
        public int cursorY;

        // Moving location from the server
        public int movingX;
        public int movingY;

        // public void Receive(string senderName, string message)
        public void Receive(string senderName, int x, int y, string z)
        {
            Console.WriteLine("Receive: " + x.ToString() + " " + y.ToString() + " ");

            // Deserialize string
            var _Serializer = new XmlSerializer(typeof(KinectSkeleton));
            var _Skeleton = _Serializer.Deserialize(new StringReader(z));

            // Set cursor position from the server
            cursorX = x;
            cursorY = y;

            // Set cursor position when the remote user is moving image
            movingX = x;
            movingY = y;

            // AppendText(senderName + ": " + message + Environment.NewLine);
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
            {
                Canvas.SetLeft(Partner, cursorX);
                Canvas.SetTop(Partner, cursorY);

                if (showSkeletons)
                {
                    drawJoints((KinectSkeleton)_Skeleton, 1, true);
                }
                else
                {
                    drawJoints((KinectSkeleton)_Skeleton, 1, false);
                }
            });
        }

        //public void ReceiveWhisper(string senderName, string message)
        public void ReceiveWhisper(string senderName, int imageNumber, bool isMoving)
        {
            // AppendText(senderName + " whisper: " + message + Environment.NewLine);
            Console.WriteLine("ReceiveWhisper: " + senderName + " " + imageNumber + " " + isMoving);

            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
            {
                switch (imageNumber)
                {
                    case 1:
                        remoteRec = R1;
                        break;
                    case 2:
                        remoteRec = R2;
                        break;
                    case 3:
                        remoteRec = R3;
                        break;
                    case 4:
                        remoteRec = R4;
                        break;
                    case 5:
                        remoteRec = R5;
                        break;
                    case 6:
                        remoteRec = R6;
                        break;
                    case 7:
                        remoteRec = R7;
                        break;
                    case 8:
                        remoteRec = R8;
                        break;
                    case 9:
                        remoteRec = R9;
                        break;
                }

                if (isMoving)
                {
                    remoteRec.Opacity = 0.5;
                    // Set rectangle's position and properties
                    //Canvas.SetLeft(remoteRec, movingX - remoteRec.ActualWidth / 2);
                    //Canvas.SetTop(remoteRec, movingY - (140 + remoteRec.ActualHeight / 2));
                    //Canvas.SetLeft(remoteRec, movingX - remoteRec.ActualWidth / 2 - 140);
                    //Canvas.SetTop(remoteRec, movingY - (140 + remoteRec.ActualHeight / 2));

                    Canvas.SetLeft(remoteRec, movingX - remoteRec.ActualWidth / 2);
                    Canvas.SetTop(remoteRec, movingY - remoteRec.ActualHeight / 2);
                    Canvas.SetZIndex(remoteRec, 10);

                    ImageLabel.Content = "Your partner is moving Image " + remoteRec.Name;
                }
                else
                {
                    remoteRec.Opacity = 1.0;
                    Canvas.SetZIndex(remoteRec, 0);

                    // rectangleNumber = 0;
                    remoteRec = null;
                }

            });

        }

        public void UserEnter(string name)
        {
            // AppendText("User " + name + " enter at " + DateTime.Now.ToString() + Environment.NewLine);
            // lstChatters.Items.Add(name);
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
            {
                InfoLabel.Content = "Client " + name + " joined.";
                Console.WriteLine("Client " + name + " joined.");
            });
        }

        public void UserLeave(string name)
        {
            isUserConnected = false;

            // AppendText("User " + name + " leave at " + DateTime.Now.ToString() + Environment.NewLine);
            // lstChatters.Items.Remove(name);
            // AdjustWhisperButton();
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
            {
                InfoLabel.Content = "Client " + name + " left.";
                Console.WriteLine("Client " + name + " left.");
            });
        }

        public void ReceiveStart(string name, int[] posX, int[] posY)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
            {
                // Start Image
                Canvas.SetLeft(R1, posX[0]);
                Canvas.SetTop(R1, posY[0]);

                Canvas.SetLeft(R2, posX[1]);
                Canvas.SetTop(R2, posY[1]);

                Canvas.SetLeft(R3, posX[2]);
                Canvas.SetTop(R3, posY[2]);

                Canvas.SetLeft(R4, posX[3]);
                Canvas.SetTop(R4, posY[3]);

                Canvas.SetLeft(R5, posX[4]);
                Canvas.SetTop(R5, posY[4]);

                Canvas.SetLeft(R6, posX[5]);
                Canvas.SetTop(R6, posY[5]);

                Canvas.SetLeft(R7, posX[6]);
                Canvas.SetTop(R7, posY[6]);

                Canvas.SetLeft(R8, posX[7]);
                Canvas.SetTop(R8, posY[7]);

                Canvas.SetLeft(R7, posX[8]);
                Canvas.SetTop(R7, posY[8]);
            });
        }

        public void ReceiveReset(string name)
        {
            int[] _posX = new int[9] { 320, 520, 720, 320, 520, 720, 320, 520, 720 };
            int[] _posY = new int[9] { 150, 150, 150, 350, 350, 350, 550, 550, 550 };

            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
            {
                // Reset Image Position
                Canvas.SetLeft(R1, _posX[0]);
                Canvas.SetTop(R1, _posY[0]);

                Canvas.SetLeft(R2, _posX[1]);
                Canvas.SetTop(R2, _posY[1]);

                Canvas.SetLeft(R3, _posX[2]);
                Canvas.SetTop(R3, _posY[2]);

                Canvas.SetLeft(R4, _posX[3]);
                Canvas.SetTop(R4, _posY[3]);

                Canvas.SetLeft(R5, _posX[4]);
                Canvas.SetTop(R5, _posY[4]);

                Canvas.SetLeft(R6, _posX[5]);
                Canvas.SetTop(R6, _posY[5]);

                Canvas.SetLeft(R7, _posX[6]);
                Canvas.SetTop(R7, _posY[6]);

                Canvas.SetLeft(R8, _posX[7]);
                Canvas.SetTop(R8, _posY[7]);

                Canvas.SetLeft(R9, _posX[8]);
                Canvas.SetTop(R9, _posY[8]);
            });
        }

        #endregion

        #region register KinectCursor Handler

        private void AddRectangleHandler()
        {
            KinectCursor.AddCursorEnterHandler(R1, RectangleOnCursorEnter1);
            KinectCursor.AddCursorLeaveHandler(R1, RectangleOnCursorLeave1);

            KinectCursor.AddCursorEnterHandler(R2, RectangleOnCursorEnter2);
            KinectCursor.AddCursorLeaveHandler(R2, RectangleOnCursorLeave2);

            KinectCursor.AddCursorEnterHandler(R3, RectangleOnCursorEnter3);
            KinectCursor.AddCursorLeaveHandler(R3, RectangleOnCursorLeave3);

            KinectCursor.AddCursorEnterHandler(R4, RectangleOnCursorEnter4);
            KinectCursor.AddCursorLeaveHandler(R4, RectangleOnCursorLeave4);

            KinectCursor.AddCursorEnterHandler(R5, RectangleOnCursorEnter5);
            KinectCursor.AddCursorLeaveHandler(R5, RectangleOnCursorLeave5);

            KinectCursor.AddCursorEnterHandler(R6, RectangleOnCursorEnter6);
            KinectCursor.AddCursorLeaveHandler(R6, RectangleOnCursorLeave6);

            KinectCursor.AddCursorEnterHandler(R7, RectangleOnCursorEnter7);
            KinectCursor.AddCursorLeaveHandler(R7, RectangleOnCursorLeave7);

            KinectCursor.AddCursorEnterHandler(R8, RectangleOnCursorEnter8);
            KinectCursor.AddCursorLeaveHandler(R8, RectangleOnCursorLeave8);

            KinectCursor.AddCursorEnterHandler(R9, RectangleOnCursorEnter9);
            KinectCursor.AddCursorLeaveHandler(R9, RectangleOnCursorLeave9);
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter1(object sender, CursorEventArgs args)
        {
            if (remoteRec != R1 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 1;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave1(object sender, CursorEventArgs args)
        {
            if (remoteRec != R1 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter2(object sender, CursorEventArgs args)
        {
            if (remoteRec != R2 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 2;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave2(object sender, CursorEventArgs args)
        {
            if (remoteRec != R2 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter3(object sender, CursorEventArgs args)
        {
            if (remoteRec != R3 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 3;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave3(object sender, CursorEventArgs args)
        {
            if (remoteRec != R3 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter4(object sender, CursorEventArgs args)
        {
            if (remoteRec != R4 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 4;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave4(object sender, CursorEventArgs args)
        {
            if (remoteRec != R4 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter5(object sender, CursorEventArgs args)
        {
            if (remoteRec != R5 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 5;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave5(object sender, CursorEventArgs args)
        {
            if (remoteRec != R5 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter6(object sender, CursorEventArgs args)
        {
            if (remoteRec != R6 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 6;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave6(object sender, CursorEventArgs args)
        {
            if (remoteRec != R6 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter7(object sender, CursorEventArgs args)
        {
            if (remoteRec != R7 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 7;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave7(object sender, CursorEventArgs args)
        {
            if (remoteRec != R7 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter8(object sender, CursorEventArgs args)
        {
            if (remoteRec != R8 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 8;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave8(object sender, CursorEventArgs args)
        {
            if (remoteRec != R8 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the cursor enters this polygon's visible area
        private void RectangleOnCursorEnter9(object sender, CursorEventArgs args)
        {
            if (remoteRec != R9 || remoteRec == null)
            {
                args.Cursor.BeginHover();
                args.Cursor.HoverFinished += Cursor_HoverFinished;
                rectangleNumber = 9;
            }
        }

        // Called when the cursor leaves this polygon's visible area
        private void RectangleOnCursorLeave9(object sender, CursorEventArgs args)
        {
            if (remoteRec != R9 || remoteRec == null)
            {
                args.Cursor.EndHover();
                args.Cursor.HoverFinished -= Cursor_HoverFinished;
                rectangleNumber = 0;
            }
        }

        // Called when the hover action has finished, and the button should be pressed
        private void Cursor_HoverFinished(object sender, EventArgs e)
        {
            ((KinectCursor)sender).HoverFinished -= Cursor_HoverFinished;
        }

        #endregion

        #region Draw Skeleton and Video

        private Point getDisplayPosition(Joint j)
        {
            float depthX, depthY;
            MainWindow.Instance.NuiRuntime.SkeletonEngine.SkeletonToDepthImage(j.Position, out depthX, out depthY);

            depthX = depthX * 320; //convert to 320, 240 space
            depthY = depthY * 240; //convert to 320, 240 space
            int colorX, colorY;
            ImageViewArea iv = new ImageViewArea();
            // only ImageResolution.Resolution640x480 is supported at this point

            MainWindow.Instance.NuiRuntime.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, (short)0, out colorX, out colorY);

            // map back to canvas1.Width & canvas1.Height
            //Console.WriteLine("main window width: " + MainWindow.Instance.canvas1.Width);
            //Console.WriteLine("main window height: " + MainWindow.Instance.canvas1.Height);

            return new Point((double)(MainWindow.Instance.canvas1.ActualWidth * colorX / 640.0), (double)(MainWindow.Instance.canvas1.ActualHeight * 1.2 * colorY / 480));
        }

        Polyline getBodySegment(Brush brush, params Joint[] joints)
        {
            /*PointCollection points = new PointCollection(ids.Length);
            for (int i = 0; i < ids.Length; ++i)
            {
                points.Add(getDisplayPosition(joints[ids[i]]));
            }*/

            PointCollection points = new PointCollection(joints.Length);
            foreach (Joint j in joints)
            {
                points.Add(getDisplayPosition(j));
            }

            Polyline polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
        }

        //void drawJoints(Nui.SkeletonData data, int playerIndx)
        void drawJoints(KinectSkeleton data, int playerIndx, bool showSkeletons)
        {
            // Draw bones
            Brush brush = Brushes.Black;
            ArrayList CurrentBodySegments = new ArrayList();
            ArrayList CurrentBodyJoints = new ArrayList();
            ArrayList _Joints = new ArrayList();

            _Joints.Add(data.ankleLeft);
            _Joints.Add(data.ankleRight);
            _Joints.Add(data.elbowLeft);
            _Joints.Add(data.elbowRight);
            _Joints.Add(data.footLeft);
            _Joints.Add(data.footRight);
            _Joints.Add(data.handLeft);
            _Joints.Add(data.handRight);
            _Joints.Add(data.head);
            _Joints.Add(data.hipCenter);
            _Joints.Add(data.hipLeft);
            _Joints.Add(data.hipRight);
            _Joints.Add(data.kneeLeft);
            _Joints.Add(data.kneeRight);
            _Joints.Add(data.shoulderCenter);
            _Joints.Add(data.shoulderLeft);
            _Joints.Add(data.shoulderRight);
            _Joints.Add(data.spine);
            _Joints.Add(data.wristLeft);
            _Joints.Add(data.wristRight);

            switch (playerIndx)
            {
                case 1:
                    brush = Brushes.Black;
                    CurrentBodySegments = BodySegments;
                    CurrentBodyJoints = BodyJoints;
                    break;
            }

            //remove current skeleton from canvas
            foreach (Polyline p in CurrentBodySegments)
            {
                MainWindow.Instance.canvas1.Children.Remove(p);
            }

            //clear BodySegments Array
            CurrentBodySegments.Clear();

            if (data != null)
            {
                //BodySegments add
                CurrentBodySegments.Add(getBodySegment(brush, data.hipCenter, data.spine, data.shoulderCenter, data.head));
                CurrentBodySegments.Add(getBodySegment(brush, data.shoulderCenter, data.shoulderLeft, data.elbowLeft, data.wristLeft, data.handLeft));
                CurrentBodySegments.Add(getBodySegment(brush, data.shoulderCenter, data.shoulderRight, data.elbowRight, data.wristRight, data.handRight));
                CurrentBodySegments.Add(getBodySegment(brush, data.hipCenter, data.hipLeft, data.kneeLeft, data.ankleLeft, data.footLeft));
                CurrentBodySegments.Add(getBodySegment(brush, data.hipCenter, data.hipRight, data.kneeRight, data.ankleRight, data.footRight));

                //BodySegments.Add(getBodySegment(data.Joints, brush, JointID.ShoulderRight, JointID.HipRight, JointID.HipLeft, JointID.ShoulderLeft));

                foreach (Polyline p in CurrentBodySegments)
                {
                    p.Opacity = 0.50;

                    // Show Body Skeletons when 'showSkeletons' is true
                    if (showSkeletons)
                    {
                        MainWindow.Instance.canvas1.Children.Add(p);
                    }
                    else
                    {
                        MainWindow.Instance.canvas1.Children.Remove(p);
                    }
                }
            }

            foreach (UIElement l in CurrentBodyJoints)
            {
                MainWindow.Instance.canvas1.Children.Remove(l);
            }
            CurrentBodyJoints.Clear();

            // Draw joints
            if (data != null)
            {
                foreach (Joint joint in _Joints)
                {
                    // Console.WriteLine("join name: " + joint.ID);
                    Point jointPos = getDisplayPosition(joint);
                    if (joint.ID == JointID.Head)
                    {
                        // Console.WriteLine("FOUND THE HEAD");                      
                        Ellipse head = new Ellipse();
                        head.Width = 50;
                        head.Height = 50;
                        head.Fill = Brushes.Black;
                        head.Opacity = 0.20;

                        Canvas.SetLeft(head, (int)jointPos.X - 25);
                        Canvas.SetTop(head, (int)jointPos.Y - 25);

                        CurrentBodyJoints.Add(head);

                        TextBlock name = new TextBlock();
                        name.Text = "Friend";
                        name.FontSize = 30;
                        name.Width = 100;
                        name.Height = 50;
                        name.Foreground = Brushes.Blue;

                        Canvas.SetLeft(name, (int)jointPos.X - 25 + 80);
                        Canvas.SetTop(name, (int)jointPos.Y - 25 - 20);

                        CurrentBodyJoints.Add(name);

                    }
                    else if (joint.ID == JointID.HandRight)
                    {
                        Ellipse el = new Ellipse();
                        el.Width = 20;
                        el.Height = 20;
                        el.Fill = Brushes.Black;
                        el.Opacity = 0.20;

                        Canvas.SetLeft(el, (int)jointPos.X - 25);
                        Canvas.SetTop(el, (int)jointPos.Y - 25);

                        CurrentBodyJoints.Add(el);
                    }
                }

                foreach (UIElement l in CurrentBodyJoints)
                {
                    // Show Head, RightHand, Name
                    if (showSkeletons)
                    {
                        MainWindow.Instance.canvas1.Children.Add(l);
                    }
                    else
                    {
                        MainWindow.Instance.canvas1.Children.Remove(l);
                    }
                }
            }
        }

        void nui_VideoFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage imageData = e.ImageFrame.Image;
            video.Source = BitmapSource.Create(imageData.Width, imageData.Height, 96, 96,
                                     PixelFormats.Bgr32, null, imageData.Bits, imageData.Width * imageData.BytesPerPixel);
        }

        void nui_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            depth.Source = e.ImageFrame.ToBitmapSource();
        }

        #endregion
    }
}
