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
using SharpQuake.Desktop;
using SharpQuake.Factories.Rendering.UI;
using SharpQuake.Framework;
using SharpQuake.Framework.Definitions;

namespace SharpQuake.Rendering.UI
{
    public class VideoMenu : BaseMenu
    {
        private struct modedesc_t
        {
            public Int32 modenum;
            public String desc;
            public Boolean iscur;
        } //modedesc_t;

        private const Int32 MAX_COLUMN_SIZE = 9;
        private const Int32 MODE_AREA_HEIGHT = MAX_COLUMN_SIZE + 2;
        private const Int32 MAX_MODEDESCS = MAX_COLUMN_SIZE * 3;

        private Int32 _WModes; // vid_wmodes
        private modedesc_t[] _ModeDescs = new modedesc_t[MAX_MODEDESCS]; // modedescs

        private readonly Vid _video;
        private readonly snd _sound;
        private readonly PictureFactory _pictures;

        public VideoMenu( IKeyboardInput keyboard, MenuFactory menus, Vid video,
            snd sound, PictureFactory pictures ) : base( "menu_video", keyboard, menus )
        {
            _video = video;
            _sound = sound;
            _pictures = pictures;
        }

        public override void KeyEvent( Int32 key )
        {
            switch ( key )
            {
                case KeysDef.K_ESCAPE:
                    _sound.LocalSound( "misc/menu1.wav" );
                    _menus.Show( "menu_options" );
                    break;

                default:
                    break;
            }
        }

        public override void Draw( )
        {
            var p = _pictures.Cache( "gfx/vidmodes.lmp", "GL_NEAREST" );
            _menus.DrawPic( ( 320 - p.Width ) / 2, 4, p );

            _WModes = 0;
            var lnummodes = _video.Device.AvailableModes.Length;

            for ( var i = 1; ( i < lnummodes ) && ( _WModes < MAX_MODEDESCS ); i++ )
            {
                var m = _video.Device.AvailableModes[i];

                var k = _WModes;

                _ModeDescs[k].modenum = i;
                _ModeDescs[k].desc = String.Format( "{0}x{1}x{2}", m.Width, m.Height, m.BitsPerPixel );
                _ModeDescs[k].iscur = false;

                if ( i == _video.ModeNum )
                    _ModeDescs[k].iscur = true;

                _WModes++;
            }

            if ( _WModes > 0 )
            {
                _menus.Print( 2 * 8, 36 + 0 * 8, "Fullscreen Modes (WIDTHxHEIGHTxBPP)" );

                var column = 8;
                var row = 36 + 2 * 8;

                for ( var i = 0; i < _WModes; i++ )
                {
                    if ( _ModeDescs[i].iscur )
                        _menus.PrintWhite( column, row, _ModeDescs[i].desc );
                    else
                        _menus.Print( column, row, _ModeDescs[i].desc );

                    column += 13 * 8;

                    if ( ( i % VideoDef.VID_ROW_SIZE ) == ( VideoDef.VID_ROW_SIZE - 1 ) )
                    {
                        column = 8;
                        row += 8;
                    }
                }
            }

            _menus.Print( 3 * 8, 36 + MODE_AREA_HEIGHT * 8 + 8 * 2, "Video modes must be set from the" );
            _menus.Print( 3 * 8, 36 + MODE_AREA_HEIGHT * 8 + 8 * 3, "command line with -width <width>" );
            _menus.Print( 3 * 8, 36 + MODE_AREA_HEIGHT * 8 + 8 * 4, "and -bpp <bits-per-pixel>" );
            _menus.Print( 3 * 8, 36 + MODE_AREA_HEIGHT * 8 + 8 * 6, "Select windowed mode with -window" );
        }
    }
}
