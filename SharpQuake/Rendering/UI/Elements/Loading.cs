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

using SharpQuake.Factories.Rendering.UI;
using System;

namespace SharpQuake.Rendering.UI.Elements
{
    public class Loading : BaseUIElement
    {
        public override Boolean IsVisible
        {
            get;
            set;
        } = false;

        private readonly Vid _video;
        private readonly VideoState _videoState;
        private readonly PictureFactory _pictures;

        public Loading( Vid video, VideoState videoState, PictureFactory pictures )
        {
            _video = video;
            _videoState = videoState;
            _pictures = pictures;
        }

        /// <summary>
        /// SCR_DrawLoading
        /// </summary>
        public override void Draw( )
        {
            base.Draw( );

            if ( !IsVisible || !HasInitialised )
                return;

            var pic = _pictures.Cache( "gfx/loading.lmp", "GL_LINEAR" );
            _video.Device.Graphics.DrawPicture( pic, ( _videoState.Data.width - pic.Width ) / 2, ( _videoState.Data.height - 48 - pic.Height ) / 2 );
        }
    }
}
