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
using SharpQuake.Networking.Client;
using SharpQuake.Renderer.Textures;
using SharpQuake.Sys;
using System;

namespace SharpQuake.Rendering.UI.Elements
{
    public class Pause : BaseUIElement
    {
        private BasePicture Picture
        {
            get;
            set;
        }

        private readonly Vid _video;
        private readonly VideoState _videoState;
        private readonly PictureFactory _pictures;
        private readonly ClientState _clientState;

        public Pause( Vid video, VideoState videoState, PictureFactory pictures, ClientState clientState )
        {
            _video = video;
            _videoState = videoState;
            _pictures = pictures;
            _clientState = clientState;
        }

        public override void Initialise()
        {
            Picture = _pictures.Cache( "gfx/pause.lmp", "GL_NEAREST" );
            HasInitialised = true;
        }

        /// <summary>
        /// DrawPause
        /// </summary>
        public override void Draw( )
        {
            base.Draw( );

            if ( !IsVisible || !HasInitialised )
                return;

            if ( !Cvars.ShowPause.Get<Boolean>( ) )	// turn off for screenshots
                return;

            if ( !_clientState.Data.paused )
                return;

            _video.Device.Graphics.DrawPicture( Picture, ( _videoState.Data.width - Picture.Width ) / 2, ( _videoState.Data.height - 48 - Picture.Height ) / 2 );
        }
    }
}
