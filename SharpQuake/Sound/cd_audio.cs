/// <copyright>
///
/// Rewritten in C# by Yury Kiselev, 2010.
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

// cdaudio.h

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
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Sound;
using SharpQuake.Sys;
using System;
using System.IO;

namespace SharpQuake
{
    /// <summary>
    /// CDAudio_functions
    /// </summary>

    public class cd_audio : IDisposable
    {
#if _WINDOWS
        private ICDAudioController _Controller;
#else
        NullCDAudioController _Controller;
#endif

        private readonly IEngine _engine;
        private readonly IConsoleLogger _logger;
        private readonly CommandFactory _commands;

        public cd_audio( IEngine engine, IConsoleLogger logger, CommandFactory commands )
        {
            _engine = engine;
            _logger = logger;
            _commands = commands;
            _Controller = new NullCDAudioController( );
        }

        /// <summary>
        /// CDAudio_Init
        /// </summary>
        public Boolean Initialise( )
        {
            if ( _engine.IsDedicated )
                return false;

            if ( CommandLine.HasParam( "-nocdaudio" ) )
                return false;

            _Controller.Initialise( );

            if ( _Controller.IsInitialised )
            {
                _commands.Add( "cd", CD_f );
                _logger.Print( "CD Audio (Fallback) Initialized\n" );
            }

            return _Controller.IsInitialised;
        }

        // CDAudio_Play(byte track, qboolean looping)
        public void Play( Byte track, Boolean looping )
        {
            _Controller.Play( track, looping );
            _logger.DPrint( "DEBUG: track byte: ^0{0}^9 - loop byte: ^0{1}\n", track, looping );
        }

        // CDAudio_Stop
        public void Stop( )
        {
            _Controller.Stop( );
        }

        // CDAudio_Pause
        public void Pause( )
        {
            _Controller.Pause( );
        }

        // CDAudio_Resume
        public void Resume( )
        {
            _Controller.Resume( );
        }

        // CDAudio_Shutdown
        public void Dispose( )
        {
            _Controller.Shutdown( );
        }

        // CDAudio_Update
        public void Update( )
        {
            _Controller.Update( );
        }

        private void CD_f( CommandMessage msg )
        {
            if ( msg.Parameters == null || msg.Parameters.Length < 1 )
                return;

            var command = msg.Parameters[0];

            if ( Utilities.SameText( command, "on" ) )
            {
                _Controller.IsEnabled = true;
                return;
            }

            if ( Utilities.SameText( command, "off" ) )
            {
                if ( _Controller.IsPlaying )
                    _Controller.Stop( );
                _Controller.IsEnabled = false;
                return;
            }

            if ( Utilities.SameText( command, "reset" ) )
            {
                _Controller.IsEnabled = true;
                if ( _Controller.IsPlaying )
                    _Controller.Stop( );

                _Controller.ReloadDiskInfo( );
                return;
            }

            if ( Utilities.SameText( command, "remap" ) )
            {
                var ret = msg.Parameters.Length - 1;
                var remap = _Controller.Remap;
                if ( ret <= 0 )
                {
                    for ( var n = 1; n < 100; n++ )
                        if ( remap[n] != n )
                            _logger.Print( "  {0} -> {1}\n", n, remap[n] );
                    return;
                }
                for ( var n = 1; n <= ret; n++ )
                    remap[n] = ( Byte ) MathLib.atoi( msg.Parameters[n] );
                return;
            }

            if ( Utilities.SameText( command, "close" ) )
            {
                _Controller.CloseDoor( );
                return;
            }

            if ( !_Controller.IsValidCD )
            {
                _Controller.ReloadDiskInfo( );
                if ( !_Controller.IsValidCD )
                {
                    _logger.Print( "No CD in player.\n" );
                    return;
                }
            }

            if ( Utilities.SameText( command, "play" ) )
            {
                _Controller.Play( ( Byte ) MathLib.atoi( msg.Parameters[1] ), false );
                return;
            }

            if ( Utilities.SameText( command, "loop" ) )
            {
                _Controller.Play( ( Byte ) MathLib.atoi( msg.Parameters[1] ), true );
                return;
            }

            if ( Utilities.SameText( command, "stop" ) )
            {
                _Controller.Stop( );
                return;
            }

            if ( Utilities.SameText( command, "pause" ) )
            {
                _Controller.Pause( );
                return;
            }

            if ( Utilities.SameText( command, "resume" ) )
            {
                _Controller.Resume( );
                return;
            }

            if ( Utilities.SameText( command, "eject" ) )
            {
                if ( _Controller.IsPlaying )
                    _Controller.Stop( );
                _Controller.Eject( );
                return;
            }

            if ( Utilities.SameText( command, "info" ) )
            {
                _logger.Print( "%u tracks\n", _Controller.MaxTrack );
                if ( _Controller.IsPlaying )
                    _logger.Print( "Currently {0} track {1}\n", _Controller.IsLooping ? "looping" : "playing", _Controller.CurrentTrack );
                else if ( _Controller.IsPaused )
                    _logger.Print( "Paused {0} track {1}\n", _Controller.IsLooping ? "looping" : "playing", _Controller.CurrentTrack );
                _logger.Print( "Volume is {0}\n", _Controller.Volume );
                return;
            }
        }
    }

