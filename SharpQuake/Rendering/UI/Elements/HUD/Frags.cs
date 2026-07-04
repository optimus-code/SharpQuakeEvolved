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

using SharpQuake.Networking.Client;
using System;

namespace SharpQuake.Rendering.UI.Elements.HUD
{
    public class Frags : BaseUIElement
    {
        public override Boolean ManualInitialisation
        {
            get
            {
                return true;
            }
        }

        private HudResources _resources;

        private readonly Scr _screen;
        private readonly Vid _video;
        private readonly ClientState _clientState;
        private readonly VideoState _videoState;

        public Frags( Scr screen, Vid video, ClientState clientState, VideoState videoState )
        {
            _screen = screen;
            _video = video;
            _clientState = clientState;
            _videoState = videoState;
        }

        public override void Initialise( )
        {
            base.Initialise( );

            _resources = _screen.HudResources;

            HasInitialised = true;
        }

        /// <summary>
        /// Sbar_DrawFrags
        /// </summary>
        public override void Draw( )
        {
            base.Draw( );

            if ( !IsVisible || !HasInitialised )
                return;

            // draw the text
            var l = _resources._ScoreBoardLines <= 4 ? _resources._ScoreBoardLines : 4;
            Int32 xofs, x = 23;
            var cl = _clientState.Data;

            if ( cl.gametype == ProtocolDef.GAME_DEATHMATCH )
                xofs = 0;
            else
                xofs = ( _videoState.Data.width - 320 ) >> 1;

            var y = _videoState.Data.height - HudResources.SBAR_HEIGHT - 23;

            for ( var i = 0; i < l; i++ )
            {
                var k = _resources._FragSort[i];
                var s = cl.scores[k];
                if ( String.IsNullOrEmpty( s.name ) )
                    continue;

                // draw background
                var top = s.colors & 0xf0;
                var bottom = ( s.colors & 15 ) << 4;
                top = _resources.ColorForMap( top );
                bottom = _resources.ColorForMap( bottom );

                _video.Device.Graphics.FillUsingPalette( xofs + x * 8 + 10, y, 28, 4, top );
                _video.Device.Graphics.FillUsingPalette( xofs + x * 8 + 10, y + 4, 28, 3, bottom );

                // draw number
                var f = s.frags;
                var num = f.ToString( ).PadLeft( 3 );
                //sprintf(num, "%3i", f);

                _resources.DrawCharacter( ( x + 1 ) * 8, -24, num[0] );
                _resources.DrawCharacter( ( x + 2 ) * 8, -24, num[1] );
                _resources.DrawCharacter( ( x + 3 ) * 8, -24, num[2] );

                if ( k == cl.viewentity - 1 )
                {
                    _resources.DrawCharacter( x * 8 + 2, -24, 16 );
                    _resources.DrawCharacter( ( x + 4 ) * 8 - 4, -24, 17 );
                }
                x += 4;
            }
        }
    }
}
