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
using SharpQuake.Networking.Client;
using SharpQuake.Networking.Server;
using SharpQuake.Rendering.UI.Menus;
using SharpQuake.Services;
using SharpQuake.Sys;

namespace SharpQuake.Rendering.UI
{
    public class SaveMenu : BaseMenu
    {
        private readonly PictureFactory _pictures;
        private readonly snd _sound;
        private readonly ClientState _clientState;
        private readonly ServerState _serverState;
        private readonly SaveFileService _saves;

        public SaveMenu( IKeyboardInput keyboard, MenuFactory menus, 
            PictureFactory pictures, snd sound, ClientState clientState, 
            ServerState serverState, SaveFileService saves ) : base( "menu_save", keyboard, menus )
        {
            _pictures = pictures;
            _sound = sound;
            _clientState = clientState;
            _serverState = serverState;
            _saves = saves;
        }

        public override void Show( )
        {
            if ( !_serverState.Data.active )
                return;
            if ( _clientState.Data.intermission != 0 )
                return;
            if ( _serverState.StaticData.maxclients != 1 )
                return;

            _saves.Update( );

            base.Show( );
        }

        public override void KeyEvent( Int32 key )
        {
            switch ( key )
            {
                case KeysDef.K_ESCAPE:
                    _menus.Show( "menu_singleplayer" );
                    break;

                case KeysDef.K_ENTER:
                    _menus.CurrentMenu.Hide( );
                    _saves.Save( Cursor );
                    return;

                case KeysDef.K_UPARROW:
                case KeysDef.K_LEFTARROW:
                    _sound.LocalSound( "misc/menu1.wav" );
                    Cursor--;
                    if ( Cursor < 0 )
                        Cursor = SaveFileService.MAX_SAVEGAMES - 1;
                    break;

                case KeysDef.K_DOWNARROW:
                case KeysDef.K_RIGHTARROW:
                    _sound.LocalSound( "misc/menu1.wav" );
                    Cursor++;
                    if ( Cursor >= SaveFileService.MAX_SAVEGAMES )
                        Cursor = 0;
                    break;
            }
        }

        private void DrawPlaque( MenuAdorner adorner )
        {
            var scale = _menus.UIScale;

            var p = _pictures.Cache( "gfx/p_save.lmp", "GL_NEAREST" );
            _menus.DrawPic( adorner.MidPointX - ( ( p.Width * scale ) / 2 ), 4 * scale, p, scale: scale );
        }

        public override void Draw( )
        {
            var scale = _menus.UIScale;
            var adorner = _menus.BuildAdorner( SaveFileService.MAX_SAVEGAMES, 0, 0, width: 152 );

            DrawPlaque( adorner );

            var newUI = Cvars.NewUI.Get<Boolean>( );

            for ( var i = 0; i < SaveFileService.MAX_SAVEGAMES; i++ )
            {
                if ( i == Cursor )
                    adorner.PrintWhite( _saves.Files[i].Name, TextAlignment.Centre );
                else
                    adorner.Print( _saves.Files[i].Name, TextAlignment.Centre );
            }

            // line cursor
            if ( !newUI )
                _menus.DrawCharacter( adorner.LeftPoint + ( ( 8 * scale ) / 2 ), adorner.LineY( Cursor ), 12 + ( ( Int32 ) ( Time.Absolute * 4 ) & 1 ) );
        }
    }
}
