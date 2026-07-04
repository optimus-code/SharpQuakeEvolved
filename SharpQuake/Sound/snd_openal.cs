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
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using SharpQuake.Framework.IO.Sound;
using SharpQuake.Framework.Logging;

namespace SharpQuake
{
    internal class OpenALController : ISoundController
    {
        private const Int32 AL_BUFFER_COUNT = 24;
        private const Int32 BUFFER_SIZE = 0x10000;

        private Boolean _IsInitialized;

        private ALDevice _Device = ALDevice.Null;
        private ALContext _Context = ALContext.Null;

        private Int32 _Source;
        private Int32[] _Buffers;
        private Int32[] _BufferBytes;
        private ALFormat _BufferFormat;
        private Int32 _SamplesSent;
        private Queue<Int32> _FreeBuffers;

        private readonly IConsoleLogger _logger;
        private readonly snd _sound;

        public OpenALController( IConsoleLogger logger, snd sound )
        {
            _logger = logger;
            _sound = sound;
        }

        public Boolean IsInitialised
        {
            get
            {
                return _IsInitialized;
            }
        }

        private void FreeContext( )
        {
            if ( _Context != ALContext.Null )
            {
                ALC.MakeContextCurrent( _Context );
            }

            if ( _Source != 0 )
            {
                AL.SourceStop( _Source );

                // Detach any queued streaming buffers before deleting the source/buffers.
                AL.GetSource( _Source, ALGetSourcei.BuffersQueued, out var queued );

                if ( queued > 0 )
                {
                    try
                    {
                        AL.SourceUnqueueBuffers( _Source, queued );
                    }
                    catch
                    {
                        // If the driver refuses the unqueue during shutdown, deleting the
                        // source below will still release the source-side queue.
                    }
                }

                AL.DeleteSource( _Source );
                _Source = 0;
            }

            if ( _Buffers != null )
            {
                AL.DeleteBuffers( _Buffers );
                _Buffers = null;
            }

            _BufferBytes = null;
            _FreeBuffers = null;
            _SamplesSent = 0;

            if ( _Context != ALContext.Null )
            {
                ALC.MakeContextCurrent( ALContext.Null );
                ALC.DestroyContext( _Context );
                _Context = ALContext.Null;
            }

            if ( _Device != ALDevice.Null )
            {
                ALC.CloseDevice( _Device );
                _Device = ALDevice.Null;
            }
        }

        public void Initialise( )
        {
            FreeContext( );

            _Device = ALC.OpenDevice( null );

            if ( _Device == ALDevice.Null )
            {
                throw new InvalidOperationException( "OpenAL: failed to open the default audio device." );
            }

            _Context = ALC.CreateContext( _Device, new ALContextAttributes( ) );

            if ( _Context == ALContext.Null )
            {
                var error = ALC.GetError( _Device );

                ALC.CloseDevice( _Device );
                _Device = ALDevice.Null;

                throw new InvalidOperationException( $"OpenAL: failed to create audio context. ALC error: {error}" );
            }

            if ( !ALC.MakeContextCurrent( _Context ) )
            {
                var error = ALC.GetError( _Device );

                ALC.DestroyContext( _Context );
                ALC.CloseDevice( _Device );

                _Context = ALContext.Null;
                _Device = ALDevice.Null;

                throw new InvalidOperationException( $"OpenAL: failed to make audio context current. ALC error: {error}" );
            }

            _Source = AL.GenSource( );

            _Buffers = AL.GenBuffers( AL_BUFFER_COUNT );
            _BufferBytes = new Int32[AL_BUFFER_COUNT];
            _FreeBuffers = new Queue<Int32>( AL_BUFFER_COUNT );

            for ( var i = 0; i < _Buffers.Length; i++ )
            {
                _FreeBuffers.Enqueue( _Buffers[i] );
            }

            AL.Source( _Source, ALSourceb.Looping, false );

            _sound.shm.channels = 2;
            _sound.shm.samplebits = 16;
            _sound.shm.speed = 11025;
            _sound.shm.buffer = new Byte[BUFFER_SIZE];
            _sound.shm.soundalive = true;
            _sound.shm.splitbuffer = false;
            _sound.shm.samples = _sound.shm.buffer.Length / ( _sound.shm.samplebits / 8 );
            _sound.shm.samplepos = 0;
            _sound.shm.submission_chunk = 1;

            if ( _sound.shm.samplebits == 8 )
            {
                _BufferFormat = _sound.shm.channels == 2
                    ? ALFormat.Stereo8
                    : ALFormat.Mono8;
            }
            else
            {
                _BufferFormat = _sound.shm.channels == 2
                    ? ALFormat.Stereo16
                    : ALFormat.Mono16;
            }

            _SamplesSent = 0;
            _IsInitialized = true;
        }

