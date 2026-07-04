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

using System;

namespace SharpQuake.Sys
{
    /// <summary>
    /// Main container for host time
    /// </summary>
    public static class Time
    {
        public static Double _Time
        {
            get;
            private set;
        }

        /// <summary>
        /// The absolute time
        /// </summary>
        public static Double Absolute
        {
            get;
            private set;
        }

        /// <summary>
        /// The delta time between frames
        /// </summary>
        public static Double Delta
        {
            get;
            private set;
        }

        private static Double LastAbsolute
        {
            get;
            set;
        }

        /// <summary>
        /// Initialise time
        /// </summary>
        /// <remarks>
        /// So a think at time 0 won't get called
        /// </remarks>
        public static void Initialise()
        {
            _Time = 1.0;
        }

        /// <summary>
        /// Host_FilterTime
        /// Returns false if the time is too short to run a frame
        /// </summary>
        public static Boolean Filter( Double time, Boolean isTimeDemo )
        {
            Absolute += time;

            if ( !isTimeDemo && Absolute - LastAbsolute < 1.0 / 72.0 )
                return false;	// framerate is too high

            Delta = Absolute - LastAbsolute;
            LastAbsolute = Absolute;

            if ( Cvars.FrameRate.Get<Double>( ) > 0 )
            {
                Delta = Cvars.FrameRate.Get<Double>( );
            }
            else
            {	// don't allow really long or short frames
                if ( Delta > 0.1 )
                    Delta = 0.1;

                if ( Delta < 0.001 )
                    Delta = 0.001;
            }

            return true;
        }

        /// <summary>
        /// Used increment the timer per frame
        /// </summary>
        public static void Increment( )
        {
            _Time += Delta;
        }

        public static void SetToMaxDelta()
        {
            Delta = 0.1;
        }
    }
}
