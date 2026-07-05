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
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.Factories.IO.WAD;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Framework.Logging;
using SharpQuake.Game.Client;
using SharpQuake.Logging;
using SharpQuake.Networking.Client;
using SharpQuake.Renderer;
using SharpQuake.Rendering;
using SharpQuake.Rendering.UI;
using SharpQuake.Rendering.UI.Elements;
using SharpQuake.Rendering.UI.Elements.HUD;
using SharpQuake.Sys;

// screen.h
// gl_screen.c

namespace SharpQuake
{
    /// <summary>
    /// SCR_functions
    /// </summary>
    public partial class Scr
    {
        public VRect VRect
        {
            get
            {
                return _VRect;
            }
        }

        public ClientVariable ViewSize
        {
            get
            {
                return Cvars.ViewSize;
            }
        }

        public Boolean BlockDrawing
        {
            get
            {
                return _video.Device.BlockDrawing;
            }
            set
            {
                _video.Device.BlockDrawing = value;
            }
        }

        public Boolean SkipUpdate
        {
            get
            {
                return _video.Device.SkipUpdate;
            }
            set
            {
                _video.Device.SkipUpdate = value;
            }
        }

        public Int32 ClearNotify;
        public Int32 glX;
        public Int32 glY;
        public Int32 glWidth;
        public Int32 glHeight;
        public Int32 FullUpdate;
        private VRect _VRect; // scr_vrect

        private Double _DisabledTime; // float scr_disabled_time

        // isPermedia
        private Boolean _IsInitialized;

        private Boolean _InUpdate;

        private Single _OldScreenSize; // float oldscreensize
        private Single _OldFov; // float oldfov

        private Boolean _IsMouseWindowed; // windowed_mouse (don't confuse with _windowed_mouse cvar)
                                          // scr_fullupdate    set to 0 to force full redraw
                                          // CHANGE

        public Action OnDrawMenus
        {
            get;
            set;
        }

        public ElementFactory Elements
        {
            get;
            private set;
        }

        public HudResources HudResources
        {
            get;
            private set;
        }

        public bool ShowBlur
        {
            get;
            set;
        }

        private readonly IConsoleLogger _logger;
        private readonly CommandFactory _commands;
        private readonly ClientVariableFactory _cvars;
        private readonly WadFactory _wads;
        private readonly IKeyboardInput _keyboard;
        private readonly IMouseInput _mouse;
        private readonly MainWindow _window;
        private readonly snd _sound;
        private readonly ClientState _clientState;
        private readonly IGameRenderer _gameRenderer;
        private readonly Vid _video;
        private readonly Drawer _drawer;
        private readonly VideoState _videoState;
        private readonly ElementFactory _elements;
        private readonly RenderState _renderState;
        private IGameConsoleLogger _gameLogger;

        // TODO - Refactor, DI has highlighted spaghetti madness
        public Scr( IConsoleLogger logger, CommandFactory commands, ClientVariableFactory cvars, WadFactory wads,
            IKeyboardInput keyboard, IMouseInput mouse, MainWindow window, snd sound, ClientState clientState,
            IGameRenderer gameRenderer, Vid video, Drawer drawer, VideoState videoState,
            ElementFactory elements, RenderState renderState )
        {
            _logger = logger;
            _commands = commands;
            _cvars = cvars;
            _wads = wads;
            _keyboard = keyboard;
            _mouse = mouse;
            _window = window;
            _sound = sound;
            _clientState = clientState;
            _gameRenderer = gameRenderer;
            _video = video;
            _drawer = drawer;
            _videoState = videoState;
            _elements = elements;
            _renderState = renderState;
            _renderState.OnTimeRefresh += EndRendering;

            HudResources = new HudResources( _video, this, _drawer, _wads, _clientState, _videoState );
            Elements = _elements; // TODO - REFACTOR
        }

