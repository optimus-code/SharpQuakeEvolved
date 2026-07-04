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
using System.IO;
using SharpQuake.Desktop;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Renderer;
using SharpQuake.Rendering;
using SharpQuake.Sys;

// vid.h -- video driver defs

namespace SharpQuake
{
	/// <summary>
	/// Vid_functions
	/// </summary>
	public class Vid : IDisposable
    {
        public UInt16[] Table8to16
        {
            get
            {
                return Device.Palette.Table8to16;//_8to16table;
            }
        }

        public UInt32[] Table8to24
        {
            get
            {
                return Device.Palette.Table8to24;//_8to24table;
            }
        }

        public Byte[] Table15to8
        {
            get
            {
                return Device.Palette.Table15to8;//_15to8table;
            }
        }

        public System.Boolean glZTrick
        {
            get
            {
                return Cvars.glZTrick.Get<Boolean>( );
            }
        }

        public System.Boolean WindowedMouse
        {
            get
            {
                return Cvars.WindowedMouse.Get<Boolean>( );
            }
        }

        public Boolean Wait
        {
            get
            {
                return Cvars.Wait.Get<Boolean>( );
            }
        }

        public Int32 ModeNum
        {
            get
            {
                return Device.ChosenMode;//_ModeNum;
            }
        }    
       
        public BaseDevice Device
        {
            get
            {
                return _window.Device;
            }
        }

        private readonly IConsoleLogger _logger;
        private readonly IKeyboardInput _keyboard;
        private readonly IMouseInput _mouse;
        private readonly CommandFactory _commands;
        private readonly ClientVariableFactory _cvars;
        private readonly cd_audio _cdAudio;
        private readonly MainWindow _window;
        private readonly VideoState _videoState;

        public Vid( IConsoleLogger logger, IKeyboardInput keyboard, IMouseInput mouse, 
            CommandFactory commands, ClientVariableFactory cvars, cd_audio cdAudio,
            MainWindow window, VideoState videoState )
        {
            _logger = logger;
            _keyboard = keyboard;
            _mouse = mouse;
            _commands = commands;
            _cvars = cvars;
            _cdAudio = cdAudio;
            _window = window;
            _videoState = videoState;
        }

        private void InitialiseClientVariables()
		{
            if ( Cvars.glZTrick == null )
            {
                Cvars.glZTrick = _cvars.Add( "gl_ztrick", true );
                Cvars.Mode = _cvars.Add( "vid_mode", 0 );
                Cvars.DefaultMode = _cvars.Add( "_vid_default_mode", 0, ClientVariableFlags.Archive );
                Cvars.DefaultModeWin = _cvars.Add( "_vid_default_mode_win", 3, ClientVariableFlags.Archive );
                Cvars.Wait = _cvars.Add( "vid_wait", false );
                Cvars.NoPageFlip = _cvars.Add( "vid_nopageflip", 0, ClientVariableFlags.Archive );
                Cvars.WaitOverride = _cvars.Add( "_vid_wait_override", 0, ClientVariableFlags.Archive );
                Cvars.ConfigX = _cvars.Add( "vid_config_x", 800, ClientVariableFlags.Archive );
                Cvars.ConfigY = _cvars.Add( "vid_config_y", 600, ClientVariableFlags.Archive );
                Cvars.StretchBy2 = _cvars.Add( "vid_stretch_by_2", 1, ClientVariableFlags.Archive );
                Cvars.WindowedMouse = _cvars.Add( "_windowed_mouse", true, ClientVariableFlags.Archive );
            }
        }

        private void InitialiseCommands()
		{
            _commands.Add( "vid_nummodes", NumModes_f );
            _commands.Add( "vid_describecurrentmode", DescribeCurrentMode_f );
            _commands.Add( "vid_describemode", DescribeMode_f );
            _commands.Add( "vid_describemodes", DescribeModes_f );
        }

