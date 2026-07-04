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
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.Mathematics;
using SharpQuake.Sys;

// input.h -- external (non-keyboard) input devices

namespace SharpQuake.Desktop
{
    /// <summary>
    /// In_functions
    /// </summary>
    public class MouseInput : IMouseInput, IDisposable
    {
        /// <summary>
        /// Callback to get the mouse active status from a window
        /// </summary>
        public Func<Boolean> OnCheckMouseActive
        {
            get;
            set;
        }

        public Point WindowCenter
        {
            get
            {
                var bounds = _window.Bounds;
                var p = bounds.Location;
                p.Offset( bounds.Width / 2, bounds.Height / 2 );
                return p;
            }
        }

        private Vector2 OldMouse // old_mouse_x, old_mouse_y
        {
            get;
            set;
        }

        public Vector2 Mouse // mouse_x, mouse_y
        {
            get;
            private set;
        }

        private Vector2 MouseAccum // mx_accum, my_accum
        {
            get;
            set;
        }

        public Boolean IsActive // mouseactive
        {
            get;
            private set;
        }

        private Int32 MouseButtons // mouse_buttons
        {
            get;
            set;
        }

        private Int32 MouseOldButtonState // mouse_oldbuttonstate
        {
            get;
            set;
        }

        private Boolean MouseActivateToggle // mouseactivatetoggle
        {
            get;
            set;
        }

        private Boolean MouseShowToggle // mouseshowtoggle
        {
            get;
            set;
        } = true;

        /// <summary>
        /// Event used to subscribe to mouse movements
        /// </summary>
        public Action<usercmd_t> OnMouseMove
        {
            get;
            set;
        }


        /// <summary>
        /// Stores mouse sensitivity
        /// </summary>
        public Single Sensitivity
        {
            get
            {
                return Cvars.Sensitivity.Get<Single>( );
            }
        }

        private readonly IKeyboardInput _keyboard;
        private readonly ClientVariableFactory _cvars;
        private readonly MainWindow _window;

        public MouseInput( ClientVariableFactory cvars, IKeyboardInput keyboard, MainWindow window )
        {
            _cvars = cvars;
            _keyboard = keyboard;
            _window = window;
        }

        /// <summary>
        /// IN_Init
        /// </summary>
        /// <param name="isMouseActive">Defaults to false</param>
        public void Initialise( )
        {
            if ( Cvars.MouseFilter == null )
                Cvars.MouseFilter = _cvars.Add( "m_filter", false );

            IsActive = OnCheckMouseActive?.Invoke( ) == true;// Host.MainWindow.IsMouseActive;

            if ( IsActive )
                MouseButtons = 3; //??? TODO: properly upgrade this to 3.0.1
        }

        /// <summary>
        /// IN_Shutdown
        /// </summary>
        public void Dispose( )
        {
            DeactivateMouse( );
            ShowMouse( );
        }

        // IN_Commands
        // oportunity for devices to stick commands on the script buffer
        public void Commands( )
        {
            // joystick not supported
        }

        /// <summary>
        /// IN_ActivateMouse
        /// </summary>
        public void ActivateMouse( )
        {
            MouseActivateToggle = true;

            if ( OnCheckMouseActive?.Invoke() == true )
            {
                //if (mouseparmsvalid)
                //    restore_spi = SystemParametersInfo (SPI_SETMOUSE, 0, newmouseparms, 0);

                //Cursor.Position = Input.WindowCenter;
                _window.SetMousePosition( WindowCenter.X, WindowCenter.Y );


                //SetCapture(mainwindow);

                //Cursor.Clip = MainWindow.Instance.Bounds;

                IsActive = true;
            }
        }

        /// <summary>
        /// IN_DeactivateMouse
        /// </summary>
        public void DeactivateMouse( )
        {
            MouseActivateToggle = false;

            //Cursor.Clip = Screen.PrimaryScreen.Bounds;

            IsActive = false;
        }

        /// <summary>
        /// IN_HideMouse
        /// </summary>
        public void HideMouse( )
        {
            if ( MouseShowToggle )
            {
                //Cursor.Hide();
                MouseShowToggle = false;
            }
        }

        /// <summary>
        /// IN_ShowMouse
        /// </summary>
        public void ShowMouse( )
        {
            if ( !MouseShowToggle )
            {
                if ( !_window.IsFullScreen )
                {
                    //Cursor.Show();
                }
                MouseShowToggle = true;
            }
        }

        // IN_Move
        // add additional movement on top of the keyboard move cmd
        public void Move( usercmd_t cmd )
        {
            if ( !_window.Focused )
                return;

            if ( _window.IsMinimised )
                return;

            MouseMove( cmd );
        }

        // IN_ClearStates
        // restores all button and position states to defaults
        public void ClearStates( )
        {
            if ( IsActive )
            {
                MouseAccum = Vector2.Zero;
                MouseOldButtonState = 0;
            }
        }

        /// <summary>
        /// IN_MouseEvent
        /// </summary>
        public void MouseEvent( Int32 mstate )
        {
            if ( IsActive )
            {
                // perform button actions
                for ( var i = 0; i < MouseButtons; i++ )
                {
                    if ( ( mstate & ( 1 << i ) ) != 0 && ( MouseOldButtonState & ( 1 << i ) ) == 0 )
                    {
                        _keyboard.Event( KeysDef.K_MOUSE1 + i, true );
                    }

                    if ( ( mstate & ( 1 << i ) ) == 0 && ( MouseOldButtonState & ( 1 << i ) ) != 0 )
                    {
                        _keyboard.Event( KeysDef.K_MOUSE1 + i, false );
                    }
                }

                MouseOldButtonState = mstate;
            }
        }

        /// <summary>
        /// IN_MouseMove
        /// </summary>
        private void MouseMove( usercmd_t cmd )
        {
            if ( !IsActive )
                return;

            var current_pos = _window.GetMousePosition( ); //Cursor.Position;
            var window_center = WindowCenter;

            var mx = ( Int32 ) ( current_pos.X - window_center.X + MouseAccum.X );
            var my = ( Int32 ) ( current_pos.Y - window_center.Y + MouseAccum.Y );

            MouseAccum = Vector2.Zero;

            if ( Cvars.MouseFilter.Get<Boolean>( ) )
                Mouse = new Vector2( ( mx + OldMouse.X ) * 0.5f, ( my + OldMouse.Y ) * 0.5f );
            else
                Mouse = new Vector2( mx, my );

            OldMouse = new Vector2( mx, my );

            Mouse *= Sensitivity;

            OnMouseMove?.Invoke( cmd ); // Propagate mouse move event

            // if the mouse has moved, force it to the center, so there's room to move
            ResetToCentre( );
        }

        /// <summary>
        /// Reset the mouse position to the centre
        /// </summary>
        public void ResetToCentre()
        {
            var windowCentre = WindowCenter;
            //Cursor.Position = window_center;
            _window.SetMousePosition( windowCentre.X, windowCentre.Y );
        }
    }
}
