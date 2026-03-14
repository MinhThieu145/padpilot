using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ChatVisual
{

    /// <summary>
    /// Intercepts keyboard input from a specific HID macro keypad using two Windows
    /// mechanisms working in tandem.
    ///
    /// ┌─────────────────────────────────────────────────────────────────┐
    /// │  ARCHITECTURE: WHY TWO HOOKS?                                   │
    /// │                                                                 │
    /// │  Raw Input API          → identifies WHICH device sent the key  │
    /// │                           but CANNOT swallow the event          │
    /// │                                                                 │
    /// │  Low-Level Keyboard Hook → CAN swallow events                   │
    /// │                            but CANNOT identify the device       │
    /// │                                                                 │
    /// │  Together: Raw Input fires first, identifies the source.        │
    /// │  The hook fires ~1ms later and decides whether to block it.     │
    /// └─────────────────────────────────────────────────────────────────┘
    ///
    /// FLOW PER KEYPRESS
    ///   1. Physical key pressed on macro keypad (F1–F9)
    ///   2. HandleRawInput()         → WM_INPUT arrives via HwndSource hook
    ///                                 Checks if key came from _targetDeviceHandle
    ///   3. LowLevelKeyboardFilter() → fires for every key system-wide
    ///                                 Swallows F1–F9 if the event is NOT injected
    ///   4. If key came from a regular keyboard:
    ///                                 InjectKeyboardEvent() re-fires it so the
    ///                                 rest of the system still sees it normally
    ///
    /// KNOWN LIMITATIONS
    ///   - Device matching uses VID/MI string in device path. If the macro keypad
    ///     is unplugged and reconnected, _targetDeviceHandle may change.
    ///   - Injected event detection (LLKHF_INJECTED flag 0x10) is not 100% accurate.
    ///     Software like the on-screen keyboard also injects events.
    /// </summary>


    internal class RawInputHook
    {

        // =====================================================================
        // P/INVOKE: RAW INPUT API
        // Used to enumerate connected devices, register to receive WM_INPUT
        // messages, and read the raw data from those messages.
        // Docs: https://learn.microsoft.com/en-us/windows/win32/inputdev/raw-input
        // =====================================================================


        /// <summary>
        /// This is call the "two-call pattern" -> basically we need to pass in the size of sth
        /// But we can only get the size of that thing by calling it first with a null value
        /// GetRawInputDeviceList requires 2 separated method declaration because the pRawInputDeviceList needs to be IntPtr for the first call (to get the count) 
        /// and an array for the second call (to get the actual device list). We can't just use IntPtr for both call because we need to pass in the pre-allocated array for the second call, and we can't do that with IntPtr.
        /// <para>
        /// First call: pass IntPtr.Zero to retrieve only the device COUNT into puiNumDevices.
        /// </para>
        /// </summary>
        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint GetRawInputDeviceList(
            IntPtr pRawInputDeviceList, // because we need to pass null to this to get the number of device (field 2)
            ref uint puiNumDevices,
            uint cbSize
        );

        /// <summary>
        /// Second call: pass a pre-allocated array to fill in the actual device list.
        /// </summary>
        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint GetRawInputDeviceList(
            [Out] RawInputDeviceList[] pRawInputDeviceList, // now this is the array that we can get the actual device list
            ref uint puiNumDevices,
            uint cbSize
        );

        /// <summary>
        /// Retrieves information about a raw input device (e.g. its name/path).
        /// Call twice: first with IntPtr.Zero to get the required buffer size,
        /// then again with an allocated buffer to get the actual data.
        /// </summary>
        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize
        );


        /// <summary>
        /// Registers this window to receive WM_INPUT messages from specific device types.
        /// Must be called before any raw input data will arrive.
        /// </summary>
        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterRawInputDevices(
            RawInputDevice[] rawInputDevices,
            uint uiNumDevices,
            uint cbSize
        );


        /// <summary>
        /// Reads the raw input data from a WM_INPUT message.
        /// Call twice: first with IntPtr.Zero to get the required buffer size,
        /// then again with an allocated buffer to get the actual data.
        /// </summary>
        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            [Out] IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader
        );


        // =====================================================================
        // P/INVOKE: LOW-LEVEL KEYBOARD HOOK
        // Used to intercept and optionally swallow keystrokes system-wide,
        // before they reach any application window.
        // Docs: https://learn.microsoft.com/en-us/windows/win32/winmsg/hooks
        // =====================================================================


        /// <summary>
        /// Installs a global keyboard hook. idHook=13 means WH_KEYBOARD_LL.
        /// Returns a handle used to remove the hook later.
        /// dwThreadId=0 hooks all threads in the system (required for global hooks).
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);


        /// <summary>
        /// Removes a previously installed hook. Pass the handle returned by SetWindowsHookEx.
        /// Always call this on shutdown to avoid a leaked hook.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);


        /// <summary>
        /// Passes a hook event to the next hook in the chain.
        /// Must be called for any event we are NOT swallowing, otherwise
        /// we silently break keyboard input for the entire system.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);


        /// <summary>
        /// Returns a handle to the current process module.
        /// Passing null retrieves the handle of the running executable,
        /// which is what SetWindowsHookEx requires for the hMod parameter
        /// when installing a global hook.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);



        // =====================================================================
        // P/INVOKE: INPUT INJECTION
        // Used to re-fire key events that we intercepted from non-macro keyboards.
        // When a regular keyboard presses F1–F9, we swallow it at the hook level
        // and re-inject it so the OS still sees it as a normal keypress.
        // Docs: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
        // =====================================================================


        /// <summary>
        /// Synthesizes keystrokes, mouse motions, and button clicks.
        /// cInputs = number of elements in pInputs array.
        /// cbSize   = Marshal.SizeOf(tagINPUT) — must match exactly or the call fails silently.
        /// </summary>
        [DllImport("User32.dll", SetLastError = true)]
        private static extern uint SendInput(
            uint cInputs,
            tagINPUT[] pInputs,
            int cbSize
        );




        // =====================================================================
        // STRUCTS: RAW INPUT
        // These mirror Windows API structs exactly. Layout must not be changed.
        // Docs: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinput
        // =====================================================================


        /// <summary>
        /// Represents one entry in the raw input device list.
        /// hDevice is the opaque handle used to identify the device later.
        /// dwType: 0 = mouse, 1 = keyboard, 2 = HID
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDeviceList
        {
            public IntPtr hDevice; // this is handle so it's the pointer (IntPrt)
            public uint dwType;
        }


        /// <summary>
        /// Passed to RegisterRawInputDevices to declare which device types we want
        /// and which window should receive their WM_INPUT messages.
        /// usUsagePage = 0x0001 → Generic Desktop Controls (HID spec)
        /// usUsage     = 0x0006 → Keyboard (under page 0x0001)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDevice
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        /// <summary>
        /// The header portion of every WM_INPUT message.
        /// dwType identifies whether the input came from mouse, keyboard, or HID.
        /// hDevice lets us match the event to our target device handle.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputHeader
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }


        /// <summary>
        /// Keyboard-specific data inside a raw input message.
        /// VKey    = virtual key code 
        /// Flags   = RI_KEY_BREAK (0x0001) means key-up; 0 means key-down
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct tagRawKeyBoard
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey; // see: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
            public uint Message;
            public uint ExtraInformation;
        }


        // These structs exist because RAWINPUT uses a C-style union in memory.
        // We only use the keyboard variant, but all three must be present so the
        // union is the correct total size when marshalled.
        // THIS IS for the RAWHID struct, which is part of the UNION for the RawInput

        [StructLayout(LayoutKind.Sequential)]
        public struct tagRAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
            public byte bRawData;
        }

        // THIS IS for the tagRawMouse struct, which is part of the UNION for the RawInput
        [StructLayout(LayoutKind.Sequential)]
        public struct RawMouseButtonsData
        {
            public ushort usButtonFlags;
            public ushort usButtonData;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RawMouseButtonsUnion
        {
            [FieldOffset(0)] public uint ulButtons;
            [FieldOffset(0)] public RawMouseButtonsData data;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tagRAWMOUSE
        {
            public ushort usFlags;
            public RawMouseButtonsUnion buttons;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }


        /// <summary>
        /// The union that sits after RawInputHeader in memory.
        /// All three fields start at the same offset (they share memory).
        /// Read only the field that matches header.dwType.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct RawInputUnion
        {

            [FieldOffset(0)] public tagRAWMOUSE mouse;
            [FieldOffset(0)] public tagRawKeyBoard keyboard;
            [FieldOffset(0)] public tagRAWHID hid;
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct RawInput
        {
            public RawInputHeader header;
            public RawInputUnion data;
        }



        // =====================================================================
        // STRUCTS: KEYBOARD HOOK
        // Read from lParam inside the low-level keyboard hook callback.
        // Docs: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct
        // =====================================================================

        /// <summary>
        /// Contains details about a low-level keyboard event.
        /// vkCode = which key was pressed.
        /// flags  = bit field; bit 4 (0x10) = LLKHF_INJECTED (event was not from hardware).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode; // the key code represent the sort of event (pls read here: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes_
            public uint scanCode; // the hardware scan code for the key
            public uint flags; // ????
            public uint time; // the timestamp for this event
            public UIntPtr dwExtraInfo;
        }


        // =====================================================================
        // STRUCTS: INPUT INJECTION (SendInput)
        // These mirror the INPUT / KEYBDINPUT structs from winuser.h.
        // Docs: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-input
        // =====================================================================


        // Mouse and hardware input structs are required by the union even though
        // we only use the keyboard variant. Do not remove them.
        [StructLayout(LayoutKind.Sequential)]
        public struct tagMOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tagHARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamR;
        }

        /// <summary>
        /// Keyboard-specific input for SendInput.
        /// wVk     = virtual key code of the key to simulate.
        /// dwFlags = 0 for key-down, KEYEVENTF_KEYUP (0x0002) for key-up.
        /// time    = 0 lets the system assign the timestamp.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct tagKEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan; // when we don't have the keycode to describe the event then we have this wScan code
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo; // additional value associated with the keystroke
        }


        // Then now we build the UNION struct for the tagInput struct
        [StructLayout(LayoutKind.Explicit)]
        public struct tagInputUnion
        {
            [FieldOffset(0)] public tagMOUSEINPUT mi;
            [FieldOffset(0)] public tagKEYBDINPUT ki;
            [FieldOffset(0)] public tagHARDWAREINPUT hi;
        }

        /// <summary>
        /// Top-level struct for SendInput.
        /// type = 0 (mouse), 1 (keyboard), 2 (hardware).
        /// We always use type = 1 (keyboard).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct tagINPUT
        {
            public uint type; // the type of the input (mouse, keyboard, or hardware)
            public tagInputUnion tagInputUnion; // the actual input data (the union of mouse, keyboard, and hardware input)
        }



        // =====================================================================
        // CONSTANTS
        // =====================================================================


        // Windows message sent by Raw Input API when device data is ready.
        private const int WM_INPUT = 0x00FF;

        // Raw input registration flags.
        private const uint RIDEV_INPUTSINK = 0x00000100; // receive input even when app is not in focus



        // =====================================================================
        // FIELDS
        // =====================================================================
        /// <summary>
        /// Handle to the macro keypad device, retrieved during initialization.
        /// Used in HandleRawInput to filter events to only my macro device.
        /// </summary>
        private IntPtr _targetDeviceHandle = IntPtr.Zero;


        /// <summary>
        /// Handle to the application window. Required for RegisterRawInputDevices
        /// and for attaching the HwndSource hook.
        /// </summary>
        private IntPtr _hwnd;


        // A callback function to use with SetWindowsHookEx
        // nCode: only have 1 value = 0. But technically we have to check for nCode (nCode >= 0)
        // if nCode < 0 that mean the window told us this is system message and must be passed to CallNextHookEx

        // wParam and lParam are pointer size that contain extra info window gave us (so those are not pointer)
        // in this case: WPARAM has info about the event (like keydown, keyup, etc)
        // LPARAM has info about the key (like which key is it)
        /// <summary>
        /// Delegate for the low-level keyboard hook callback (SetWindowsHookEx)
        /// Must be kept as a field to prevent garbage collection — if GC collects it,
        /// Windows will call a dangling pointer and crash the process.
        /// </summary>
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _lowLevelKeyboardProc;


        /// <summary>
        /// Handle returned by SetWindowsHookEx. Used to remove the hook on shutdown.
        /// </summary>
        private IntPtr _macroHookHandle;


        /// <summary>
        /// WPF interop source and its hook delegate. Kept as fields to prevent GC.
        /// </summary>
        private HwndSource _source;
        private HwndSourceHook _sourceHook;


        /// <summary>
        /// Debug counter — tracks how many WM_INPUT messages have arrived.
        /// </summary>
        private int countingEvent = 0;



        // =====================================================================
        // CONSTRUCTOR
        // =====================================================================
        public RawInputHook(Window window)
        {
            Console.WriteLine("[RawInputHook] Initializing...");

            // this is literally mean handle to the window
            _hwnd = new WindowInteropHelper(window).Handle;

            // Let install the hook:
            // This is the hook to read and intercept the input events from window
            InstallMacroHook();


            // We first list our devices
            (RawInputDeviceList[] deviceList, uint result) = ListDevice();

            if (result == uint.MaxValue)  // this is actually the uint -1 in the doc
            {
                Console.WriteLine("Failed to get device list");
                int errorCode = Marshal.GetLastWin32Error();
                string errorMessage = new Win32Exception(errorCode).Message;
                Console.WriteLine($"Error {errorCode}: {errorMessage}");

                return;
            }
            else
            {
                // then we scan through the device list and find it
                bool isFoundTarget = FindTargetDevice(deviceList);
                if (isFoundTarget)
                {
                    Console.WriteLine("Found target device with handle: " + _targetDeviceHandle);
                }
                else
                {
                    Console.WriteLine("Target device not found");
                    return;
                }



                // Then we need to register that device
                bool isRegisterSuccessful = RegisterDevice();

                if (isRegisterSuccessful)
                {
                    Console.WriteLine("Register device succesfully");
                }
                else
                {
                    Console.WriteLine("Register device NOT succesful");
                    int errorCode = Marshal.GetLastWin32Error();
                    Console.WriteLine("Register Success " + errorCode);

                    return;
                }



                // then now we need the HwndSource (this is so confusing so pls look this up)
                // but basically this capture all the message sent to THIS WINDOW (and not other application)
                // Also if you wondering if this hook receive the message first or our LowLevel receive first -> then the low level receive first
                // The only reason the message for RawInput come first is because Raw Input process the message SO FAST it comes out first
                _source = HwndSource.FromHwnd(_hwnd);
                _sourceHook = HandleRawInput;
                _source.AddHook(_sourceHook);

            }

        }


        // INSTALL HOOK METHOD
        private void InstallMacroHook()
        {
            int idHook = 13; // a hook procedure that monitors low-level keyboard input events

            // the delegate (the hook procedure).
            // we simply need to declare it to get the pointer to it (which is what SetWindowsHookEx need)

            // since it's a delegate (a callback) we need to pass the actual function that match the delegate (the function is HandleRawInput)
            _lowLevelKeyboardProc = new LowLevelKeyboardProc(LowLevelKeyboardFilter);

            // null → GetModuleHandle returns the handle of the running .exe.
            // This is required for global hooks to locate our callback.
            IntPtr currModuleHandle = GetModuleHandle(null);

            // dwThreadId = 0 → hook all threads in the system (global hook).
            uint dwThreadId = 0;

            _macroHookHandle = SetWindowsHookEx(idHook, _lowLevelKeyboardProc, currModuleHandle, dwThreadId);


        }



        // this is the function that our hook would call when it receive event from the hook chain
        private IntPtr LowLevelKeyboardFilter(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                // this mean the window told us this is system message and must be passed to CallNextHookEx
                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }
            else
            {
                // we can decide to swallow these event of keep passing them

                // Okay we have a big problem: we only know the key, but not the device from the wParam and lParam -> don't know if it's our macro or not
                // but the RawInput earlier can tell the device, but then it can't stop the event
                // so the genius plan. We track the timestamp that the last macro was push down
                // because RawInput happen before the LowLevel Hook, it a macro was press down about 15ms before it reach here: it's very likely (like 99.999%) that key is the macro key


                // we check if the key is a macro key (F1 - F9) and if the timestamp is within 15ms of the last macro key event
                long now = Environment.TickCount;

                Console.WriteLine("Low Level Hook got event! This is the data:");

                // Now we read from the lParam to get the actual key info (which key is it, etc)
                KBDLLHOOKSTRUCT kbdStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                // THIS IS AN INSANELY COMPLICATED CONDITION
                // but in general, it check if the event is injected (not from physical keyboard)
                // why: because the current logic is: all the event come from macro would be injected (we stop everything from F1 - F9 at low level)
                // but the RawInput reject them IF THEY FROM THE MACRO
                // but the way the condition works involve confusing bitwise calculation (because this flag is confusing)
                // learn more: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct 
                // LIMITATION: this is not 100% accurate, stuff like the on-screen keyboad also inject event (but it's good enough for now)
                bool isInjected = (kbdStruct.flags & 0x10) != 0;
                if (isInjected)
                {
                    return CallNextHookEx(_macroHookHandle, nCode, wParam, lParam);
                }

                // we swallow the event if it's F1 - F9  and are not injected event
                // 

                if (kbdStruct.vkCode >= 112 && kbdStruct.vkCode <= 120) // F1-F9 only
                {
                    return (IntPtr)1; // swallow
                }

                return CallNextHookEx(_macroHookHandle, nCode, wParam, lParam); // everything else passes

            }
        }



        // LIST DEVICES METHOD
        // this method is to list all the device that we have.
        // We will call this method in the constructor to see all the device we have and get the handle of the mouse that we want to listen to

        private (RawInputDeviceList[] deviceList, uint result) ListDevice()
        {
            uint dwSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
            uint deviceCount = 0;
            // we first get the number of devices
            GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, dwSize);

            RawInputDeviceList[] deviceList = new RawInputDeviceList[(int)deviceCount];

            // step 2: then we simply need to pass the device count to the 2nd call
            uint result = GetRawInputDeviceList(deviceList, ref deviceCount, dwSize);

            return (deviceList, result);

        }


        // REGISTER DEVICES METHOD
        // This function register our device with window
        // basically told window: "hey I want to listen to all the input of the device to be here
        private bool RegisterDevice()
        {
            // we first need to make sure we get the size of the structure first
            uint cbSize = (uint)Marshal.SizeOf<RawInputDevice>();
            uint uiNumDevices = 1;
            RawInputDevice[] rawInputDevices = new RawInputDevice[(int)uiNumDevices];





            // we need to populate the struct for ourself. Since the uiNumDevices = 1 (only 1 element)
            rawInputDevices[0].usUsagePage = 0x0001; // mouse class driver and mapped driver
            rawInputDevices[0].usUsage = 0x0006; // no idea???
            rawInputDevices[0].dwFlags = RIDEV_INPUTSINK; // get data even when the app is not focused
            rawInputDevices[0].hwndTarget = _hwnd;


            Console.WriteLine("Window handle: " + _hwnd);

            rawInputDevices[0].hwndTarget = _hwnd;

            bool isRegisterSuccessful = RegisterRawInputDevices(
                rawInputDevices,
                uiNumDevices,
                cbSize
            );


            return isRegisterSuccessful;

        }


        // 
        private bool FindTargetDevice(RawInputDeviceList[] deviceList)
        {
            // let see all the device
            foreach (RawInputDeviceList device in deviceList)
            {

                uint pcbSize = 0;

                Console.WriteLine("This is the device Info");
                Console.WriteLine(device.dwType);
                Console.WriteLine(device.hDevice);

                // Then we call the GetRawInputDeviceInfo to extract device info
                GetRawInputDeviceInfo(
                    device.hDevice, // handle to the raw input device
                    0x20000007, // getting device name
                    IntPtr.Zero,
                    ref pcbSize

                );


                // now the pcbSize is the size of the buffer we need. We need to create the buffer
                // and pointer to it

                IntPtr buffer = Marshal.AllocHGlobal((int)pcbSize); // this return a pointer to the buffer with size pcbSize
                GetRawInputDeviceInfo(
                    device.hDevice,
                    0x20000007,
                    buffer,
                    ref pcbSize
                );

                Console.WriteLine(Marshal.PtrToStringAnsi(buffer)); // conver the buffer to string to read it

                // this mean we set our target to the Mouse Dell
                // VID_413C is the vendor ID for Dell, so this is to make sure we only listen to the mouse from Dell
                string bufferString = Marshal.PtrToStringAnsi(buffer);
                if (bufferString.Contains("VID_1189") && bufferString.Contains("MI_00")) // this is innfo of our macro
                {
                    _targetDeviceHandle = device.hDevice;
                    // release that memory back to use we're done
                    Marshal.FreeHGlobal(buffer);

                    return true;

                }

                Marshal.FreeHGlobal(buffer);


            }
            return false;

        }



        // Method to inject the keyboard event from RawInput with SendInput
        private void InjectKeyboardEvent(ushort vKey, bool isKeyUp)
        {
            tagINPUT[] tagINPUTs = new tagINPUT[1];

            tagINPUTs[0].type = 1; // 1 for keyboard input
            tagINPUTs[0].tagInputUnion.ki = new tagKEYBDINPUT
            {
                wVk = vKey,
                wScan = 0,
                dwFlags = isKeyUp ? 0x0002u : 0x0000u, // KEYEVENTF_KEYUP for key up, 0 for key down
                time = 0,
                dwExtraInfo = UIntPtr.Zero
            };

            uint sent = SendInput(1, tagINPUTs, Marshal.SizeOf<tagINPUT>());

            if (sent != tagINPUTs.Length)
            {
                Console.WriteLine("SendInput failed or was blocked.");
            }

        }



        // this is the method that we will add to the hook.
        // This is the method that will be called every time we get a WM_INPUT message (which is when we get input from the mouse)
        // basically we match the delegate here: https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsourcehook?view=windowsdesktop-10.0
        private IntPtr HandleRawInput(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {

            if (msg == WM_INPUT)
            {
                Console.WriteLine("Got WM_INPUT number " + countingEvent);
                countingEvent += 1;

                // so now we have the input from the hook. It come from WM_INPUT
                // but we need a way to get actual mouse data
                // and we pass the lParam to GetRawInputData function to get the actual mouse data


                // Let write our GetRawInputData

                // Pass 1: we need to get the size of pcbSize
                uint uiCommand = 0x10000003;
                uint pcbSize = 0;
                uint cbSizeHeader = (uint)Marshal.SizeOf<RawInputHeader>();
                Console.WriteLine("Header size: " + Marshal.SizeOf<RawInputHeader>());

                GetRawInputData(lParam, uiCommand, IntPtr.Zero, ref pcbSize, cbSizeHeader);

                // Pass 2: now we actually have the pcbSize
                IntPtr pData = Marshal.AllocHGlobal((int)pcbSize);
                Console.WriteLine("pcbSize: " + pcbSize); // add this

                GetRawInputData(lParam, uiCommand, pData, ref pcbSize, cbSizeHeader);

                // Marshal.PtrToStructure basically meant: starting at the beginning (since pData is a pointer to the beginning of the buffer)
                // Eead the memory the size of RawInputHeader and convert it to the RawInputHeader struct
                RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(pData);

                // extract information for the RawInput header (this use to determine if it's the event we want)
                uint headerdwType = header.dwType; // 0 is mouse, 1 is keyboard, 2 is HID
                IntPtr headerhDevice = header.hDevice; // this is the handle to the device that generate this event. We can compare this with our _targetDeviceHandle to see if it's the event we want
                IntPtr headerwParam = header.wParam; // extr info 


                if (headerdwType == 1) // this mean that the event come from keyboard and it's F1-F9
                {

                    // ✅ ADD THIS - breaks the infinite loop
                    if (headerhDevice == IntPtr.Zero)
                    {
                        Console.WriteLine("Injected event detected, ignoring.");
                        Marshal.FreeHGlobal(pData);
                        return IntPtr.Zero;
                    }



                    // then now we need to move the pointer from the head to right after the header
                    // so now the pointer is at the beginning of the RawInputUnion (which is the actual data we want)
                    IntPtr payloadPtr = IntPtr.Add(pData, Marshal.SizeOf<RawInputHeader>());

                    // then we read the data from the payloadPtr (we know for sure that this is the keyboard data)
                    tagRawKeyBoard tagRawKeyBoard = Marshal.PtrToStructure<tagRawKeyBoard>(payloadPtr);

                    // extract the data from the tagRawKeyBoard

                    // each key has a code (full list https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes)
                    // the code is in hexadecimal. So 0x70 = hex is 112 in decimal, which is the code for F1
                    ushort keyboardVkey = tagRawKeyBoard.VKey;
                    ushort keyboardFlags = tagRawKeyBoard.Flags; // this is to determine if it's keydown or keyup. If the least significant bit is 1, it's keyup. If it's 0, it's keydown



                    // now we now that the rawInput has 2 fields: header and mouse
                    // dwType == 1 -> come from keyboard
                    // rawInput.keyboard.Flags == 0 -> only keydown event
                    bool isTargetKey = keyboardVkey >= 0x70 && keyboardVkey <= 0x78; // F1-F9
                    bool isMacroDevice = headerhDevice == _targetDeviceHandle;
                    bool isKeyUp = (keyboardFlags & 0x0001) != 0; // RI_KEY_BREAK

                    if (isTargetKey) // only do all these processing if it's the targetkey
                    {

                        if (isMacroDevice)
                        {
                            Console.WriteLine("GGoott  kkeeyy from target device!");

                            // Keep it swallowed.
                            // Run your macro logic here.
                            // DO NOT call SendInput here.
                        }
                        else
                        {
                            Console.WriteLine("Got kkeeyy from another keyboard. Reinjecting....");


                            InjectKeyboardEvent(keyboardVkey, isKeyUp);

                            // Set the flag to indicate we're injecting
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Got input from other device or other type!");
                }

                // release that memory back to use we're done
                Marshal.FreeHGlobal(pData);


            }

            return IntPtr.Zero;
        }


        // Clean up function to remove the hook when we're done
        public void Shutdown()
        {
            if (_macroHookHandle != IntPtr.Zero)
            {
                bool ok = UnhookWindowsHookEx(_macroHookHandle);

                if (!ok)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine("UnhookWindowsHookEx failed: " + error);
                }

                _macroHookHandle = IntPtr.Zero;
            }
        }

        // =====================================================================
        // UTILITIES
        // =====================================================================

        /// <summary>
        /// Reads the last Win32 error and logs it with a human-readable message.
        /// Call this immediately after a P/Invoke failure while SetLastError is still set.
        /// </summary>
        private static void LogWin32Error(string context)
        {
            int errorCode = Marshal.GetLastWin32Error();
            string message = new Win32Exception(errorCode).Message;
            Console.WriteLine($"[Win32 Error] {context} failed — code {errorCode}: {message}");
        }



    }
}

