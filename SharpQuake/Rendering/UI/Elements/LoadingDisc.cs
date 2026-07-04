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

using SharpQuake.Framework.Factories.IO.WAD;
using SharpQuake.Renderer.Textures;

namespace SharpQuake.Rendering.UI.Elements
{
    public class LoadingDisc : BaseUIElement
    {
        public BasePicture Disc
        {
            get;
            private set;
        }

        private readonly Vid _video;
        private readonly VideoState _videoState;
        private readonly WadFactory _wads;

        public LoadingDisc( Vid video, VideoState videoState, WadFactory wads )
        {
            _video = video;
            _videoState = videoState;
            _wads = wads;
        }

        public override void Initialise( )
        {
            base.Initialise( );

            Disc = BasePicture.FromWad( _video.Device, _wads.FromTexture( "disc" ), "disc", "GL_NEAREST" );

            HasInitialised = true;
        }

        /// <summary>
        /// Draw_BeginDisc
        /// </summary>
        /// <remarks>
        /// (Draws the little blue disc in the corner of the screen.
        /// Call before beginning any disc IO.)
        /// </remarks>
        public override void Draw( )
        {
            base.Draw( );

            if ( !IsVisible || !HasInitialised )
                return;

            if ( Disc != null )
            {
                _video.Device.SetDrawBuffer( true );
                _video.Device.Graphics.DrawPicture( Disc, _videoState.Data.width - 24, 0 );
                _video.Device.SetDrawBuffer( false );
            }
        }
    }
}
