using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using static ChatVisual.RawInputHook;


namespace ChatVisual
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // To make the app invisible for screen sharing and screenshot tools
        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);


        // declare Agent client
        private ClaudeClient _claudeClient;
        private OpenAIWrapper _openAIWrapper;
        private ModeOrchestrator _modeOrchestrator;

        private RawInputHook rawInputHook;

        private ObservableCollection<string> currentScreenshotList = new ObservableCollection<string>();


        // this is a special class that act similar to react hook, it's notify UI when change
        ObservableCollection<ConversationMessage> Messages;

        public MainWindow()
        {
            InitializeComponent();

            // claude client
            _claudeClient = new ClaudeClient();

            // openAI client
            _openAIWrapper = new OpenAIWrapper();

            // orchestrator for the ai agents
            _modeOrchestrator = new ModeOrchestrator();

            // intitialize the messages
            Messages = new ObservableCollection<ConversationMessage>();
            ChatHistory.ItemsSource = Messages;

            // set the source for the ScreenshotHistory (to link between the xaml UI and the currentScreenshotList)
            ScreenshotHistory.ItemsSource = currentScreenshotList;

            // if the Orchestrator shout Mode Changed... then we have to clear the Messages List so that the UI in sync with the AI agen Chat History
            _modeOrchestrator.OnModeChange += () =>
            {
                Messages.Clear();
            };

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

            // set the Display Affinity to make the window invisible to screen sharing tool
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetWindowDisplayAffinity(hwnd, 0x00000011);

            // we register the raw input for keyboard
            rawInputHook.RegisterMacroKeyAction(112, MoveWindowUp);
            rawInputHook.RegisterMacroKeyAction(113, MoveWindowDown);
            rawInputHook.RegisterMacroKeyAction(114, MoveWindowLeft);
            rawInputHook.RegisterMacroKeyAction(115, MoveWindowRight);
            rawInputHook.RegisterMacroKeyAction(116, TakeScreenshot);
            rawInputHook.RegisterMacroKeyAction(117, sendMessageToOrchestrator);

            // temporary key for mode change
            rawInputHook.RegisterMacroKeyAction(118, _modeOrchestrator.CycleThroughMode);

        }

        // this is for the window close, we need to shutdown the raw input hook to release the handle
        // this connect to the Closed event in the <Window> tag in the MainWindow.xaml
        private void Window_Closed(object sender, EventArgs e)
        {
            rawInputHook?.Shutdown();
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

            // get DPI scale factor from WPF
            PresentationSource source = PresentationSource.FromVisual(this);
            double dpiScale = source.CompositionTarget.TransformToDevice.M11;

            // physical screen size
            int width = (int)(SystemParameters.PrimaryScreenWidth * dpiScale);
            int height = (int)(SystemParameters.PrimaryScreenHeight * dpiScale);
            
            using (Bitmap myBitmap = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(myBitmap))
                {
                    g.CopyFromScreen(new System.Drawing.Point(0, 0),
                    new System.Drawing.Point(0, 0),
                    new System.Drawing.Size(width, height)
                    );

                    /*
                     * Loggin the screenshot
                    Directory.CreateDirectory("screenshots");
                    string fileName = $"screenshots/screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    myBitmap.Save(fileName);
                    */
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
        private async void sendMessageToOrchestrator()
        {
            string text = MessageInput.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text) && currentScreenshotList.Count == 0)
                return;

            try
            {
                Console.WriteLine(text);

                Messages.Add(new ConversationMessage() { Role = "User", Content = text ?? "" });
                await Task.Delay(1);

                // string response = await _claudeClient.sendMessage(text ?? "", currentScreenshotList.ToList<string>());
                string response = await _modeOrchestrator.GetResponseAsync(text ?? "", currentScreenshotList.ToList<string>());

                Messages.Add(new ConversationMessage() { Role = "Assistant", Content = response });

                MessageInput.Text = "";
                currentScreenshotList.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message from the Main Window to Orchestrator. Error Message: {ex.Message}");
                Messages.Add(new ConversationMessage() { Role = "Assistant", Content = "Something went wrong pls try again" });
            }

        }

    }
}
