/// <copyright>
///
/// SharpQuakeEvolved changes by optimus-code, 2019
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
/// </copyright>

using System;
using System.Drawing;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Renderer.Desktop;

using OpenTK.Mathematics;

using OTKGameWindow = OpenTK.Windowing.Desktop.GameWindow;
using OTKGameWindowSettings = OpenTK.Windowing.Desktop.GameWindowSettings;
using OTKNativeWindowSettings = OpenTK.Windowing.Desktop.NativeWindowSettings;

using OTKContextProfile = OpenTK.Windowing.Common.ContextProfile;
using OTKCursorState = OpenTK.Windowing.Common.CursorState;
using OTKVSyncMode = OpenTK.Windowing.Common.VSyncMode;
using OTKWindowBorder = OpenTK.Windowing.Common.WindowBorder;
using OTKWindowState = OpenTK.Windowing.Common.WindowState;

namespace SharpQuake.Renderer.OpenGL.Desktop
{
    public class GLWindow : BaseWindow
    {
        private OTKGameWindow OpenTKWindow { get; }

        private IBaseIcon _icon;

        public override VSyncMode VSync
        {
            get
            {
                switch ( OpenTKWindow.VSync )
                {
                    case OTKVSyncMode.On:
                        return VSyncMode.One;

                    case OTKVSyncMode.Adaptive:
                        return VSyncMode.Other;

                    default:
                        return VSyncMode.None;
                }
            }
            set
            {
                switch ( value )
                {
                    case VSyncMode.One:
                        OpenTKWindow.VSync = OTKVSyncMode.On;
                        break;

                    case VSyncMode.None:
                        OpenTKWindow.VSync = OTKVSyncMode.Off;
                        break;

                    case VSyncMode.Other:
                        OpenTKWindow.VSync = OTKVSyncMode.Adaptive;
                        break;
                }
            }
        }

        public override IBaseIcon Icon
        {
            get => _icon;
            set
            {
                _icon = value;

                if ( value != null )
                {
                    OpenTKWindow.Icon = ( ( GLWindowIcon ) value ).Icon;
                }
            }
        }

        public override Size ClientSize
        {
            get
            {
                Vector2i size = OpenTKWindow.ClientSize;
                return new Size( size.X, size.Y );
            }
            set
            {
                OpenTKWindow.ClientSize = new Vector2i( value.Width, value.Height );
            }
        }

        public override bool IsFullScreen
        {
            get => OpenTKWindow.IsFullscreen;
        }

        public override bool Focused
        {
            get => OpenTKWindow.IsFocused;
        }

        public override bool IsMinimised
        {
            get => OpenTKWindow.WindowState == OTKWindowState.Minimized;
        }

        public override bool CursorVisible
        {
            get
            {
                return OpenTKWindow.CursorState != OTKCursorState.Hidden &&
                       OpenTKWindow.CursorState != OTKCursorState.Grabbed;
            }
            set
            {
                OpenTKWindow.CursorState = value
                    ? OTKCursorState.Normal
                    : OTKCursorState.Hidden;
            }
        }

        public override Rectangle Bounds
        {
            get
            {
                Box2i bounds = OpenTKWindow.Bounds;
                return new Rectangle(
                    bounds.Min.X,
                    bounds.Min.Y,
                    bounds.Size.X,
                    bounds.Size.Y );
            }
            set
            {
                OpenTKWindow.Bounds = new Box2i(
                    value.Left,
                    value.Top,
                    value.Right,
                    value.Bottom );
            }
        }

        public override bool IsMouseActive
        {
            get
            {
                // OpenTK 4/GLFW does not expose the old OpenTK 3-style static mouse device
                // connection API. If the window exists, mouse input can be processed.
                return OpenTKWindow.Exists;
            }
        }

        public GLWindow( string title, Size size, bool isFullScreen )
            : base( title, size, isFullScreen )
        {
            var nativeWindowSettings = new OTKNativeWindowSettings
            {
                Title = title,
                ClientSize = new Vector2i( size.Width, size.Height ),
                WindowState = isFullScreen ? OTKWindowState.Fullscreen : OTKWindowState.Normal,
                WindowBorder = isFullScreen ? OTKWindowBorder.Hidden : OTKWindowBorder.Fixed,
                StartVisible = true,
                StartFocused = true,

                APIVersion = new Version( 2, 1 ),
                Profile = OTKContextProfile.Any,
                Flags = OpenTK.Windowing.Common.ContextFlags.Default,
            };

            OpenTKWindow = new OTKGameWindow(
                OTKGameWindowSettings.Default,
                nativeWindowSettings );

            RouteEvents( );

            // OpenTK.DisplayDevice was removed in OpenTK 4.
            // Port GLDevice to accept GameWindow/NativeWindow directly.
            Device = new GLDevice( OpenTKWindow );
        }

