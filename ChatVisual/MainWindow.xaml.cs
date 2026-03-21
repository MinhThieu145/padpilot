using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using static ChatVisual.RawInputHook;


namespace ChatVisual
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {


        // declare claude client
        private ClaudeClient claudeClient;
        private RawInputHook rawInputHook;

        private System.Collections.Generic.List<string> currentScreenshotList = new System.Collections.Generic.List<string>();

        // this is a special class that act similar to react hook, it's notify UI when change
        ObservableCollection<ChatMessage> Messages;

        public MainWindow()
        {
            InitializeComponent();

            // claude client
            claudeClient = new ClaudeClient();


            // intitialize the messages
            Messages = new ObservableCollection<ChatMessage>();
            ChatHistory.ItemsSource = Messages;

            // Confirm if this is 64 bit or 32 bit process, and the size of the RawInputHeader struct
            Console.WriteLine($"IntPtr.Size = {IntPtr.Size}");
            Console.WriteLine($"Is64BitProcess = {Environment.Is64BitProcess}");
            Console.WriteLine($"RawInputHeader size = {Marshal.SizeOf<RawInputHeader>()}");

        }

        // this is for the window handle.
        // This make sure the window handle is fully ready
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // we declare the rawInputHook here since we need its handle 
            rawInputHook = new RawInputHook(this);

            // we register the raw input for keyboard
            rawInputHook.RegisterMacroKeyAction(112, MoveWindowUp);
            rawInputHook.RegisterMacroKeyAction(113, MoveWindowDown);
            rawInputHook.RegisterMacroKeyAction(114, MoveWindowLeft);
            rawInputHook.RegisterMacroKeyAction(115, MoveWindowRight);
            rawInputHook.RegisterMacroKeyAction(116, TakeScreenshot);
            rawInputHook.RegisterMacroKeyAction(117, sendMessageToClaude);


        }

        // this is for the window close, we need to shutdown the raw input hook to release the handle
        // this connect to the Closed event in the <Window> tag in the MainWindow.xaml
        private void Window_Closed(object sender, EventArgs e)
        {
            rawInputHook?.Shutdown();
        }


        /// <summary>
        /// Handle the Send Message button click
        /// </summary>
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            sendMessageToClaude();
        }


        /// <summary>
        /// Handle the Screenshot button click
        /// </summary>
        private void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            TakeScreenshot();
        }

        // =====================================================================
        // EVENT HANDLER
        // =====================================================================

        /// <summary>
        /// Move the Window Handle up by 10 units
        /// </summary>
        private void MoveWindowUp()
        {
            this.Top -= 10;
        }

        /// <summary>
        /// Move the Window Handle down by 10 units
        /// </summary>
        private void MoveWindowDown()
        {
            this.Top += 10;
        }

        /// <summary>
        /// Move the Window Handle left by 10 units
        /// </summary>
        private void MoveWindowLeft()
        {
            this.Left -= 10;
        }

        /// <summary>
        /// Move the Window Handle right by 10 units
        /// </summary>
        private void MoveWindowRight()
        {
            this.Left += 10;
        }



        /// <summary>
        /// Take the screenshot of the current window 
        /// </summary>
        private void TakeScreenshot()
        {
            Console.WriteLine("Screenshot taken");

            // the image bitmap
            int width = (int)SystemParameters.PrimaryScreenWidth;
            int height = (int)SystemParameters.PrimaryScreenHeight;

            using (Bitmap myBitmap = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(myBitmap))
                {
                    g.CopyFromScreen(new System.Drawing.Point(0, 0),
                    new System.Drawing.Point(0, 0),
                    new System.Drawing.Size(width, height)
                    );

                    // myBitmap.Save("screenshot.png");
                }


                // get create a memory stream
                using (MemoryStream ms = new MemoryStream())
                {
                    // now we save the bitmap we have earlier into the stream
                    // instead of save to disk
                    myBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                    // we pour everything from the stream to a byte array
                    // if you use Read() you would have to care about the position of the reader
                    byte[] bytes = ms.ToArray();

                    // convert that to base64
                    string base64 = Convert.ToBase64String(bytes);

                    currentScreenshotList.Add(base64);
                }

            }

        }


        /// <summary>
        /// Send the current message to Claude Client 
        /// </summary>
        private async void sendMessageToClaude()
        {
            string text = MessageInput.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text) && currentScreenshotList.Count == 0)
                return;

            SendMessageButton.IsEnabled = false;

            try
            {
                Console.WriteLine(text);

                Messages.Add(new ChatMessage() { Role = "User", Content = text ?? "" });
                await Task.Delay(1);

                string response = await claudeClient.sendMessage(text ?? "", currentScreenshotList);

                Messages.Add(new ChatMessage() { Role = "Assistant", Content = response });

                MessageInput.Text = "";
                currentScreenshotList.Clear();
            }
            finally
            {
                SendMessageButton.IsEnabled = true;
            }

        }



    }
}
