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
/// 

using SharpQuake.Framework;
using SharpQuake.Framework.Logging;
using System;

namespace SharpQuake.Rendering
{
    public class VideoState
    {
        public Boolean IsScreenDisabledForLoading
        {
            get;
            set;
        }

        // scr_skipupdate
        public Boolean FullSbarDraw
        {
            get;
            set;
        }

        // fullsbardraw = false
        public Boolean IsPermedia
        {
            get;
            set;
        }

        public Boolean ScreenCopyEverything
        {
            get;
            set;
        }

        // only the refresh window will be updated unless these variables are flagged
        public Boolean ScreenCopyTop
        {
            get;
            set;
        }

        public VidDef Data
        {
            get;
            set;
        } = new VidDef( );

        private IConsoleLogger _logger;

        public VideoState( IConsoleLogger logger )
        {
            _logger = logger;
        }

        /// <summary>
        /// Con_SafePrintf
        /// </summary>
        /// <remarks>
        /// Okay to call even when the screen can't be updated
        /// </remarks>
        /// <param name="fmt"></param>
        /// <param name="args"></param>
        public void SafePrint( String fmt, params Object[] args )
        {
            var temp = IsScreenDisabledForLoading;
            IsScreenDisabledForLoading = true;
            _logger.Print( fmt, args );
            IsScreenDisabledForLoading = temp;
        }
    }
}