        public override void RouteEvents( )
        {
            OpenTKWindow.FocusedChanged += args =>
            {
                OnFocusedChanged( );
            };

            OpenTKWindow.Closing += args =>
            {
                OnClosing( );
            };

            OpenTKWindow.UpdateFrame += args =>
            {
                OnUpdateFrame( args.Time );
            };

            OpenTKWindow.KeyDown += args =>
            {
                KeyDown?.Invoke(
                    OpenTKWindow,
                    new KeyboardKeyEventArgs( ( Key ) ( int ) args.Key ) );
            };

            OpenTKWindow.KeyUp += args =>
            {
                KeyUp?.Invoke(
                    OpenTKWindow,
                    new KeyboardKeyEventArgs( ( Key ) ( int ) args.Key ) );
            };

            OpenTKWindow.MouseMove += args =>
            {
                MouseMove?.Invoke( OpenTKWindow, EventArgs.Empty );
            };

            OpenTKWindow.MouseDown += args =>
            {
                MouseDown?.Invoke(
                    OpenTKWindow,
                    new MouseButtonEventArgs( ( MouseButton ) ( int ) args.Button, args.IsPressed ) );
            };

            OpenTKWindow.MouseUp += args =>
            {
                MouseUp?.Invoke(
                    OpenTKWindow,
                    new MouseButtonEventArgs( ( MouseButton ) ( int ) args.Button, args.IsPressed ) );
            };

            OpenTKWindow.MouseWheel += args =>
            {
                MouseWheel?.Invoke(
                    OpenTKWindow,
                    new MouseWheelEventArgs( Math.Sign( args.OffsetY ) ) );
            };
        }

        public override void Run( )
        {
            OpenTKWindow.Run( );
        }

        protected override void OnFocusedChanged( )
        {
            // Intentionally empty.
        }

        protected override void OnClosing( )
        {
            // Intentionally empty.
        }

        protected override void OnUpdateFrame( double time )
        {
            // Intentionally empty.
        }

        public override void Present( )
        {
            OpenTKWindow.SwapBuffers( );
        }

        public override void SetFullScreen( bool isFullScreen )
        {
            if ( isFullScreen )
            {
                OpenTKWindow.WindowBorder = OTKWindowBorder.Hidden;
                OpenTKWindow.WindowState = OTKWindowState.Fullscreen;
            }
            else
            {
                OpenTKWindow.WindowState = OTKWindowState.Normal;
                OpenTKWindow.WindowBorder = OTKWindowBorder.Fixed;
            }
        }

        public override void ProcessEvents( )
        {
            // OpenTK 4 uses ProcessEvents(timeout). 0.0 means non-blocking poll.
            OpenTKWindow.ProcessEvents( 0.0 );
        }

        public override void Exit( )
        {
            OpenTKWindow.Close( );
        }

        public override void SetMousePosition( int x, int y )
        {
            // Preserve the old OpenTK 3 behavior as closely as possible:
            // old static Mouse.SetPosition used screen coordinates, while
            // OpenTK 4 MousePosition is client-relative.
            Vector2i clientPosition = OpenTKWindow.PointToClient( new Vector2i( x, y ) );
            OpenTKWindow.MousePosition = new Vector2( clientPosition.X, clientPosition.Y );
        }

        public override Point GetMousePosition( )
        {
            Vector2 mousePosition = OpenTKWindow.MousePosition;

            Vector2i screenPosition = OpenTKWindow.PointToScreen(
                new Vector2i( ( int ) mousePosition.X, ( int ) mousePosition.Y ) );

            return new Point( screenPosition.X, screenPosition.Y );
        }

        public override void Dispose( )
        {
            base.Dispose( );

            OpenTKWindow.Dispose( );

            IsDisposed = true;
        }
    }
}