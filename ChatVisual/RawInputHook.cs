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

            if (!InstallLowLevelKeyboardHook())
            {
                Console.WriteLine("[RawInputHook] Failed to install low-level hook. Aborting.");
                return;
            }


        }




        // =====================================================================
        // INITIALIZATION
        // =====================================================================

        /// <summary>
        /// Installs a global low-level keyboard hook (WH_KEYBOARD_LL).
        /// This hook fires for every keystroke in the system, in every application.
        /// Our callback (LowLevelKeyboardFilter) uses it to swallow F1–F9.
        /// </summary>
        private bool InstallLowLevelKeyboardHook()
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

            if (_macroHookHandle == IntPtr.Zero)
            {
                LogWin32Error("SetWindowsHookEx");
                return false;
            }

            Console.WriteLine("[RawInputHook] Low-level keyboard hook installed.");
            return true;
        }


        // =====================================================================
        // EVENT HANDLERS
        // =====================================================================


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

                // handle F1 - F9 separatedly

                if (kbdStruct.vkCode >= 112 && kbdStruct.vkCode <= 120) // F1-F9 only
                {
                    bool isKeyUp = (kbdStruct.flags & 0x80) != 0; // LLKHF_UP
                    HandleMacroFunctionKey(kbdStruct.vkCode, isKeyUp);
                    return (IntPtr)1; // swallow

                }

                return CallNextHookEx(_macroHookHandle, nCode, wParam, lParam); // everything else passes

            }
        }


        // Functions to handle the Macro Function Key
        private void HandleMacroFunctionKey(uint vkCode, bool isKeyUp)
        {
            // placeholder for now
            Console.WriteLine($"Macro key handled: {vkCode}, keyUp: {isKeyUp}");
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