        public void Shutdown( )
        {
            FreeContext( );
            _IsInitialized = false;
        }

        public void ClearBuffer( )
        {
            if ( !_IsInitialized || _Source == 0 )
                return;

            AL.SourceStop( _Source );

            AL.GetSource( _Source, ALGetSourcei.BuffersQueued, out var queued );

            if ( queued > 0 )
            {
                var buffers = AL.SourceUnqueueBuffers( _Source, queued );

                foreach ( var buffer in buffers )
                {
                    var idx = Array.IndexOf( _Buffers, buffer );

                    if ( idx != -1 )
                    {
                        _BufferBytes[idx] = 0;
                    }

                    if ( buffer != 0 && !_FreeBuffers.Contains( buffer ) )
                    {
                        _FreeBuffers.Enqueue( buffer );
                    }
                }
            }

            _SamplesSent = 0;
        }

        public Byte[] LockBuffer( )
        {
            return _sound.shm.buffer;
        }

        public void UnlockBuffer( Int32 bytes )
        {
            if ( !_IsInitialized || bytes <= 0 )
                return;

            AL.GetSource( _Source, ALGetSourcei.BuffersProcessed, out var processed );

            if ( processed > 0 )
            {
                var bufs = AL.SourceUnqueueBuffers( _Source, processed );

                foreach ( var buffer in bufs )
                {
                    if ( buffer == 0 )
                        continue;

                    var idx = Array.IndexOf( _Buffers, buffer );

                    if ( idx != -1 )
                    {
                        _SamplesSent += _BufferBytes[idx] >> ( ( _sound.shm.samplebits / 8 ) - 1 );
                        _SamplesSent &= ( _sound.shm.samples - 1 );
                        _BufferBytes[idx] = 0;
                    }

                    if ( !_FreeBuffers.Contains( buffer ) )
                    {
                        _FreeBuffers.Enqueue( buffer );
                    }
                }
            }

            if ( _FreeBuffers.Count == 0 )
            {
                _logger.DPrint( "UnlockBuffer: No free buffers!\n" );
                return;
            }

            var buf = _FreeBuffers.Dequeue( );

            if ( buf == 0 )
                return;

            ReadOnlySpan<byte> sourceBytes = _sound.shm.buffer.AsSpan( 0, bytes );

            AL.BufferData<byte>(
                buf,
                _BufferFormat,
                sourceBytes,
                _sound.shm.speed );

            AL.SourceQueueBuffer( _Source, buf );

            var bufferIndex = Array.IndexOf( _Buffers, buf );

            if ( bufferIndex != -1 )
            {
                _BufferBytes[bufferIndex] = bytes;
            }

            AL.GetSource( _Source, ALGetSourcei.SourceState, out var state );

            if ( ( ALSourceState ) state != ALSourceState.Playing )
            {
                AL.SourcePlay( _Source );

                _logger.DPrint(
                    "^9Sound resumed from ^0{0}^9, free ^0{1}^9 of ^0{2}^9 buffers\n",
                    ( ( ALSourceState ) state ).ToString( "F" ),
                    _FreeBuffers.Count,
                    _Buffers.Length );
            }
        }

        public Int32 GetPosition( )
        {
            if ( !_IsInitialized )
                return 0;

            Int32 offset = 0;

            AL.GetSource( _Source, ALGetSourcei.SourceState, out var state );

            if ( ( ALSourceState ) state != ALSourceState.Playing )
            {
                for ( var i = 0; i < _BufferBytes.Length; i++ )
                {
                    _SamplesSent += _BufferBytes[i] >> ( ( _sound.shm.samplebits / 8 ) - 1 );
                    _BufferBytes[i] = 0;
                }

                _SamplesSent &= ( _sound.shm.samples - 1 );
            }
            else
            {
                AL.GetSource( _Source, ALGetSourcei.SampleOffset, out offset );
            }

            return ( _SamplesSent + offset ) & ( _sound.shm.samples - 1 );
        }
    }
}