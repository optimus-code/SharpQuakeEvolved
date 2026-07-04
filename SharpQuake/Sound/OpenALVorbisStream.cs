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
///
/// You should have received a copy of the GNU General Public License
/// along with this program; if not, write to the Free Software
/// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
/// </copyright>

using System;
using NVorbis;
using OpenTK.Audio.OpenAL;

namespace SharpQuake.Sound
{
    internal sealed class OpenALVorbisStream : IDisposable
    {
        private const Int32 BufferCount = 4;
        private const Int32 BufferMilliseconds = 200;

        private readonly String _path;

        private VorbisReader _reader;
        private Int32 _source;
        private Int32[] _buffers;

        private float[] _floatBuffer;
        private short[] _pcmBuffer;

        private ALFormat _format;

        private Boolean _isDisposed;
        private Boolean _isLooped;
        private Boolean _isPaused;
        private Boolean _endOfStream;

        private Single _volume = 1.0f;

        public OpenALVorbisStream( String path, Boolean isLooped )
        {
            _path = path;
            _isLooped = isLooped;

            OpenReader( );
            CreateOpenALObjects( );
        }

        public Boolean IsLooped
        {
            get => _isLooped;
            set => _isLooped = value;
        }

        public Single Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp( value, 0.0f, 1.0f );

                if ( _source != 0 )
                    AL.Source( _source, ALSourcef.Gain, _volume );
            }
        }

        public Boolean IsPaused => _isPaused;

        public Boolean IsPlaying
        {
            get
            {
                if ( _source == 0 )
                    return false;

                AL.GetSource( _source, ALGetSourcei.SourceState, out var state );

                return ( ALSourceState ) state == ALSourceState.Playing;
            }
        }

        public void Play( )
        {
            ThrowIfDisposed( );

            StopInternal( resetReader: true );

            _endOfStream = false;
            _isPaused = false;

            for ( var i = 0; i < _buffers.Length; i++ )
            {
                if ( !FillBuffer( _buffers[i] ) )
                    break;

                AL.SourceQueueBuffer( _source, _buffers[i] );
            }

            AL.Source( _source, ALSourcef.Gain, _volume );

            AL.GetSource( _source, ALGetSourcei.BuffersQueued, out var queued );

            if ( queued > 0 )
                AL.SourcePlay( _source );
        }

        public void Stop( )
        {
            if ( _isDisposed )
                return;

            StopInternal( resetReader: true );
        }

        public void Pause( )
        {
            if ( _isDisposed || _source == 0 )
                return;

            AL.SourcePause( _source );
            _isPaused = true;
        }

        public void Resume( )
        {
            if ( _isDisposed || _source == 0 )
                return;

            _isPaused = false;

            AL.GetSource( _source, ALGetSourcei.BuffersQueued, out var queued );

            if ( queued > 0 )
                AL.SourcePlay( _source );
        }

        public void Update( )
        {
            if ( _isDisposed || _source == 0 || _isPaused )
                return;

            AL.GetSource( _source, ALGetSourcei.BuffersProcessed, out var processed );

            while ( processed-- > 0 )
            {
                var buffer = AL.SourceUnqueueBuffer( _source );

                if ( buffer == 0 )
                    continue;

                if ( FillBuffer( buffer ) )
                    AL.SourceQueueBuffer( _source, buffer );
            }

            AL.GetSource( _source, ALGetSourcei.BuffersQueued, out var queued );
            AL.GetSource( _source, ALGetSourcei.SourceState, out var state );

            if ( queued > 0 && ( ALSourceState ) state != ALSourceState.Playing )
            {
                AL.SourcePlay( _source );
            }
        }

        private void OpenReader( )
        {
            _reader = new VorbisReader( _path );

            if ( _reader.Channels == 1 )
                _format = ALFormat.Mono16;
            else if ( _reader.Channels == 2 )
                _format = ALFormat.Stereo16;
            else
                throw new NotSupportedException(
                    $"Unsupported OGG channel count: {_reader.Channels}. Only mono/stereo are supported." );

            var samplesPerBuffer = Math.Max(
                _reader.Channels * 1024,
                _reader.SampleRate * _reader.Channels * BufferMilliseconds / 1000 );

            _floatBuffer = new float[samplesPerBuffer];
            _pcmBuffer = new short[samplesPerBuffer];
        }

        private void CreateOpenALObjects( )
        {
            _source = AL.GenSource( );
            _buffers = AL.GenBuffers( BufferCount );

            AL.Source( _source, ALSourceb.Looping, false );
            AL.Source( _source, ALSourcef.Gain, _volume );
        }

        private Boolean FillBuffer( Int32 buffer )
        {
            var samplesRead = 0;

            while ( samplesRead < _floatBuffer.Length )
            {
                var read = _reader.ReadSamples(
                    _floatBuffer,
                    samplesRead,
                    _floatBuffer.Length - samplesRead );

                if ( read == 0 )
                {
                    if ( _isLooped )
                    {
                        _reader.DecodedPosition = 0;
                        continue;
                    }

                    _endOfStream = true;
                    break;
                }

                samplesRead += read;
            }

            if ( samplesRead <= 0 )
                return false;

            for ( var i = 0; i < samplesRead; i++ )
            {
                var sample = Math.Clamp( _floatBuffer[i], -1.0f, 1.0f );
                _pcmBuffer[i] = ( short ) MathF.Round( sample * short.MaxValue );
            }

            AL.BufferData<short>(
                buffer,
                _format,
                _pcmBuffer.AsSpan( 0, samplesRead ),
                _reader.SampleRate );

            return true;
        }

        private void StopInternal( Boolean resetReader )
        {
            if ( _source == 0 )
                return;

            AL.SourceStop( _source );

            AL.GetSource( _source, ALGetSourcei.BuffersQueued, out var queued );

            while ( queued-- > 0 )
            {
                AL.SourceUnqueueBuffer( _source );
            }

            if ( resetReader && _reader != null )
                _reader.DecodedPosition = 0;

            _endOfStream = false;
            _isPaused = false;
        }

        private void ThrowIfDisposed( )
        {
            if ( _isDisposed )
                throw new ObjectDisposedException( nameof( OpenALVorbisStream ) );
        }

        public void Dispose( )
        {
            if ( _isDisposed )
                return;

            StopInternal( resetReader: false );

            if ( _buffers != null )
            {
                AL.DeleteBuffers( _buffers );
                _buffers = null;
            }

            if ( _source != 0 )
            {
                AL.DeleteSource( _source );
                _source = 0;
            }

            _reader?.Dispose( );
            _reader = null;

            _isDisposed = true;
        }
    }
}