        /// <summary>
        /// VID_Init (unsigned char *palette)
        /// Called at startup to set up translation tables, takes 256 8 bit RGB values
        /// the palette data will go away after the call, so it must be copied off if
        /// the video driver will need it again
        /// </summary>
        /// <param name="palette"></param>
        public void Initialise( Byte[] palette )
        {
            InitialiseClientVariables();
            InitialiseCommands();

            Device.Initialise( palette );

            UpdateConsole( );

            // Moved from SetMode

            // so Con_Printfs don't mess us up by forcing vid and snd updates
            var temp = _videoState.IsScreenDisabledForLoading;
            _videoState.IsScreenDisabledForLoading = true;

            _cdAudio.Pause( );

            Device.SetMode( Device.ChosenMode, palette );

            var vid = _videoState.Data;

            UpdateConsole( false );

            vid.width = Device.Desc.Width; // vid.conwidth
            vid.height = Device.Desc.Height;
            vid.numpages = 2;

            _cdAudio.Resume( );

            _videoState.IsScreenDisabledForLoading = temp;

            _cvars.Set( "vid_mode", Device.ChosenMode );

            // fix the leftover Alt from any Alt-Tab or the like that switched us away
            ClearAllStates( );

            _videoState.SafePrint( "Video mode {0} initialized.\n", Device.GetModeDescription( Device.ChosenMode ) );

            vid.recalc_refdef = true;

            if ( Device.Desc.Renderer.StartsWith( "PowerVR", StringComparison.InvariantCultureIgnoreCase ) )
                _videoState.FullSbarDraw = true;

            if ( Device.Desc.Renderer.StartsWith( "Permedia", StringComparison.InvariantCultureIgnoreCase ) )
                _videoState.IsPermedia = true;

            CheckTextureExtensions( );

            Directory.CreateDirectory( Path.Combine( FileSystem.GameDir, "glquake" ) );
        }


        private void UpdateConsole( System.Boolean isInitialStage = true )
        {
            var vid = _videoState.Data;

            if ( isInitialStage )
            {
                var i2 = CommandLine.CheckParm( "-conwidth" );

                if ( i2 > 0 )
                    vid.conwidth = MathLib.atoi( CommandLine.Argv( i2 + 1 ) );
                else
                    vid.conwidth = 640;

                vid.conwidth &= 0xfff8; // make it a multiple of eight

                if ( vid.conwidth < 320 )
                    vid.conwidth = 320;

                // pick a conheight that matches with correct aspect
                vid.conheight = vid.conwidth * 3 / 4;

                i2 = CommandLine.CheckParm( "-conheight" );

                if ( i2 > 0 )
                    vid.conheight = MathLib.atoi( CommandLine.Argv( i2 + 1 ) );

                if ( vid.conheight < 200 )
                    vid.conheight = 200;
            }
            else
            {
                if ( vid.conheight > Device.Desc.Height )
                    vid.conheight = Device.Desc.Height;
                if ( vid.conwidth > Device.Desc.Width )
                    vid.conwidth = Device.Desc.Width;
            }
        }

        /// <summary>
        /// VID_Shutdown
        /// Called at shutdown
        /// </summary>
        public void Dispose()
        {
            Device.Dispose( );
            //_IsInitialized = false;
        }

        /// <summary>
        /// VID_GetModeDescription
        /// </summary>
        public String GetModeDescription( Int32 mode )
        {
            return Device.GetModeDescription( mode );
        }

        /// <summary>
        /// VID_NumModes_f
        /// </summary>
        /// <param name="msg"></param>
        private void NumModes_f( CommandMessage msg )
        {
            var nummodes = Device.AvailableModes.Length;

            if( nummodes == 1 )
                _logger.Print( "{0} video mode is available\n", nummodes );
            else
                _logger.Print( "{0} video modes are available\n", nummodes );
        }

        /// <summary>
        /// VID_DescribeCurrentMode_f
        /// </summary>
        /// <param name="msg"></param>
        private void DescribeCurrentMode_f( CommandMessage msg )
        {
            _logger.Print( "{0}\n", GetModeDescription( Device.ChosenMode ) );
        }

        /// <summary>
        /// VID_DescribeMode_f
        /// </summary>
        /// <param name="msg"></param>
        private void DescribeMode_f( CommandMessage msg )
        {
            var modenum = MathLib.atoi( msg.Parameters[0] );

            _logger.Print( "{0}\n", GetModeDescription( modenum ) );
        }

        /// <summary>
        /// VID_DescribeModes_f
        /// </summary>
        /// <param name="msg"></param>
        private void DescribeModes_f( CommandMessage msg )
        {
            for ( var i = 0; i < Device.AvailableModes.Length; i++ )
            {
                _logger.Print( "{0}:{1}\n", i, GetModeDescription( i ) );
            }
        }

        /// <summary>
        /// ClearAllStates
        /// </summary>
        private void ClearAllStates()
        {
            // send an up event for each key, to make sure the server clears them all
            for( var i = 0; i < 256; i++ )
            {
                _keyboard.Event( i, false );
            }

            _keyboard.ClearStates();
            _mouse.ClearStates();
        }

        /// <summary>
        /// CheckTextureExtensions
        /// </summary>
        private void CheckTextureExtensions()
        {
            const String TEXTURE_EXT_STRING = "GL_EXT_texture_object";

            // check for texture extension
            var texture_ext = Device.Desc.Extensions.Contains( TEXTURE_EXT_STRING );
        }
    }
}
