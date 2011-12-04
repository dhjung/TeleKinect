using System;
using System.Collections;
using System.Collections.Generic;
using System.ServiceModel;

namespace KinectWhiteboardServer
{

    #region 1. Contract Interface (Client to Server)

    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(IChatCallback))]
    interface IChat
    {
        [OperationContract(IsOneWay = false, IsInitiating = true, IsTerminating = false)]
        string[] Join(string name);

        [OperationContract(IsOneWay = true, IsInitiating = false, IsTerminating = false)]
        //void Say(string msg);
        void Say(int x, int y, string z);

        [OperationContract(IsOneWay = true, IsInitiating = false, IsTerminating = false)]
        //void Whisper(string to, string msg);
        void Whisper(int imageNumber, bool isMoving);

        [OperationContract(IsOneWay = true, IsInitiating = false, IsTerminating = false)]
        void Start(string name);

        [OperationContract(IsOneWay = true, IsInitiating = false, IsTerminating = false)]
        void Reset(string name);

        [OperationContract(IsOneWay = true, IsInitiating = false, IsTerminating = true)]
        void Leave();
    }
    #endregion

    #region 2. CallBackContract  (Server to Client)

    interface IChatCallback
    {
        [OperationContract(IsOneWay = true)]
        //void Receive(string senderName, string message);
        void Receive(string senderName, int x, int y, string z);

        [OperationContract(IsOneWay = true)]
        //void ReceiveWhisper(string senderName, string message);
        void ReceiveWhisper(string senderName, int imageNumber, bool isMoving);

        [OperationContract(IsOneWay = true)]
        void ReceiveStart(string senderName, int [] posX, int [] posY);

        [OperationContract(IsOneWay = true)]
        void ReceiveReset(string senderName);

        [OperationContract(IsOneWay = true)]
        void UserEnter(string name);

        [OperationContract(IsOneWay = true)]
        void UserLeave(string name);
    }
    #endregion

    #region 3. Message Type & EventArgs

    // Message Type
    public enum MessageType { Receive, UserEnter, UserLeave, ReceiveWhisper, ReceiveStart, ReceiveReset };

    // ChatEventArgs
    public class ChatEventArgs : EventArgs
    {
        public MessageType msgType;
        public string name;
        public string message;
        public int x;
        public int y;
        public int [] posX = new int[9];
        public int [] posY = new int[9];
        public string z;
        public int imageNumber;
        public bool isMoving;
    }

    #endregion

    #region 4. Service Implementation (Client to Server)

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ChatService : IChat
    {

        #region 4.1. Global variables and Methods
        
        // Object for syncronizing process 
        private static Object syncObj = new Object();

        IChatCallback callback = null;

        // Callback Deligate and Event
        public delegate void ChatEventHandler(object sender, ChatEventArgs e);
        public static event ChatEventHandler ChatEvent;

        // Collection which contain user name (for key) and event handler (for value)
        static Dictionary<string, ChatEventHandler> chatters = new Dictionary<string, ChatEventHandler>();

        // user name (key)
        private string name;
        // chat event 
        private ChatEventHandler myEventHandler = null;


        /// <summary>
        /// Broadcasting event to all clients
        /// </summary>
        /// <param name="e"></param>
        private void BroadcastMessage(ChatEventArgs e)
        {
            // Event
            ChatEventHandler temp = ChatEvent;

            if (temp != null)
            {
                // Send current evnets
                foreach (ChatEventHandler handler in temp.GetInvocationList())
                {
                    handler.BeginInvoke(this, e, new AsyncCallback(EndAsync), null);
                }
            }
        }

        /// <summary>
        /// Relay event to all clients
        /// </summary>
        /// <param name="e"></param>
        private void RelayMessage(ChatEventArgs e)
        {
            // Event
            ChatEventHandler temp = ChatEvent;

            if (temp != null)
            {
                // Send current evnets
                foreach (ChatEventHandler handler in temp.GetInvocationList())
                {
                    if (handler != myEventHandler)
                    {
                        handler.BeginInvoke(this, e, new AsyncCallback(EndAsync), null);
                    }
                }
            }
        }

        /// <summary>
        /// Send events to client
        /// </summary>
        /// <param name="ar"></param>
        private void EndAsync(IAsyncResult ar)
        {
            ChatEventHandler d = null;

            try
            {
                System.Runtime.Remoting.Messaging.AsyncResult asres = (System.Runtime.Remoting.Messaging.AsyncResult)ar;
                d = ((ChatEventHandler)asres.AsyncDelegate);
                d.EndInvoke(ar);
            }
            catch
            {
                ChatEvent -= d;
            }
        }

        /// <summary>
        /// Raise Event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyEventHandler(object sender, ChatEventArgs e)
        {
            try
            {
                switch (e.msgType)
                {
                    case MessageType.Receive:
                        // callback.Receive(e.name, e.message);
                        callback.Receive(e.name, e.x, e.y, e.z);
                        Console.WriteLine("callback Receive from " + e.name + "(" + e.x + ", " + e.y + ")");
                        break;
                    case MessageType.ReceiveWhisper:
                        //callback.ReceiveWhisper(e.name, e.message);
                        callback.ReceiveWhisper(e.name, e.imageNumber, e.isMoving);
                        Console.WriteLine("callback ReceiveWhisper from " + e.name + "(" + e.imageNumber + ", " + e.isMoving + ")");
                        break;
                    case MessageType.ReceiveStart:
                        callback.ReceiveStart(e.name, e.posX, e.posY);
                        Console.WriteLine("callback Start request from " + e.name);
                        break;
                    case MessageType.ReceiveReset:
                        callback.ReceiveReset(e.name);
                        Console.WriteLine("callback Reset request from " + e.name);
                        break;
                    case MessageType.UserEnter:
                        callback.UserEnter(e.name);
                        Console.WriteLine("callback UserEnter " + e.name);
                        break;
                    case MessageType.UserLeave:
                        callback.UserLeave(e.name);
                        Console.WriteLine("callback UserLeave " + e.name);
                        break;
                }
            }
            catch
            {
                Leave();
            }
        }

        #endregion

        #region 4.2. JOIN

        /// <summary>
        /// * JOIN 
        /// When the user join the chat room first
        /// </summary>
        /// <param name="name">User Name</param>
        /// <returns>Return user list if there are no the same name in the list</returns>
        public string[] Join(string name)
        {
            Console.WriteLine("User Name: " + name);
            
            myEventHandler = new ChatEventHandler(MyEventHandler);

            lock (syncObj)
            {
                if (!chatters.ContainsKey(name))// Check there are the same name in the list
                {
                    // Add name and event
                    this.name = name;
                    chatters.Add(name, MyEventHandler);
          
                    // Set the channel for user
                    callback = OperationContext.Current.GetCallbackChannel<IChatCallback>();

                    // Send "UserEnter" event
                    ChatEventArgs e = new ChatEventArgs();
                    e.msgType = MessageType.UserEnter;
                    e.name = name;
                    RelayMessage(e);

                    // Add Deligater
                    ChatEvent += myEventHandler;

                    // Send the user list
                    string[] list = new string[chatters.Count];
                    
                    lock (syncObj)
                    {
                        chatters.Keys.CopyTo(list, 0);
                    }
                    return list;
                }
                else // If the there are the same name in the list 
                {
                    return null;
                }
            }
        }
        #endregion

        #region 4.3. Say

        //public void Say(string msg)
        public void Say(int x, int y, string z)
        {
            ChatEventArgs e = new ChatEventArgs();
            e.msgType = MessageType.Receive;
            e.name = this.name;
            // e.message = msg;

            //Console.WriteLine("server: " + " I got what you are saying");

            e.x = x;
            e.y = y;
            e.z = z;

            RelayMessage(e);
        }
        #endregion

        #region 4.4. Whisper

        //public void Whisper(string to, string msg)
        public void Whisper(int imageNumber, bool isMoving)
        {
            ChatEventArgs e = new ChatEventArgs();
            e.msgType = MessageType.ReceiveWhisper;
            e.name = this.name;
            
            e.imageNumber = imageNumber;
            e.isMoving = isMoving;
            // e.message = msg;
            RelayMessage(e);
            
            //try
            //{
            //    ChatEventHandler chatterTo;
            //    lock (syncObj)
            //    {
            //        chatterTo = chatters[to];
            //    }
            //    chatterTo.BeginInvoke(this, e, new AsyncCallback(EndAsync), null);
            //}
            //catch (KeyNotFoundException)
            //{
                // When errors occur 
            //}
        }
        #endregion

        #region 4.5. Leave Chat Room
        
        public void Leave()
        {
            if (this.name == null) return;

            lock (syncObj)
            {
                chatters.Remove(this.name);
            }
            ChatEvent -= myEventHandler;

            // Raise New Event
            ChatEventArgs e = new ChatEventArgs();
            e.msgType = MessageType.UserLeave;
            e.name = this.name;
            RelayMessage(e);
        }

        #endregion 

        #region 4.6 Start

        public void Start(string name)
        {
            ChatEventArgs e = new ChatEventArgs();
            e.msgType = MessageType.ReceiveStart;
            e.name = this.name;

            // Generate Random Position
            Random random = new Random();

            for (int i = 0; i < e.posX.Length; i++)
            {
                int _tempX = random.Next(0, 900);
                int _tempY = random.Next(100, 600);
                
                // Avoid overlapping between points
                for (int j = 0; j <= i; j++)
                {
                    // Ignore check logic for the first location
                    if (i > 0 && e.posX[j] > 0)
                    {
                        while ((Math.Abs(e.posX[j] - _tempX) < 200) && (Math.Abs(e.posY[j] - _tempY) < 200))
                        {
                            _tempX = random.Next(0, 900);
                            _tempY = random.Next(100, 600);

                            j = 0;
                        }
                    }
                }

                e.posX[i] = _tempX;
                e.posY[i] = _tempY;
            }

            // Broadcast to all clients
            BroadcastMessage(e);
        }

        #endregion

        #region 4.7 Reset

        public void Reset(string name)
        {
            ChatEventArgs e = new ChatEventArgs();
            e.msgType = MessageType.ReceiveReset;
            e.name = this.name;

            // Broadcast to all clients
            BroadcastMessage(e);
        }

        #endregion
    }
    #endregion

}