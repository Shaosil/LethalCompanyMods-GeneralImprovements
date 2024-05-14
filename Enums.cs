using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
namespace GeneralImprovements
{
    public class Enums
    {
        public enum eAutoLaunchOption { NONE, ONLINE, LAN }

        public enum eShowHiddenMoons { Never, AfterDiscovery, Always }

        public enum eMaskedEntityCopyLook { None, Suit, SuitAndCosmetics }

        public enum eSaveFurniturePlacement { None, StartingFurniture, All }

        public enum eMonitorNames
        {
            None,

            // Text assignments
            ProfitQuota,
            Deadline,
            ShipScrap,
            ScrapLeft,
            Time,
            Weather,
            FancyWeather,
            Sales,
            Credits,
            DoorPower,
            TotalDays,
            TotalQuotas,
            TotalDeaths,
            DaysSinceDeath,

            // Material assignments
            InternalCam,
            ExternalCam
        }

        // Shortcut key helpers
        public enum eValidKeys
        {
            None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals,
            A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
            Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
            LeftShift, RightShift, LeftAlt, AltGr, LeftCtrl, RightCtrl, LeftWindows, RightCommand,
            ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace,
            PageDown, PageUp, Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause,
            NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals, Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
            F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
            MouseLeft, MouseRight, MouseMiddle, MouseBackButton, MouseForwardButton
        };
        public static ButtonControl GetMouseButtonMapping(eValidKeys mouseButton)
        {
            return mouseButton switch
            {
                eValidKeys.MouseLeft => Mouse.current.leftButton,
                eValidKeys.MouseRight => Mouse.current.rightButton,
                eValidKeys.MouseMiddle => Mouse.current.middleButton,
                eValidKeys.MouseBackButton => Mouse.current.backButton,
                eValidKeys.MouseForwardButton => Mouse.current.forwardButton,
                _ => throw new NotImplementedException()
            };
        }

        public enum eItemsToKeep { None, Held, NonScrap, All };
    }
}