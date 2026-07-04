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

using SharpQuake.Framework.Rendering.UI;
using System;

namespace SharpQuake.Rendering.UI.Elements.Text
{
    public class ModalMessage : BaseUIElement, ITextRenderer, IResetableRenderer
    {
        public override Boolean IsVisible
        {
            get;
            set;
        } = false;

        private String Message
        {
            get;
            set;
        }

        private readonly VideoState _videoState;
        private readonly Drawer _drawer;

        public ModalMessage( VideoState videoState, Drawer drawer )
        {
            _videoState = videoState;
            _drawer = drawer;
        }

        public void Enqueue( String text )
        {
            Message = text;
        }

        public void Reset( )
        {
            Message = null;
        }

        /// <summary>
        /// SCR_DrawNotifyString
        /// </summary>
        public override void Draw( )
        {
            base.Draw( );

            if ( !IsVisible || !HasInitialised )
                return;

            if ( string.IsNullOrEmpty( Message ) )
                return;

            var offset = 0;
            var y = ( Int32 ) ( _videoState.Data.height * 0.35 );

            do
            {
                var end = Message.IndexOf( '\n', offset );
                if ( end == -1 )
                    end = Message.Length;
                if ( end - offset > 40 )
                    end = offset + 40;

                var length = end - offset;
                if ( length > 0 )
                {
                    var x = ( _videoState.Data.width - length * 8 ) / 2;
                    for ( var j = 0; j < length; j++, x += 8 )
                        _drawer.DrawCharacter( x, y, Message[offset + j] );

                    y += 8;
                }
                offset = end + 1;
            } while ( offset < Message.Length );
        }
    }
}