    internal class NullCDAudioController
    {
        private Byte[] _Remap;
        private OpenALVorbisStream oggStream;
        //private WaveOutEvent waveOut; // or WaveOutEvent()
        private Boolean _isLooping;
        String trackid;
        String trackpath;
        private Boolean _noAudio = false;
        private Boolean _noPlayback = false;
        private Single _Volume;
        private Boolean _isPlaying;
        private Boolean _isPaused;

        #region ICDAudioController Members

        public Boolean IsInitialised
        {
            get
            {
                return true;
            }
        }

        public Boolean IsEnabled
        {
            get
            {
                return true;
            }
            set
            {

            }
        }

        public Boolean IsPlaying
        {
            get
            {
                return _isPlaying;
            }
        }

        public Boolean IsPaused
        {
            get
            {
                return _isPaused;
            }
        }

        public Boolean IsValidCD
        {
            get
            {
                return false;
            }
        }

        public Boolean IsLooping
        {
            get
            {
                return _isLooping;
            }
        }

        public Byte[] Remap
        {
            get
            {
                return _Remap;
            }
        }

        public Byte MaxTrack
        {
            get
            {
                return 0;
            }
        }

        public Byte CurrentTrack
        {
            get
            {
                return 0;
            }
        }

        public Single Volume
        {
            get
            {
                return _Volume;
            }
            set
            {
                _Volume = value;
            }
        }

        private Single BgmVolume
        {
            get
            {
                return Cvars.BgmVolume.Get<Single>( );
            }
        }

        public NullCDAudioController( )
        {
            _Remap = new Byte[100];
        }

        public void Initialise( )
        {
            _Volume = BgmVolume;

            if ( Directory.Exists( String.Format( "{0}/{1}/music/", QuakeParameter.globalbasedir, QuakeParameter.globalgameid ) ) == false )
            {
                _noAudio = true;
            }
        }

        public void Play( Byte track, Boolean looping )
        {
            if ( _noAudio )
                return;

            trackid = track.ToString( "00" );
            trackpath = String.Format(
                "{0}/{1}/music/track{2}.ogg",
                QuakeParameter.globalbasedir,
                QuakeParameter.globalgameid,
                trackid );

#if DEBUG
            Console.WriteLine( "DEBUG: track path:{0} ", trackpath );
#endif

            try
            {
                _isLooping = looping;

                oggStream?.Dispose( );

                oggStream = new OpenALVorbisStream( trackpath, looping );
                oggStream.Volume = _Volume;
                oggStream.Play( );

                _isPlaying = true;
                _isPaused = false;
                _noPlayback = false;
            }
            catch ( Exception )
            {
                Console.WriteLine( "Could not find or play {0}", trackpath );

                oggStream?.Dispose( );
                oggStream = null;

                _isPlaying = false;
                _isPaused = false;
                _noPlayback = true;
            }
        }

        public void Stop( )
        {
            if ( _noAudio )
                return;

            oggStream?.Stop( );

            _isPlaying = false;
            _isPaused = false;
        }

        public void Pause( )
        {
            if ( _noAudio )
                return;

            oggStream?.Pause( );

            _isPlaying = false;
            _isPaused = true;
        }

        public void Resume( )
        {
            if ( _noAudio )
                return;

            oggStream?.Resume( );

            _isPlaying = true;
            _isPaused = false;
        }

        public void Shutdown( )
        {
            oggStream?.Dispose( );
            oggStream = null;

            _isPlaying = false;
            _isPaused = false;
        }

        public void Update( )
        {
            if ( _noAudio || _noPlayback || oggStream == null )
                return;

            _Volume = BgmVolume;
            oggStream.Volume = _Volume;
            oggStream.Update( );

            _isPaused = oggStream.IsPaused;
            _isPlaying = oggStream.IsPlaying;
        }

        public void ReloadDiskInfo( )
        {
        }

        public void CloseDoor( )
        {
        }

        public void Eject( )
        {
        }

        #endregion ICDAudioController Members
    }
}
