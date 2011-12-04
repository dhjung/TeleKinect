using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Kinect.Nui;
using System.Collections;

namespace KinectWhiteboard
{
    public class KinectSkeleton
    {
        public Joint ankleLeft;
        public Joint ankleRight;
        public Joint elbowLeft;
        public Joint elbowRight;
        public Joint footLeft;
        public Joint footRight;
        public Joint handLeft;
        public Joint handRight;
        public Joint head;
        public Joint hipCenter;
        public Joint hipLeft;
        public Joint hipRight;
        public Joint kneeLeft;
        public Joint kneeRight;
        public Joint shoulderCenter;
        public Joint shoulderLeft;
        public Joint shoulderRight; 
        public Joint spine;
        public Joint wristLeft;
        public Joint wristRight;

        public KinectSkeleton(Joint ankleLeft, Joint ankleRight, Joint elbowLeft, Joint elbowRight, Joint footLeft, 
                              Joint footRight, Joint handLeft, Joint handRight, Joint head, Joint hipCenter, 
                              Joint hipLeft, Joint hipRight, Joint kneeLeft, Joint kneeRight, Joint shoulderCenter, 
                              Joint shoulderLeft, Joint shoulderRight, Joint spine, Joint wristLeft, Joint wristRight)
        {
            this.ankleLeft = ankleLeft;
            this.ankleRight = ankleRight;
            this.elbowLeft = elbowLeft;
            this.elbowRight = elbowRight;
            this.footLeft = footLeft;
            this.footRight = footRight;
            this.handLeft = handLeft;
            this.handRight = handRight;
            this.head = head;
            this.hipCenter = hipCenter;
            this.hipLeft = hipLeft;
            this.hipRight = hipRight;
            this.kneeLeft = kneeLeft;
            this.kneeRight = kneeRight;
            this.shoulderCenter = shoulderCenter;
            this.shoulderLeft = shoulderLeft;
            this.shoulderRight = shoulderRight;
            this.spine = spine;
            this.wristLeft = wristLeft;
            this.wristRight = wristRight;
        }

        public KinectSkeleton()
        {
            // TODO: Complete member initialization
        }
    }
}