        // SCR_Init
        public void Initialise( )
        {
            if ( Cvars.ViewSize == null )
            {
                Cvars.ViewSize = _cvars.Add( "viewsize", 100f, ClientVariableFlags.Archive );
                Cvars.Fov = _cvars.Add( "fov", 90f, ClientVariableFlags.Archive );	// 10 - 170
                Cvars.ConSpeed = _cvars.Add( "scr_conspeed", 3000 );
                Cvars.CenterTime = _cvars.Add( "scr_centertime", 2 );
                Cvars.ShowRam = _cvars.Add( "showram", true );
                Cvars.ShowTurtle = _cvars.Add( "showturtle", false );
                Cvars.ShowPause = _cvars.Add( "showpause", true );
                Cvars.PrintSpeed = _cvars.Add( "scr_printspeed", 8 );
                Cvars.glTripleBuffer = _cvars.Add( "gl_triplebuffer", 1, ClientVariableFlags.Archive );
                Cvars.ShowFPS = _cvars.Add( "r_showFPS", false );
            }

            //
            // register our commands
            //
            _commands.Add( "screenshot", ScreenShot_f );
            _commands.Add( "sizeup", SizeUp_f );
            _commands.Add( "sizedown", SizeDown_f );

            HudResources.Initialise( );
            Elements.Initialise( );

            if ( CommandLine.HasParam( "-fullsbar" ) )
                _videoState.FullSbarDraw = true;

            UpdateScreenData( );

            _IsInitialized = true;
        }

        public void InitialiseHUD( )
        {
            Elements.Initialise( ElementFactory.HUD );
            Elements.Initialise( ElementFactory.INTERMISSION );
            Elements.Initialise( ElementFactory.FINALE );
            Elements.Initialise( ElementFactory.SP_SCOREBOARD );
            Elements.Initialise( ElementFactory.MP_SCOREBOARD );
            Elements.Initialise( ElementFactory.MP_MINI_SCOREBOARD );
            Elements.Initialise( ElementFactory.FRAGS );
        }

        // void SCR_UpdateScreen (void);
        // This is called every frame, and can also be called explicitly to flush
        // text to the screen.
        //
        // WARNING: be very careful calling this from elsewhere, because the refresh
        // needs almost the entire 256k of stack space!
        public void UpdateScreen( )
        {
            if ( BlockDrawing || !_IsInitialized || _InUpdate )
                return;

            _InUpdate = true;
            try
            {
                if ( _window?.IsDisposing == true )
                {
                    if ( ( _window.VSync == VSyncMode.One ) != _video.Wait )
                        _window.VSync = ( _video.Wait ? VSyncMode.One : VSyncMode.None );
                }

                _videoState.Data.numpages = 2 + ( Int32 ) Cvars.glTripleBuffer.Get<Int32>( );

                _videoState.ScreenCopyTop = false;
                _videoState.ScreenCopyEverything = false;

                if ( _videoState.IsScreenDisabledForLoading )
                {
                    if ( ( Time.Absolute - _DisabledTime ) > 60 )
                    {
                        _videoState.IsScreenDisabledForLoading = false;
                        _logger.Print( "Load failed.\n" );
                    }
                    else
                        return;
                }

                _gameLogger = Elements.Get<VisualConsole>( ElementFactory.CONSOLE );

                if ( _gameLogger == null || !_gameLogger.IsInitialised )
                    return; // not initialized yet


                BeginRendering( );

                //
                // determine size of refresh window
                //
                if ( _OldFov != Cvars.Fov.Get<Single>( ) )
                {
                    _OldFov = Cvars.Fov.Get<Single>( );
                    _videoState.Data.recalc_refdef = true;
                }

                if ( _OldScreenSize != Cvars.ViewSize.Get<Single>( ) )
                {
                    _OldScreenSize = Cvars.ViewSize.Get<Single>( );
                    _videoState.Data.recalc_refdef = true;
                }

                if ( _videoState.Data.recalc_refdef )
                    CalcRefdef( );

                //
                // do 3D refresh drawing, and then update the screen
                //
                Elements.Get<VisualConsole>( ElementFactory.CONSOLE )?.Configure( );

                _renderState.OnRender?.Invoke( );

                _video.Device.Desc.NoiseGrain = Cvars.NoiseGrain.Get<float>( );
                _video.Device.Desc.Bloom = Cvars.Bloom.Get<float>( );
                _video.Device.Desc.ScreenBlur = ShowBlur ? 1f : 0f;

                _video.Device.Begin2DScene( Time.Absolute );


                //Set2D();

                //
                // draw any areas not covered by the refresh
                //
                TileClear( );

                DrawElements( );

                _video.Device.End2DScene( );

                _renderState.OnUpdatePalette?.Invoke( );
                EndRendering( );
            }
            finally
            {
                _InUpdate = false;
            }
        }

