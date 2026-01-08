// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using osu.Framework.Logging;

namespace osu.Desktop.EzMacOS
{
    [SupportedOSPlatform("macos")]
    public static class SpotlightKey
    {
        private static IntPtr eventTap;
        private static IntPtr runLoopSource;
        private static IntPtr runLoopMode;
        private static bool isDisabled;
        private static EventTapCallback? callbackDelegate;

        private const int kVK_Space = 49;
        private const ulong kCGEventFlagMaskCommand = 1UL << 20;

        public static void Disable()
        {
            if (isDisabled)
                return;

            try
            {
                Logger.Log("Attempting to disable Cmd+Space (Spotlight) via Accessibility tap...", LoggingTarget.Runtime, LogLevel.Debug);

                ulong mask = CGEventMaskBit(CGEventType.KeyDown);
                callbackDelegate = OnEventTap;

                eventTap = CGEventTapCreate(
                    kCGHIDEventTap,
                    kCGHeadInsertEventTap,
                    kCGEventTapOptionDefault,
                    mask,
                    callbackDelegate,
                    IntPtr.Zero);

                if (eventTap == IntPtr.Zero)
                {
                    Logger.Log("Failed to create event tap. Ensure Accessibility permission is granted (System Settings → Privacy & Security → Accessibility).", LoggingTarget.Runtime, LogLevel.Error);
                    return;
                }

                runLoopSource = CFMachPortCreateRunLoopSource(IntPtr.Zero, eventTap, 0);
                IntPtr runLoop = CFRunLoopGetMain();
                runLoopMode = CFStringCreateWithCString(IntPtr.Zero, "kCFRunLoopDefaultMode", kCFStringEncodingUTF8);
                CFRunLoopAddSource(runLoop, runLoopSource, runLoopMode);

                CGEventTapEnable(eventTap, true);
                isDisabled = true;
                Logger.Log("Cmd+Space (Spotlight) blocking enabled during gameplay.", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to disable Cmd+Space: {ex.Message}\n{ex.StackTrace}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        public static void Enable()
        {
            if (!isDisabled)
                return;

            try
            {
                Logger.Log("Re-enabling Cmd+Space (Spotlight)...", LoggingTarget.Runtime, LogLevel.Debug);

                if (eventTap != IntPtr.Zero)
                {
                    CGEventTapEnable(eventTap, false);
                    CFMachPortInvalidate(eventTap);
                }

                if (runLoopSource != IntPtr.Zero)
                {
                    IntPtr runLoop = CFRunLoopGetMain();
                    CFRunLoopRemoveSource(runLoop, runLoopSource, runLoopMode);
                    CFRelease(runLoopSource);
                }

                if (runLoopMode != IntPtr.Zero)
                    CFRelease(runLoopMode);

                eventTap = IntPtr.Zero;
                runLoopSource = IntPtr.Zero;
                runLoopMode = IntPtr.Zero;
                callbackDelegate = null;

                isDisabled = false;
                Logger.Log("Cmd+Space (Spotlight) blocking disabled.", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to enable Cmd+Space: {ex.Message}\n{ex.StackTrace}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr EventTapCallback(IntPtr proxy, CGEventType type, IntPtr @event, IntPtr userInfo);

        private static IntPtr OnEventTap(IntPtr proxy, CGEventType type, IntPtr @event, IntPtr userInfo)
        {
            try
            {
                if (type == CGEventType.KeyDown)
                {
                    ulong flags = CGEventGetFlags(@event);
                    long keyCode = CGEventGetIntegerValueField(@event, kCGKeyboardEventKeycode);

                    if ((flags & kCGEventFlagMaskCommand) != 0 && keyCode == kVK_Space)
                    {
                        Logger.Log("Blocked Cmd+Space via Accessibility tap", LoggingTarget.Runtime, LogLevel.Debug);
                        return IntPtr.Zero; // swallow event
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in event tap: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }

            return @event;
        }

        private static ulong CGEventMaskBit(CGEventType type) => 1UL << (int)type;

        private const int kCGHIDEventTap = 0;               // HID system-wide events
        private const int kCGHeadInsertEventTap = 0;         // Insert at head
        private const int kCGEventTapOptionDefault = 0;      // Listen-only/active

        private const int kCGKeyboardEventKeycode = 9;       // Field for keycode

        private const uint kCFStringEncodingUTF8 = 0x08000100;

        #region Native Methods

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGEventTapCreate(int tap, int place, int options, ulong eventsOfInterest, EventTapCallback callback, IntPtr userInfo);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern void CGEventTapEnable(IntPtr tap, bool enable);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern ulong CGEventGetFlags(IntPtr eventRef);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern long CGEventGetIntegerValueField(IntPtr eventRef, int field);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern void CFMachPortInvalidate(IntPtr port);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, Int32 order);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFRunLoopGetMain();

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRunLoopRemoveSource(IntPtr rl, IntPtr source, IntPtr mode);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr cf);

        #endregion

        private enum CGEventType
        {
            KeyDown = 10,
            KeyUp = 11
        }
    }
}

