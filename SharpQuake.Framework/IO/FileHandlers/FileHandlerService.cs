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

using SharpQuake.Framework.IO.FileHandlers.Local;
using SharpQuake.Framework.IO.FileHandlers.PAK;
using SharpQuake.Framework.IO.FileHandlers.PK3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SharpQuake.Framework.IO.FileHandlers
{
    public class FileHandlerService
    {
        private List<(String Path, Type Type)> SearchPaths
        {
            get;
            set;
        } = new List<(String, Type)>( );

        private Dictionary<String, Type> Handlers
        {
            get;
            set;
        } = new Dictionary<String, Type>( );

        private Dictionary<String, (IArchiveFileHandler Handler, DateTime Timestamp)> ArchivePool
        {
            get;
            set;
        } = new Dictionary<String, (IArchiveFileHandler, DateTime)>( );

        private List<(IArchiveEntryReader Reader, DateTime Timestamp)> ReaderPool
        {
            get;
            set;
        } = new List<(IArchiveEntryReader, DateTime)>( );

        private DateTime LastDisposeCheck
        {
            get;
            set;
        }

        public FileHandlerService( )
        {
            RegisterHandlers( );
        }

        private void RegisterHandlers()
        {
            RegisterHandler<LocalArchiveFileHandler>( "" );
            RegisterHandler<PAKArchiveFileHandler>( ".pak" );
            RegisterHandler<PK3ArchiveFileHandler>( ".pk3" );
        }

        private void RegisterHandler<THandler>( String extension )
            where THandler : IArchiveFileHandler
        {
            if ( Handlers.ContainsKey( extension ) )
                return;

            Handlers.Add( extension, typeof( THandler ) );
        }

        private void AddSearchPath( String path )
        {
            var key = Path.HasExtension( path ) ? Path.GetExtension( path ).ToLower( ) : "";

            foreach ( var handler in Handlers )
            {
                if ( handler.Key == key )
                {
                    SearchPaths.Add( (path, handler.Value) );
                    return;
                }
            }            
        }

        public void AddGameDirectory( String path )
        {
            if ( SearchPaths.Count( p => p.Path == path ) > 0 )
                return;

            // Add the directory to the search path
            AddSearchPath( path );

            foreach ( var archiveFile in FindArchiveFiles( path ) )
                AddSearchPath( archiveFile );
        }

        private String[] FindArchiveFiles( String path )
        {
            var results = new List<String>( );

            // Originally Quake 1 just loaded files prefixed with PAK
            // etc. We just look for files in the search path with the
            // extension to allow custom archive names.

            foreach ( var handler in Handlers )
            {
                // Ignore local file system handler
                if ( String.IsNullOrEmpty( handler.Key ) )
                    continue;

                var files = Directory.GetFiles( path, "*" + handler.Key );

                if ( files?.Length > 0 )
                    results.AddRange( files );
            }

            // Order from Z-A to replicate later Quake engine load order
            // E.g. z-Pak0.pak would take precedence over Pak0.pak
            return results
                .OrderByDescending( f => Path.GetFileNameWithoutExtension( f ) )
                .ToArray();
        }

        private IArchiveFileHandler OpenArchive( Type type, String path )
        {
            if ( ArchivePool.ContainsKey( path ) )
                return ArchivePool[path].Handler;

            var handler = ( IArchiveFileHandler ) Activator.CreateInstance( type, path );

            ArchivePool.Add( path, (handler, DateTime.Now) );

            return handler;
        }

        public List<String> Search( String path )
        {
            var results = new List<String>();
            var searchCriteria = path;
            var isWildCard = path.StartsWith( "*." );

            if ( isWildCard )
                searchCriteria = path.Replace( "*.", "." );

            foreach ( var searchPath in SearchPaths )
            {
                var archiveInstance = ( IDisposable ) OpenArchive( searchPath.Type, searchPath.Path );
                var archive = ( IArchiveFileHandler ) archiveInstance;
                    
                foreach ( var entry in archive.Entries )
                {
                    var extension = Path.GetExtension( entry );

                    if ( isWildCard && extension == searchCriteria ||
                        entry.ToLower() == searchCriteria )
                    {
                        results.Add( entry );
                    }
                }
            }

            return results;
        }

        private IArchiveEntryReader OpenFile( String path )
        {
            foreach ( var searchPath in SearchPaths )
            {
                var archiveInstance = ( IDisposable ) OpenArchive( searchPath.Type, searchPath.Path );
                var archive = ( IArchiveFileHandler ) archiveInstance;
                IArchiveEntryReader reader = null;

                var key = Handlers
                    .Where( h => h.Value == archive.GetType( ) )
                    .FirstOrDefault().Key.Replace( ".", "" ).ToUpper();

                foreach ( var entry in archive.Entries )
                {
                    var extension = Path.GetExtension( entry );

                    if ( entry == path )
                    {
                        reader = archive.OpenRead( entry );

                        ConsoleWrapper.DPrint( "^9{0}File: ^0{1}^9 : ^0{2}^9\n", key, Path.GetFileName( searchPath.Path ), path );

                        break;
                    }
                }

                if ( reader != null )
                    return reader;
            }

            return null;
        }

        public IArchiveEntryReader FindFile( String path, out DisposableWrapper<BinaryReader> file )
        {
            file = null;

            //var matchingPath = Search( path ).FirstOrDefault( );

            //if ( String.IsNullOrEmpty( matchingPath ) )
            //    return null;

            var reader = OpenFile( path );

            if ( reader == null )
                return null;

            file = new DisposableWrapper<BinaryReader>( new BinaryReader( reader.Stream, Encoding.ASCII ), true );
            return reader;
        }

        private void ExpireDisposables()
        {
            LastDisposeCheck = DateTime.Now;

            // Dispose any readers left open
            foreach ( var file in ReaderPool.ToArray( ) )
            {
                if ( DateTime.Now.Subtract( file.Timestamp ).TotalSeconds >= 30 )
                {
                    file.Reader.Dispose( );
                    ReaderPool.RemoveAll( r => r == file );
                }
            }

            // Dispose any archives left open
            foreach ( var path in ArchivePool.Keys.ToList() )
            {
                var archive = ArchivePool[path];

                if ( DateTime.Now.Subtract( archive.Timestamp ).TotalSeconds >= 30 )
                {
                    archive.Handler.Dispose( );
                    ArchivePool.Remove( path );
                }
            }

        }

        public void QueueForCleanup( IArchiveEntryReader reader )
        {
            ReaderPool.Add( (reader, DateTime.Now) );
        }

        public void Tick()
        {
            if ( DateTime.Now.Subtract( LastDisposeCheck ).TotalSeconds >= 5 )
                    ExpireDisposables( );
        }
    }
}
