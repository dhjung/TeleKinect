using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Peers;

namespace KinectWhiteboard
{
    /// <summary>
    /// Extends a standard button with the ability to respond to Kinect cursor hover-over events
    /// </summary>
    public class KinectButton : Button
    {
        #region Data

        IInvokeProvider _invoker;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public KinectButton()
        {
            KinectCursor.AddCursorEnterHandler(this, OnCursorEnter);
            KinectCursor.AddCursorLeaveHandler(this, OnCursorLeave);

            // Get the automation peer that can invoke this button properly.
            _invoker = (IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(this).GetPattern(PatternInterface.Invoke);
        }

        #region Internal

        // Called when the cursor enters this button's visible area
        private void OnCursorEnter(object sender, CursorEventArgs args)
        {
            args.Cursor.BeginHover();

            args.Cursor.HoverFinished += Cursor_HoverFinished;
        }

        // Called when the cursor leaves this button's visible area
        private void OnCursorLeave(object sender, CursorEventArgs args)
        {
            args.Cursor.EndHover();

            args.Cursor.HoverFinished -= Cursor_HoverFinished;
        }

        // Called when the hover action has finished, and the button should be pressed
        private void Cursor_HoverFinished(object sender, EventArgs e)
        {
            // Using the automation provider to invoke the button is the only way to really emulate a mouse clicking on it in WPF.
            _invoker.Invoke();

            ((KinectCursor)sender).HoverFinished -= Cursor_HoverFinished;
        }

        #endregion
    }
}