        private void UpdateScreenData( )
        {
            _videoState.Data.maxwarpwidth = VideoDef.WARP_WIDTH;
            _videoState.Data.maxwarpheight = VideoDef.WARP_HEIGHT;
            _videoState.Data.colormap = _gameRenderer.ColorMap;
            var v = BitConverter.ToInt32( _gameRenderer.ColorMap, 2048 );
            _videoState.Data.fullbright = 256 - EndianHelper.LittleLong( v );
        }

        /// <summary>
        /// Logic for drawing elements
        /// </summary>
        private void DrawElements( )
        {
            if ( Elements.IsVisible( ElementFactory.MODAL ) )
            {
                Elements.Draw( ElementFactory.HUD );
                _drawer.FadeScreen( );
                Elements.Draw( ElementFactory.MODAL );
                _videoState.ScreenCopyEverything = true;
            }
            else if ( Elements.IsVisible( ElementFactory.LOADING ) )
            {
                Elements.Draw( ElementFactory.LOADING );
                Elements.Draw( ElementFactory.HUD );
            }
            else if ( _clientState.Data.intermission == 1 && _keyboard.Destination == KeyDestination.key_game )
            {
                if ( _clientState.Data.gametype == ProtocolDef.GAME_DEATHMATCH )
                    Elements.Draw( ElementFactory.MP_SCOREBOARD );
                else
                    Elements.Draw( ElementFactory.INTERMISSION );
            }
            else if ( _clientState.Data.intermission == 2 && _keyboard.Destination == KeyDestination.key_game )
            {
                Elements.Draw( ElementFactory.FINALE );
                Elements.Draw( ElementFactory.CENTRE_PRINT );
            }
            else
            {
                Elements.Draw( ElementFactory.CROSSHAIR );
                Elements.Draw( ElementFactory.RAM );
                Elements.Draw( ElementFactory.NET );
                Elements.Draw( ElementFactory.TURTLE );
                Elements.Draw( ElementFactory.PAUSE );
                Elements.Draw( ElementFactory.CENTRE_PRINT );
                Elements.Draw( ElementFactory.HUD );
                Elements.Draw( ElementFactory.CONSOLE );
                OnDrawMenus?.Invoke( );
            }

            if ( Cvars.ShowFPS.Get<Boolean>() )
                Elements.Draw( ElementFactory.FPS );
        }

        /// <summary>
        /// GL_EndRendering
        /// </summary>
        public void EndRendering( )
        {
            if ( _window == null || _window.IsDisposing )
                return;

            var form = _window;
            if ( form == null )
                return;

            _video?.Device?.EndScene( );

            //if( !SkipUpdate || BlockDrawing )
            //    form.SwapBuffers();

            // handle the mouse state
            if ( !_video.WindowedMouse )
            {
                if ( _IsMouseWindowed )
                {
                    _mouse.DeactivateMouse( );
                    _mouse.ShowMouse( );
                    _IsMouseWindowed = false;
                }
            }
            else
            {
                _IsMouseWindowed = true;
                if ( _keyboard.Destination == KeyDestination.key_game && !_mouse.IsActive &&
                    _clientState.StaticData.state != cactive_t.ca_disconnected )// && ActiveApp)
                {
                    _mouse.ActivateMouse( );
                    _mouse.HideMouse( );
                }
                else if ( _mouse.IsActive && _keyboard.Destination != KeyDestination.key_game )
                {
                    _mouse.DeactivateMouse( );
                    _mouse.ShowMouse( );
                }
            }

            if ( _videoState.FullSbarDraw )
                Elements.SetDirty( ElementFactory.HUD );
        }

