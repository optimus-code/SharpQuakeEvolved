/// <copyright>
///
/// SharpQuakeEvolved changes by optimus-code, 2019
/// 
/// Based on SharpQuake (Quake Rewritten in C# by Yury Kiselev, 2010.)
///
/// Copyright (C) 1996-1997 Id Software, Inc.
///
/// This program is free software; you can redistribute it and/or
/// modify it under the terms of the GNU General Public License
/// as published by the Free Software Foundation; either version 2
/// of the License, or (at your option) any later version.
///
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
///
/// See the GNU General Public License for more details.
///
/// You should have received a copy of the GNU General Public License
/// along with this program; if not, write to the Free Software
/// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
/// </copyright>
/// Borrowed from OpenTK


namespace SharpQuake.Framework.IO.Input
{
    public enum Key
    {
        Unknown = -1,

        Space = 32,
        Quote = 39,
        Apostrophe = Quote,

        Comma = 44,
        Minus = 45,
        Period = 46,
        Slash = 47,

        Number0 = 48,
        Number1 = 49,
        Number2 = 50,
        Number3 = 51,
        Number4 = 52,
        Number5 = 53,
        Number6 = 54,
        Number7 = 55,
        Number8 = 56,
        Number9 = 57,

        Semicolon = 59,

        // OpenTK 4 calls this Equal. Your old enum called it Plus.
        Plus = 61,
        Equal = Plus,

        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,

        BracketLeft = 91,
        LBracket = BracketLeft,

        BackSlash = 92,
        Backslash = BackSlash,

        BracketRight = 93,
        RBracket = BracketRight,

        Tilde = 96,
        Grave = Tilde,
        GraveAccent = Tilde,

        Escape = 256,
        Enter = 257,
        Tab = 258,

        BackSpace = 259,
        Back = BackSpace,
        Backspace = BackSpace,

        Insert = 260,
        Delete = 261,

        Right = 262,
        Left = 263,
        Down = 264,
        Up = 265,

        PageUp = 266,
        PageDown = 267,
        Home = 268,
        End = 269,

        CapsLock = 280,
        ScrollLock = 281,
        NumLock = 282,
        PrintScreen = 283,
        Pause = 284,

        F1 = 290,
        F2 = 291,
        F3 = 292,
        F4 = 293,
        F5 = 294,
        F6 = 295,
        F7 = 296,
        F8 = 297,
        F9 = 298,
        F10 = 299,
        F11 = 300,
        F12 = 301,
        F13 = 302,
        F14 = 303,
        F15 = 304,
        F16 = 305,
        F17 = 306,
        F18 = 307,
        F19 = 308,
        F20 = 309,
        F21 = 310,
        F22 = 311,
        F23 = 312,
        F24 = 313,
        F25 = 314,

        // Not present in OpenTK 4.9.4 / GLFW.
        F26 = Unknown,
        F27 = Unknown,
        F28 = Unknown,
        F29 = Unknown,
        F30 = Unknown,
        F31 = Unknown,
        F32 = Unknown,
        F33 = Unknown,
        F34 = Unknown,
        F35 = Unknown,

        Keypad0 = 320,
        KeyPad0 = Keypad0,

        Keypad1 = 321,
        KeyPad1 = Keypad1,

        Keypad2 = 322,
        KeyPad2 = Keypad2,

        Keypad3 = 323,
        KeyPad3 = Keypad3,

        Keypad4 = 324,
        KeyPad4 = Keypad4,

        Keypad5 = 325,
        KeyPad5 = Keypad5,

        Keypad6 = 326,
        KeyPad6 = Keypad6,

        Keypad7 = 327,
        KeyPad7 = Keypad7,

        Keypad8 = 328,
        KeyPad8 = Keypad8,

        Keypad9 = 329,
        KeyPad9 = Keypad9,

        KeypadDecimal = 330,
        KeyPadDecimal = KeypadDecimal,
        KeypadPeriod = KeypadDecimal,
        KeyPadPeriod = KeypadDecimal,

        KeypadDivide = 331,
        KeyPadDivide = KeypadDivide,

        KeypadMultiply = 332,
        KeyPadMultiply = KeypadMultiply,

        KeypadSubtract = 333,
        KeyPadSubtract = KeypadSubtract,
        KeypadMinus = KeypadSubtract,
        KeyPadMinus = KeypadSubtract,

        KeypadAdd = 334,
        KeyPadAdd = KeypadAdd,
        KeypadPlus = KeypadAdd,
        KeyPadPlus = KeypadAdd,

        KeypadEnter = 335,
        KeyPadEnter = KeypadEnter,

        KeypadEqual = 336,
        KeyPadEqual = KeypadEqual,

        ShiftLeft = 340,
        LShift = ShiftLeft,
        LeftShift = ShiftLeft,

        ControlLeft = 341,
        LControl = ControlLeft,
        LeftControl = ControlLeft,

        AltLeft = 342,
        LAlt = AltLeft,
        LeftAlt = AltLeft,

        WinLeft = 343,
        LWin = WinLeft,
        LeftSuper = WinLeft,

        ShiftRight = 344,
        RShift = ShiftRight,
        RightShift = ShiftRight,

        ControlRight = 345,
        RControl = ControlRight,
        RightControl = ControlRight,

        AltRight = 346,
        RAlt = AltRight,
        RightAlt = AltRight,

        WinRight = 347,
        RWin = WinRight,
        RightSuper = WinRight,

        Menu = 348,

        // Not present in OpenTK 4.9.4.
        Clear = Unknown,
        Sleep = Unknown,
        NonUSBackSlash = Unknown,

        LastKey = Menu
    }
}