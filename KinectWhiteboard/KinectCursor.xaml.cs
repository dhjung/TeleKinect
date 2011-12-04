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

using Nui = Microsoft.Research.Kinect.Nui;
using Coding4Fun.Kinect.Wpf;
using System.Diagnostics;
using System.Windows.Media.Animation;

using System.Collections;
using System.Xml.Serialization;
using System.IO;

namespace KinectWhiteboard
{
    /// <summary>
    /// Displays the cursor on the screen, and provides functionality allowing the cursor to interact with other elements
    /// </summary>
    public partial class KinectCursor : UserControl
    {
        #region Data

        Visual _currentlyOver;
        Storyboard _hoverStoryboard;
        bool _isHovering;
        bool _isPainting;
        // Check whether object on canvas is selected
        bool _isSlected = false;

        #endregion

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

        Dictionary<Nui.JointID, Brush> jointColors = new Dictionary<Nui.JointID, Brush>() { 
            {Nui.JointID.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {Nui.JointID.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {Nui.JointID.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {Nui.JointID.Head, new SolidColorBrush(Color.FromRgb(200, 0,   0))},
            {Nui.JointID.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79,  84,  33))},
            {Nui.JointID.ElbowLeft, new SolidColorBrush(Color.FromRgb(84,  33,  42))},
            {Nui.JointID.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {Nui.JointID.HandLeft, new SolidColorBrush(Color.FromRgb(215,  86, 0))},
            {Nui.JointID.ShoulderRight, new SolidColorBrush(Color.FromRgb(33,  79,  84))},
            {Nui.JointID.ElbowRight, new SolidColorBrush(Color.FromRgb(33,  33,  84))},
            {Nui.JointID.WristRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {Nui.JointID.HandRight, new SolidColorBrush(Color.FromRgb(37,   69, 243))},
            {Nui.JointID.HipLeft, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {Nui.JointID.KneeLeft, new SolidColorBrush(Color.FromRgb(69,  33,  84))},
            {Nui.JointID.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {Nui.JointID.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {Nui.JointID.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {Nui.JointID.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222,  76))},
            {Nui.JointID.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {Nui.JointID.FootRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))}
        };

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public KinectCursor()
        {
            InitializeComponent();

            _hoverStoryboard = FindResource("PART_HoverStoryboard") as Storyboard;
            _hoverStoryboard.Completed += OnHoverStoryboardComplete;

            // capture the mouse once loaded, so we can make sure parts of the UI only respond to 
            // this cursor, whether it's controlled via the Kinect or the mouse. It's a Kinect app after all :)
            Loaded += (s, e) =>
            {
                //Mouse.Capture(this, CaptureMode.SubTree);
                try
                {
                    if (MainWindow.Instance.NuiRuntime == null)
                        CompositionTarget.Rendering += (s2, e2) => OnUpdate(null, new Nui.SkeletonFrameReadyEventArgs());
                    else
                        MainWindow.Instance.NuiRuntime.SkeletonFrameReady += OnUpdate;
                }
                catch (NullReferenceException)
                {
                    //Do Nothing
                }
            };
        }

        #region Events

        /// <summary>
        /// Fired when a hover is completed
        /// </summary>
        public event EventHandler HoverFinished;

        /// <summary>
        /// Fired on an element when the cursor enters that element's visible boundary
        /// </summary>
        public static readonly RoutedEvent CursorEnterEvent = EventManager.RegisterRoutedEvent("CursorEnter", RoutingStrategy.Direct, typeof(EventHandler<CursorEventArgs>), typeof(DependencyObject));

        /// <summary>
        /// Fired on an element when the cursor leaves that element's visible boundary
        /// </summary>
        public static readonly RoutedEvent CursorLeaveEvent = EventManager.RegisterRoutedEvent("CursorLeave", RoutingStrategy.Direct, typeof(EventHandler<CursorEventArgs>), typeof(DependencyObject));

        /// <summary>
        /// Adds a handler for the CursorEnter event on a particular element
        /// </summary>
        /// <param name="target">The element to handle the event on</param>
        /// <param name="handler">The handler of the event</param>
        public static void AddCursorEnterHandler(DependencyObject target, EventHandler<CursorEventArgs> handler)
        {
            IInputElement ie = target as IInputElement;

            if (ie != null)
                ie.AddHandler(CursorEnterEvent, handler);
        }

        /// <summary>
        /// Removes a handler for the CursorEnter event on a particular element
        /// </summary>
        /// <param name="target">The element to handle the event on</param>
        /// <param name="handler">The handler of the event</param>
        public static void RemoveCursorEnterHandler(DependencyObject target, EventHandler<CursorEventArgs> handler)
        {
            IInputElement ie = target as IInputElement;

            if (ie != null)
                ie.RemoveHandler(CursorEnterEvent, handler);
        }

        /// <summary>
        /// Adds a handler for the CursorLeave event on a particular element
        /// </summary>
        /// <param name="target">The element to handle the event on</param>
        /// <param name="handler">The handler of the event</param>
        public static void AddCursorLeaveHandler(DependencyObject target, EventHandler<CursorEventArgs> handler)
        {
            IInputElement ie = target as IInputElement;

            if (ie != null)
                ie.AddHandler(CursorLeaveEvent, handler);
        }

        /// <summary>
        /// Removes a handler for the CursorLeave event on a particular element
        /// </summary>
        /// <param name="target">The element to handle the event on</param>
        /// <param name="handler">The handler of the event</param>
        public static void RemoveCursorLeaveHandler(DependencyObject target, EventHandler<CursorEventArgs> handler)
        {
            IInputElement ie = target as IInputElement;

            if (ie != null)
                ie.RemoveHandler(CursorLeaveEvent, handler);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Tells the KinectCursor to start a hover operation
        /// </summary>
        public void BeginHover()
        {
            Debug.Assert(!_isHovering);

            WaitCursor.Visibility = Visibility.Visible;
            DrawCursor.Visibility = Visibility.Collapsed;

            _hoverStoryboard.Begin();
            _isHovering = true;
        }

        /// <summary>
        /// Tells the Kinect to end a hover operation prematurely, if one is still taking place
        /// </summary>
        public void EndHover()
        {
            if (!_isHovering) return;

            WaitCursor.Visibility = Visibility.Collapsed;
            DrawCursor.Visibility = Visibility.Visible;

            _hoverStoryboard.Stop();
            _isHovering = false;
        }

        /// <summary>
        /// Gets the position of the cursor relative to the given element
        /// </summary>
        /// <param name="visual">The element</param>
        /// <returns>The cursor's position, in the coordinate space of the element</returns>
        public Point GetPosition(Visual visual)
        {
            if (visual == MainWindow.Instance) return CursorPosition;

            return MainWindow.Instance.TransformToDescendant(visual).Transform(CursorPosition);
        }

        /// <summary>
        /// Gets the previous position of the cursor relative to the given element
        /// </summary>
        /// <param name="visual">The element</param>
        /// <returns>The cursor's previousposition, in the coordinate space of the element</returns>
        public Point GetPreviousPosition(Visual visual)
        {
            if (visual == MainWindow.Instance) return CursorPosition;

            return MainWindow.Instance.TransformToDescendant(visual).Transform(PreviousCursorPosition);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current cursor position relative to the window's client area
        /// </summary>
        public Point CursorPosition
        {
            get { return _cursorPosition; }
            private set
            {
                _cursorPosition = value;

                Canvas.SetLeft(PART_Cursor, value.X);
                Canvas.SetTop(PART_Cursor, value.Y);
            }
        }
        private Point _cursorPosition;

        /// <summary>
        /// Gets the previous cursor position
        /// </summary>
        public Point PreviousCursorPosition { get; private set; }

        /// <summary>
        /// Gets or sets whether the cursor is currently passive. If true, CursorEnter and CursorLeave events will not fire.
        /// </summary>
        public bool Passive
        {
            get { return _passive; }
            set
            {
                if (_passive == value) return;

                _passive = value;

                UpdateElementOver();
            }
        }
        private bool _passive;

        #endregion

        #region Internal

        // String contains serialized XML which is generated from Skeleton
        string m_xmlSkeleton;

        void OnUpdate(object sender, Nui.SkeletonFrameReadyEventArgs e)
        {
            // KINECT Add code to get joint data and smooth it
            if (Application.Current.MainWindow == null || MainWindow.Instance == null) return;

            Nui.SkeletonData skeleton = null;

            PreviousCursorPosition = CursorPosition;

            if (e.SkeletonFrame == null)
            {
                CursorPosition = Mouse.GetPosition(Application.Current.MainWindow);
            }
            else
            {
                foreach (Nui.SkeletonData sd in e.SkeletonFrame.Skeletons)
                {
                    if (sd.TrackingState == Nui.SkeletonTrackingState.Tracked)
                    {
                        skeleton = sd;
                        break;
                    }
                }

                if (skeleton == null) return;

                // Show User's Skeleton when they selects
                //if (MainWindow.Instance.showSkeletons)
                //{
                    drawJoints(skeleton, 1);
                //}


                #region Serialization
                    
                // Initialize 
                var _Skeleton = new KinectSkeleton { ankleLeft = skeleton.Joints[Nui.JointID.AnkleLeft],
                                                     ankleRight = skeleton.Joints[Nui.JointID.AnkleRight],
                                                     elbowLeft = skeleton.Joints[Nui.JointID.ElbowLeft],
                                                     elbowRight = skeleton.Joints[Nui.JointID.ElbowRight],
                                                     footLeft = skeleton.Joints[Nui.JointID.FootLeft],
                                                     footRight = skeleton.Joints[Nui.JointID.FootRight],
                                                     handLeft = skeleton.Joints[Nui.JointID.HandLeft],
                                                     handRight = skeleton.Joints[Nui.JointID.HandRight],
                                                     head = skeleton.Joints[Nui.JointID.Head],
                                                     hipCenter = skeleton.Joints[Nui.JointID.HipCenter],
                                                     hipLeft = skeleton.Joints[Nui.JointID.HipLeft],
                                                     hipRight = skeleton.Joints[Nui.JointID.HipRight],
                                                     kneeLeft = skeleton.Joints[Nui.JointID.KneeLeft],
                                                     kneeRight = skeleton.Joints[Nui.JointID.KneeRight],
                                                     shoulderCenter = skeleton.Joints[Nui.JointID.ShoulderCenter],
                                                     shoulderLeft = skeleton.Joints[Nui.JointID.ShoulderLeft],
                                                     shoulderRight = skeleton.Joints[Nui.JointID.ShoulderRight],
                                                     spine = skeleton.Joints[Nui.JointID.Spine],
                                                     wristLeft = skeleton.Joints[Nui.JointID.WristLeft],
                                                     wristRight = skeleton.Joints[Nui.JointID.WristRight] };
                
                var _Serializer = new XmlSerializer(typeof(KinectSkeleton));
                StringBuilder _Builder = new StringBuilder();
                using (StringWriter _Writer = new StringWriter(_Builder))
                {
                    _Serializer.Serialize(_Writer, _Skeleton);
                }

                m_xmlSkeleton = _Builder.ToString();

                #endregion Serialization

                Nui.Vector nuiv = skeleton.Joints[Nui.JointID.HandRight].ScaleTo((int)ActualWidth, (int)ActualHeight, 1.0f, 0.5f).Position;
                CursorPosition = new Point(nuiv.X, nuiv.Y);
            }

            // Update which graphical element the cursor is currently over
            if (!Passive)
                UpdateElementOver();

            if (MainWindow.Instance.NuiRuntime == null)
            {
                // For mouse, see if the right mouse button is down.
                if (_isPainting)
                {
                    if (Mouse.LeftButton == MouseButtonState.Released)
                    {
                        // MainWindow.Instance.StopPainting();
                    }
                }
                else
                {
                    if (Mouse.LeftButton == MouseButtonState.Pressed)
                    {
                        // MainWindow.Instance.StartPainting();
                    }
                }

                _isPainting = Mouse.LeftButton == MouseButtonState.Pressed;
            }
            else
            {
                if (MainWindow.Instance.isUserConnected)
                {
                    // Send my cursor position to remote user
                    MainWindow.Instance.SendCursorPosition((int)CursorPosition.X, (int)CursorPosition.Y, m_xmlSkeleton);
                }

                // To begin painting w/ Kinect, raise your left hand above your shoulder.
                // To stop painting, lower it.

                Nui.Joint lh = skeleton.Joints[Nui.JointID.HandLeft];
                Nui.Joint ls = skeleton.Joints[Nui.JointID.ShoulderLeft];

                bool isup = lh.Position.Y > ls.Position.Y;

                if (isup != _isPainting)
                {
                    _isPainting = isup;

                    if (_isPainting){
                        // MainWindow.Instance.StartPainting();
                    }
                    else{
                        // MainWindow.Instance.StopPainting();
                    }
                }
                
                // Image number
                int number = MainWindow.Instance.rectangleNumber;

                if (_isSlected && (number != 0))
                {
                    Rectangle selectedRectangle = new Rectangle();

                    switch (number)
                    {
                        case 0:
                            break;
                        case 1:
                            selectedRectangle = MainWindow.Instance.R1;
                            break;
                        case 2:
                            selectedRectangle = MainWindow.Instance.R2;
                            break;
                        case 3:
                            selectedRectangle = MainWindow.Instance.R3;
                            break;
                        case 4:
                            selectedRectangle = MainWindow.Instance.R4;
                            break;
                        case 5:
                            selectedRectangle = MainWindow.Instance.R5;
                            break;
                        case 6:
                            selectedRectangle = MainWindow.Instance.R6;
                            break;
                        case 7:
                            selectedRectangle = MainWindow.Instance.R7;
                            break;
                        case 8:
                            selectedRectangle = MainWindow.Instance.R8;
                            break;
                        case 9:
                            selectedRectangle = MainWindow.Instance.R9;
                            break;
                    }

                    if (selectedRectangle != null)
                    {
                        // When rectangle is moving 
                        selectedRectangle.Opacity = 0.5;
                        // selectedRectangle.IsHitTestVisible = false;                        
                        Canvas.SetZIndex(selectedRectangle, 10);

                        if (isup)
                        {
                            _isSlected = false;
                            selectedRectangle.Opacity = 1.0;
                            // selectedRectangle.IsHitTestVisible = true;                            
                            Canvas.SetZIndex(selectedRectangle, 0);

                            // Send which image is moving to the remote user
                            MainWindow.Instance.SendMovingImage(number, _isSlected);
                        }
                        else
                        {
                            // Set moving image position on the Canvas
                            // Canvas.SetLeft(selectedRectangle, CursorPosition.X - selectedRectangle.ActualWidth / 2);
                            // Canvas.SetTop(selectedRectangle, CursorPosition.Y - (140 + selectedRectangle.ActualHeight / 2));

                            // Canvas.SetLeft(selectedRectangle, CursorPosition.X - selectedRectangle.ActualWidth / 2 - 140);
                            // Canvas.SetTop(selectedRectangle, CursorPosition.Y - (140 + selectedRectangle.ActualHeight / 2));

                            Canvas.SetLeft(selectedRectangle, CursorPosition.X - selectedRectangle.ActualWidth / 2);
                            Canvas.SetTop(selectedRectangle, CursorPosition.Y - selectedRectangle.ActualHeight / 2);

                            // Send which image is moving to the remote user 
                            MainWindow.Instance.SendMovingImage(number, _isSlected);
                        }
                    }
                }
            }
        }

        // Perform hit testing and fire enter/leave events so controls can properly respond
        private void UpdateElementOver()
        {
            Visual visualOver = null;
            DependencyObject commonAncestor = null;

            if (!Passive)
                visualOver = Application.Current.MainWindow.InputHitTest(_cursorPosition) as Visual;

            if (_currentlyOver == visualOver) return;

            if (_currentlyOver != null && visualOver != null)
                commonAncestor = visualOver.FindCommonVisualAncestor(_currentlyOver);

            for (DependencyObject leaving = _currentlyOver; leaving != commonAncestor; leaving = VisualTreeHelper.GetParent(leaving))
            {
                if (!(leaving is IInputElement))
                {
                    continue;
                }

                ((IInputElement)leaving).RaiseEvent(new CursorEventArgs(this, CursorLeaveEvent, leaving));
            }

            for (DependencyObject entering = visualOver; entering != commonAncestor; entering = VisualTreeHelper.GetParent(entering))
            {
                if (!(entering is IInputElement))
                {
                    continue;
                }

                ((IInputElement)entering).RaiseEvent(new CursorEventArgs(this, CursorEnterEvent, entering));
            }

            _currentlyOver = visualOver;
        }

        // Fires when the hover animation is finished, so we can inform any listeners (like buttons) so they can do their thing.
        private void OnHoverStoryboardComplete(object sender, EventArgs e)
        {
            if (HoverFinished != null)
                HoverFinished(this, new EventArgs());

            EndHover();
            _isHovering = false;

            // Objects on Canvas will be selected when the hover animation is finished. 
            _isSlected = true;
        }
        #endregion

        #region Skeleton

        private Point getDisplayPosition(Nui.Joint joint)
        {
            float depthX, depthY;
            MainWindow.Instance.NuiRuntime.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);

            depthX = depthX * 320; //convert to 320, 240 space
            depthY = depthY * 240; //convert to 320, 240 space
            int colorX, colorY;
            Nui.ImageViewArea iv = new Nui.ImageViewArea();
            // only ImageResolution.Resolution640x480 is supported at this point

            MainWindow.Instance.NuiRuntime.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(Nui.ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, (short)0, out colorX, out colorY);

            // map back to canvas1.Width & canvas1.Height
            //Console.WriteLine("main window width: " + MainWindow.Instance.canvas1.Width);
            //Console.WriteLine("main window height: " + MainWindow.Instance.canvas1.Height);

            return new Point((double)(MainWindow.Instance.canvas1.ActualWidth * colorX / 640.0), (double)(MainWindow.Instance.canvas1.ActualHeight * 1.2 * colorY / 480));
        }

        Polyline getBodySegment(Microsoft.Research.Kinect.Nui.JointsCollection joints, Brush brush, params Nui.JointID[] ids)
        {
            PointCollection points = new PointCollection(ids.Length);
            for (int i = 0; i < ids.Length; ++i)
            {
                points.Add(getDisplayPosition(joints[ids[i]]));
            }

            Polyline polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 20;
            return polyline;
        }

        void drawJoints(Nui.SkeletonData data, int playerIndx)
        {
            // Draw bones
            Brush brush = Brushes.Black;
            ArrayList CurrentBodySegments = new ArrayList();
            ArrayList CurrentBodyJoints = new ArrayList();
            switch (playerIndx)
            {
                case 1:
                    brush = Brushes.Green;
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
                CurrentBodySegments.Add(getBodySegment(data.Joints, brush, Nui.JointID.HipCenter, Nui.JointID.Spine, Nui.JointID.ShoulderCenter, Nui.JointID.Head));
                CurrentBodySegments.Add(getBodySegment(data.Joints, brush, Nui.JointID.ShoulderCenter, Nui.JointID.ShoulderLeft, Nui.JointID.ElbowLeft, Nui.JointID.WristLeft, Nui.JointID.HandLeft));
                CurrentBodySegments.Add(getBodySegment(data.Joints, brush, Nui.JointID.ShoulderCenter, Nui.JointID.ShoulderRight, Nui.JointID.ElbowRight, Nui.JointID.WristRight, Nui.JointID.HandRight));
                CurrentBodySegments.Add(getBodySegment(data.Joints, brush, Nui.JointID.HipCenter, Nui.JointID.HipLeft, Nui.JointID.KneeLeft, Nui.JointID.AnkleLeft, Nui.JointID.FootLeft));
                CurrentBodySegments.Add(getBodySegment(data.Joints, brush, Nui.JointID.HipCenter, Nui.JointID.HipRight, Nui.JointID.KneeRight, Nui.JointID.AnkleRight, Nui.JointID.FootRight));

                //BodySegments.Add(getBodySegment(data.Joints, brush, JointID.ShoulderRight, JointID.HipRight, JointID.HipLeft, JointID.ShoulderLeft));

                foreach (Polyline p in CurrentBodySegments)
                {
                    p.Opacity = 0.50;

                    // Show Body Skeletons when 'showSkeletons' is true
                    if (MainWindow.Instance.showSkeletons)
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
                foreach (Nui.Joint joint in data.Joints)
                {
                    // Console.WriteLine("join name: " + joint.ID);
                    Point jointPos = getDisplayPosition(joint);
                    if (joint.ID == Nui.JointID.Head)
                    {
                        // Console.WriteLine("FOUND THE HEAD");                      

                        Ellipse el = new Ellipse();
                        el.Width = 50;
                        el.Height = 50;
                        el.Fill = Brushes.Green;
                        el.Opacity = 0.20;

                        Canvas.SetLeft(el, (int)jointPos.X - 25);
                        Canvas.SetTop(el, (int)jointPos.Y - 25);

                        CurrentBodyJoints.Add(el);

                        TextBlock t1 = new TextBlock();
                        t1.Text = "Me";
                        t1.FontSize = 30;
                        t1.Width = 50;
                        t1.Height = 50;

                        Canvas.SetLeft(t1, (int)jointPos.X - 25 + 80);
                        Canvas.SetTop(t1, (int)jointPos.Y - 25 - 20);

                        CurrentBodyJoints.Add(t1);

                    }
                    else if (joint.ID == Nui.JointID.HandRight)
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
                    if (MainWindow.Instance.showSkeletons)
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

        #endregion         
    }
}