        /// <summary>
        /// SCR_EndLoadingPlaque
        /// </summary>
        public void EndLoadingPlaque( )
        {
            _videoState.IsScreenDisabledForLoading = false;
            FullUpdate = 0;
            _gameLogger.ClearNotify( );
        }

        /// <summary>
        /// SCR_BeginLoadingPlaque
        /// </summary>
        public void BeginLoadingPlaque( )
        {
            _sound.StopAllSounds( true );

            if ( _clientState.StaticData.state != cactive_t.ca_connected ||
                _clientState.StaticData.signon != ClientDef.SIGNONS )
                return;

            // redraw with no console and the loading plaque
            _gameLogger.ClearNotify( );
            Elements.Reset( ElementFactory.CENTRE_PRINT );
            Elements.Reset( ElementFactory.CONSOLE );

            Elements.Show( ElementFactory.LOADING );
            FullUpdate = 0;
            Elements.SetDirty( ElementFactory.HUD );
            UpdateScreen( );
            Elements.Hide( ElementFactory.LOADING );

            _videoState.IsScreenDisabledForLoading = true;
            _DisabledTime = Time.Absolute;
            FullUpdate = 0;
        }

        /// <summary>
        /// SCR_ModalMessage
        /// Displays a text string in the center of the screen and waits for a Y or N keypress.
        /// </summary>
        public Boolean ModalMessage( String text )
        {
            if ( _clientState.StaticData.state == cactive_t.ca_dedicated )
                return true;

            Elements.Enqueue( ElementFactory.MODAL, text );

            // draw a fresh screen
            FullUpdate = 0;

            Elements.Show( ElementFactory.MODAL );
            UpdateScreen( );
            Elements.Hide( ElementFactory.MODAL );

            _sound.ClearBuffer( );		// so dma doesn't loop current sound

            do
            {
                _keyboard.KeyCount = -1;        // wait for a key down and up
                SendKeyEvents( );
            } while ( _keyboard.LastPress != 'y' && _keyboard.LastPress != 'n' && _keyboard.LastPress != KeysDef.K_ESCAPE );

            FullUpdate = 0;
            UpdateScreen( );

            return ( _keyboard.LastPress == 'y' );
        }

        // SCR_SizeUp_f
        //
        // Keybinding command
        private void SizeUp_f( CommandMessage msg )
        {
            _cvars.Set( "viewsize", Cvars.ViewSize.Get<Single>( ) + 10 );
            _videoState.Data.recalc_refdef = true;
        }

        // SCR_SizeDown_f
        //
        // Keybinding command
        private void SizeDown_f( CommandMessage msg )
        {
            _cvars.Set( "viewsize", Cvars.ViewSize.Get<Single>( ) - 10 );
            _videoState.Data.recalc_refdef = true;
        }

        /// <summary>
        /// SCR_ScreenShot_f
        /// </summary>
        /// <param name="msg"></param>
        private void ScreenShot_f( CommandMessage msg )
        {
            _video.Device.ScreenShot( out var path );
            _logger.Print( $"Screenshot saved '{path}'.\n" );
        }

        /// <summary>
        /// GL_BeginRendering
        /// </summary>
        private void BeginRendering( )
        {
            if ( _window == null || _window.IsDisposing )
                return;

            glX = 0;
            glY = 0;
            glWidth = 0;
            glHeight = 0;

            var window = _window;
            if ( window != null )
            {
                var size = window.ClientSize;
                glWidth = size.Width;
                glHeight = size.Height;
            }

            _video?.Device?.BeginScene( );
        }

