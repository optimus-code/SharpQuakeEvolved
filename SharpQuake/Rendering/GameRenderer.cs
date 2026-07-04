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

using SharpQuake.Framework;
using SharpQuake.Framework.IO;
using SharpQuake.Game.Client;
using SharpQuake.Game.Rendering.Textures;
using SharpQuake.Networking.Client;
using System;

namespace SharpQuake.Rendering
{
    /// <summary>
    /// Main interface to any visual/rendering component within the game
    /// </summary>
    public class GameRenderer : IGameRenderer, IDisposable
    {
        public Byte[] ColorMap
        {
            get;
            set;
        }

        public Byte[] BasePal
        {
            get;
            set;
        }

        public ModelTexture NoTextureMip
        {
            get;
            private set;
        }

        public WarpableTextures WarpableTextures
        {
            get;
            set;
        }

        private readonly ClientState _clientState;

        public GameRenderer( ClientState clientState )
        {
            _clientState = clientState;
        }

        public void Initialise( )
        {
            // Initialise palettes here instead
            if ( _clientState.StaticData.state != cactive_t.ca_dedicated )
            {
                BasePal = FileSystem.LoadFile( "gfx/palette.lmp" );

                if ( BasePal == null )
                    Utilities.Error( "Couldn't load gfx/palette.lmp" );

                ColorMap = FileSystem.LoadFile( "gfx/colormap.lmp" );

                if ( ColorMap == null )
                    Utilities.Error( "Couldn't load gfx/colormap.lmp" );
            }

            InitTextures( );
        }

        public void Dispose( )
        {
        }


        // R_InitTextures
        private void InitTextures( )
        {
            // create a simple checkerboard texture for the default
            NoTextureMip = new ModelTexture( );
            NoTextureMip.name = "NONE";
            NoTextureMip.pixels = new Byte[16 * 16 + 8 * 8 + 4 * 4 + 2 * 2];
            NoTextureMip.width = NoTextureMip.height = 16;
            var offset = 0;
            NoTextureMip.offsets[0] = offset;
            offset += 16 * 16;
            NoTextureMip.offsets[1] = offset;
            offset += 8 * 8;
            NoTextureMip.offsets[2] = offset;
            offset += 4 * 4;
            NoTextureMip.offsets[3] = offset;

            var dest = NoTextureMip.pixels;
            for ( var m = 0; m < 4; m++ )
            {
                offset = NoTextureMip.offsets[m];
                for ( var y = 0; y < ( 16 >> m ); y++ )
                    for ( var x = 0; x < ( 16 >> m ); x++ )
                    {
                        if ( ( y < ( 8 >> m ) ) ^ ( x < ( 8 >> m ) ) )
                            dest[offset] = 0;
                        else
                            dest[offset] = 0xff;

                        offset++;
                    }
            }
        }

    }
}
