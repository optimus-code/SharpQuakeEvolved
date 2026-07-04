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
using System.Drawing;
using SharpQuake.Desktop;
using SharpQuake.Framework;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Framework.Logging;
using SharpQuake.Renderer;
using SharpQuake.Renderer.OpenGL.Desktop;
using SharpQuake.Sys;

namespace SharpQuake
{
	public class MainWindow : GLWindow//GameWindow
    { 
        /// <summary>
        /// Called every render frame
        /// </summary>
        public Action<Double> OnFrame
        {
            get;
            set;
        }

        /// <summary>
        /// Executed when window focus is changed
        /// </summary>
        public Action<Boolean> OnFocusChanged
        {
            get;
            set;
        }

        private Int32 MouseBtnState
        {
            get;
            set;
        }

        private readonly IEngine _engine;
        private IConsoleLogger _logger;
        private IKeyboardInput _keyboard;
        private IMouseInput _mouse;

        public MainWindow( IEngine engine, Size size, Boolean isFullScreen )
        : base( "SharpQuakeEvolved", size, isFullScreen )
        {
            _engine = engine;

            VSync = VSyncMode.One;
            //Icon = Icon.ExtractAssociatedIcon( AppDomain.CurrentDomain.FriendlyName ); //Application.ExecutablePath

            KeyDown += Keyboard_KeyDown;
            KeyUp += Keyboard_KeyUp;

            MouseMove += Mouse_Move;
            MouseDown += Mouse_ButtonEvent;
            MouseUp += Mouse_ButtonEvent;
            MouseWheel += Mouse_WheelChanged;
        }

        public void Configure( IConsoleLogger logger, IKeyboardInput keyboard, IMouseInput mouse )
        {
            _logger = logger;
            _keyboard = keyboard;
            _mouse = mouse;
        }

        protected override void OnFocusedChanged( )
        {
            base.OnFocusedChanged( );

            OnFocusChanged?.Invoke( Focused ); // Indicate to any subscribers window focus has changed
        }

        protected override void OnUpdateFrame( Double time )
        {
            try
            {
                OnFrame?.Invoke( time ); // We call an action so MainWindow doesn't deal with render loop logic
            }
            catch ( EndGameException ) // TODO - Surely better way to do handle this??
            {
                // nothing to do
            }
        }

        private void Mouse_WheelChanged( Object sender, MouseWheelEventArgs e )
        {
            if ( e.Delta > 0 )
            {
                _keyboard.Event( KeysDef.K_MWHEELUP, true );
                _keyboard.Event( KeysDef.K_MWHEELUP, false );
            }
            else
            {
                _keyboard.Event( KeysDef.K_MWHEELDOWN, true );
                _keyboard.Event( KeysDef.K_MWHEELDOWN, false );
            }
        }

        private void Mouse_ButtonEvent( Object sender, MouseButtonEventArgs e )
        {
            MouseBtnState = 0;

            if ( e.Button == MouseButton.Left && e.IsPressed )
                MouseBtnState |= 1;

            if ( e.Button == MouseButton.Right && e.IsPressed )
                MouseBtnState |= 2;

            if ( e.Button == MouseButton.Middle && e.IsPressed )
                MouseBtnState |= 4;

            _mouse.MouseEvent( MouseBtnState );
        }

        private void Mouse_Move( Object sender, EventArgs e )
        {
            _mouse.MouseEvent( MouseBtnState );
        }

        private Int32 MapKey( Key srcKey )
        {
            var key = KeysDef.Translate( srcKey );

            if ( key == 0 )
                _logger.DPrint( "key 0x{0:X} has no translation\n", key );

            return key;
        }

        private void Keyboard_KeyUp( Object sender, KeyboardKeyEventArgs e )
        {
            _keyboard.Event( MapKey( e.Key ), false );
        }

        private void Keyboard_KeyDown( Object sender, KeyboardKeyEventArgs e )
        {
            _keyboard.Event( MapKey( e.Key ), true );
        }

        public override void Dispose( )
        {
            if ( IsDisposing )
                return;

            IsDisposing = true;

            _engine.Dispose( );

            base.Dispose( );
		}
	}
}
