using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace KinectWhiteboard
{
    /// <summary>
    /// Parameters passed with KinectCursor.CursorEnter and CursorLeave events
    /// </summary>
    public class CursorEventArgs : RoutedEventArgs
    {
        #region Data

        KinectCursor _cursor;
        int _timestamp;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cursor">The cursor interacting with the element</param>
        /// <param name="routedEvent">The event being raised</param>
        /// <param name="source">The element raising the event</param>
        public CursorEventArgs(KinectCursor cursor, RoutedEvent routedEvent, object source)
            : base(routedEvent, source)
        {
            _cursor = cursor;
            _timestamp = Environment.TickCount;
        }

        #region Properties

        /// <summary>
        /// Gets the cursor that interacted with the source element to raise this event
        /// </summary>
        public KinectCursor Cursor { get { return _cursor; } }

        /// <summary>
        /// Gets the timestamp of the event (according to Environment.TickCount)
        /// </summary>
        public int Timestamp { get { return _timestamp; } }

        #endregion
    }
}