        // SCR_CalcRefdef
        //
        // Must be called whenever vid changes
        // Internal use only
        private void CalcRefdef( )
        {
            FullUpdate = 0; // force a background redraw
            _videoState.Data.recalc_refdef = false;

            // force the status bar to redraw
            Elements.SetDirty( ElementFactory.HUD );

            // bound viewsize
            if ( Cvars.ViewSize.Get<Single>( ) < 30 )
                _cvars.Set( "viewsize", 30f );
            if ( Cvars.ViewSize.Get<Single>( ) > 120 )
                _cvars.Set( "viewsize", 120f );

            // bound field of view
            if ( Cvars.Fov.Get<Single>( ) < 10 )
                _cvars.Set( "fov", 10f );
            if ( Cvars.Fov.Get<Single>( ) > 170 )
                _cvars.Set( "fov", 170f );

            // intermission is always full screen
            Single size;

            var full = false;

            if ( Cvars.NewUI?.Get<Boolean>( ) == true )
            {
                HudResources.Lines = 0;
                size = 120;
                full = true;
            }
            else
            {
                if ( _clientState.Data.intermission > 0 )
                    size = 120;
                else
                    size = Cvars.ViewSize.Get<Single>( );

                if ( size >= 120 )
                    HudResources.Lines = 0; // no status bar at all
                else if ( size >= 110 )
                    HudResources.Lines = 24; // no inventory
                else
                    HudResources.Lines = 24 + 16 + 8;
            

                if ( Cvars.ViewSize.Get<Single>( ) >= 100.0 )
                {
                    full = true;
                    size = 100.0f;
                }
                else
                    size = Cvars.ViewSize.Get<Single>( );

                if ( _clientState.Data.intermission > 0 )
                {
                    full = true;
                    size = 100;
                    HudResources.Lines = 0;
                }
            }
            size /= 100.0f;

            var h = _videoState.Data.height - HudResources.Lines;

            var rdef = _renderState.Data;
            rdef.vrect.width = ( Int32 ) ( _videoState.Data.width * size );
            if ( rdef.vrect.width < 96 )
            {
                size = 96.0f / rdef.vrect.width;
                rdef.vrect.width = 96;  // min for icons
            }

            rdef.vrect.height = ( Int32 ) ( _videoState.Data.height * size );
            if ( rdef.vrect.height > _videoState.Data.height - HudResources.Lines )
                rdef.vrect.height = _videoState.Data.height - HudResources.Lines;
            if ( rdef.vrect.height > _videoState.Data.height )
                rdef.vrect.height = _videoState.Data.height;
            rdef.vrect.x = ( _videoState.Data.width - rdef.vrect.width ) / 2;
            if ( full )
                rdef.vrect.y = 0;
            else
                rdef.vrect.y = ( h - rdef.vrect.height ) / 2;

            rdef.fov_x = Cvars.Fov.Get<Single>( );
            rdef.fov_y = Utilities.CalculateFOV( rdef.fov_x, rdef.vrect.width, rdef.vrect.height );

            _VRect = rdef.vrect;
        }


        // SCR_TileClear
        private void TileClear( )
        {
            var rdef = _renderState.Data;
            if ( rdef.vrect.x > 0 )
            {
                // left
                _drawer.TileClear( 0, 0, rdef.vrect.x, _videoState.Data.height - HudResources.Lines );
                // right
                _drawer.TileClear( rdef.vrect.x + rdef.vrect.width, 0,
                    _videoState.Data.width - rdef.vrect.x + rdef.vrect.width,
                    _videoState.Data.height - HudResources.Lines );
            }
            if ( rdef.vrect.y > 0 )
            {
                // top
                _drawer.TileClear( rdef.vrect.x, 0, rdef.vrect.x + rdef.vrect.width, rdef.vrect.y );
                // bottom
                _drawer.TileClear( rdef.vrect.x, rdef.vrect.y + rdef.vrect.height,
                    rdef.vrect.width, _videoState.Data.height - HudResources.Lines - ( rdef.vrect.height + rdef.vrect.y ) );
            }
        }

        /// <summary>
        /// SafePrint redirect to remove circular dependencies
        /// </summary>
        /// <param name="fmt"></param>
        /// <param name="args"></param>
        public void SafePrint( String fmt, params Object[] args )
        {
            _gameLogger.SafePrint( fmt, args );
        }

        /// <summary>
        /// Sys_SendKeyEventsa
        /// </summary>
        public void SendKeyEvents( )
        {
            SkipUpdate = false;
            _window.ProcessEvents( );
        }
    }
}
