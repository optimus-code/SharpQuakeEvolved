/// <copyright>
///
/// SharpQuakeEvolved changes by optimus-code, 2019-2023
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

using SharpQuake.Framework.IO.Input;
using System;
using System.IO;
using System.Text;

namespace SharpQuake.Desktop
{
    /// <summary>
    /// Interface for Keyboard message pumping
    /// </summary>
    public interface IKeyboardInput
    {
        /// <summary>
        /// Used to prevent input whilst a demo is playing
        /// </summary>
        Boolean IsWatchingDemo
        {
            get;
            set;
        }

        KeyDestination Destination
        {
            get;
            set;
        }

        Int32 KeyCount
        {
            get;
            set;
        }

        Boolean TeamMessage
        {
            get;
            set;
        }

        Char[][] Lines
        {
            get;
            set;
        }

        Int32 EditLine
        {
            get;
            set;
        }

        StringBuilder ChatBuffer
        {
            get;
            set;
        }

        Int32 LastPress
        {
            get;
            set;
        }

        String[] Bindings
        {
            get;
            set;
        }

        Int32 LinePos
        {
            get;
            set;
        }

        void Initialise( );
        void Event( Int32 key, Boolean down );
        void WriteBindings( Stream dest );
        void ReadBindings( Stream dest );
        void ClearStates( );
        String KeynumToString( Int32 keynum );
        void SetBinding( Int32 keynum, String binding );

        void Listen( KeyDestination destination, Action<Int32> onKeyPressed );

        Boolean IsValidConsoleCharacter( Char character );
        Boolean IsKeyDown( Int32 key );
    }
}
