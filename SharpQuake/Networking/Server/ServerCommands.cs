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

using SharpQuake.Desktop;
using SharpQuake.Factories.Rendering;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Framework.Logging;
using SharpQuake.Game.Client;
using SharpQuake.Game.Data.Models;
using SharpQuake.Networking.Client;
using SharpQuake.Sys;
using SharpQuake.Sys.Programs;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SharpQuake.Networking.Server
{
    public class ServerCommands
    {
        public Boolean ShowFPS
        {
            get;
            private set;
        }

        private readonly IEngine _engine;
        private readonly IConsoleLogger _logger;
        private readonly IKeyboardInput _keyboard;
        private readonly CommandFactory _commands;
        private readonly ClientVariableFactory _cvars;
        private readonly ModelFactory _models;
        private readonly ClientState _clientState;
        private readonly ProgramsState _programsState;
        private readonly ProgramsEdict _programsEdict;
        private readonly ServerState _serverState;
        private readonly SharpQuake.Server _server;
        private readonly ServerWorld _serverWorld;
        private readonly client _client;
        private readonly Network _network;
        private readonly ProgramsExec _programsExec;
        private readonly Scr _screen;
        private readonly LocalHost _localHost;

        public ServerCommands( IEngine engine, IConsoleLogger logger, IKeyboardInput keyboard, CommandFactory commands, ClientVariableFactory cvars,
            ModelFactory models, ServerState serverState, SharpQuake.Server server, ServerWorld serverWorld, ClientState clientState, ProgramsState programsState, 
            ProgramsEdict programsEdict, ProgramsExec programsExec, client client, Network network,
            Scr screen, LocalHost localHost )
        {
            _engine = engine;
            _logger = logger;
            _keyboard = keyboard;
            _commands = commands;
            _cvars = cvars;
            _models = models;
            _serverState = serverState;
            _server = server;
            _client = client;
            _clientState = clientState;
            _programsState = programsState;
            _programsEdict = programsEdict;
            _programsExec = programsExec;
            _client = client;
            _network = network;
            _screen = screen;
            _localHost = localHost;
        }

        /// <summary>
        /// Host_InitCommands
        /// </summary>
        public void Initialise( )
        {
            _commands.Add( "status", Status_f );
            _commands.Add( "god", God_f );
            _commands.Add( "notarget", Notarget_f );
            _commands.Add( "fly", Fly_f );
            _commands.Add( "map", Map_f );
            _commands.Add( "restart", Restart_f );
            _commands.Add( "changelevel", Changelevel_f );
            _commands.Add( "connect", Connect_f );
            _commands.Add( "reconnect", Reconnect_f );
            _commands.Add( "name", Name_f );
            _commands.Add( "noclip", Noclip_f );
            _commands.Add( "version", Version_f );
            _commands.Add( "say", Say_f );
            _commands.Add( "say_team", Say_Team_f );
            _commands.Add( "tell", Tell_f );
            _commands.Add( "color", Color_f );
            _commands.Add( "kill", Kill_f );
            _commands.Add( "pause", Pause_f );
            _commands.Add( "spawn", Spawn_f );
            _commands.Add( "begin", Begin_f );
            _commands.Add( "prespawn", PreSpawn_f );
            _commands.Add( "kick", Kick_f );
            _commands.Add( "ping", Ping_f );
            _commands.Add( "load", Loadgame_f );
            _commands.Add( "save", Savegame_f );
            _commands.Add( "give", Give_f );

            _commands.Add( "startdemos", Startdemos_f );
            _commands.Add( "demos", Demos_f );
            _commands.Add( "stopdemo", Stopdemo_f );

            _commands.Add( "viewmodel", Viewmodel_f );
            _commands.Add( "viewframe", Viewframe_f );
            _commands.Add( "viewnext", Viewnext_f );
            _commands.Add( "viewprev", Viewprev_f );

            _commands.Add( "mcache", _models.Print );

            // New
            _commands.Add( "showfps", ShowFPS_f );
        }

        public void ShowFPS_f( CommandMessage msg )
        {
            ShowFPS = !ShowFPS;
        }

        /// <summary>
        /// Host_Viewmodel_f
        /// </summary>
        /// <param name="msg"></param>
        private void Viewmodel_f( CommandMessage msg )
        {
            var e = FindViewthing( );
            if ( e == null )
                return;

            var m = _models.ForName( msg.Parameters[0], false, ModelType.Alias, false );
            if ( m == null )
            {
                _logger.Print( "Can't load {0}\n", msg.Parameters[0] );
                return;
            }

            e.v.frame = 0;
            _clientState.Data.model_precache[( Int32 ) e.v.modelindex] = m;
        }

        /// <summary>
        /// Host_Viewframe_f
        /// </summary>
        private void Viewframe_f( CommandMessage msg )
        {
            var e = FindViewthing( );
            if ( e == null )
                return;

            var m = _clientState.Data.model_precache[( Int32 ) e.v.modelindex];

            var f = MathLib.atoi( msg.Parameters[0] );
            if ( f >= m.FrameCount )
                f = m.FrameCount - 1;

            e.v.frame = f;
        }

        private void PrintFrameName( ModelData m, Int32 frame )
        {
            var hdr = _models.GetExtraData( m );
            if ( hdr == null )
                return;

            _logger.Print( "frame {0}: {1}\n", frame, hdr.frames[frame].name );
        }

        /// <summary>
        /// Host_Viewnext_f
        /// </summary>
        private void Viewnext_f( CommandMessage msg )
        {
            var e = FindViewthing( );
            if ( e == null )
                return;

            var m = _clientState.Data.model_precache[( Int32 ) e.v.modelindex];

            e.v.frame = e.v.frame + 1;
            if ( e.v.frame >= m.FrameCount )
                e.v.frame = m.FrameCount - 1;

            PrintFrameName( m, ( Int32 ) e.v.frame );
        }

        /// <summary>
        /// Host_Viewprev_f
        /// </summary>
        private void Viewprev_f( CommandMessage msg )
        {
            var e = FindViewthing( );
            if ( e == null )
                return;

            var m = _clientState.Data.model_precache[( Int32 ) e.v.modelindex];

            e.v.frame = e.v.frame - 1;
            if ( e.v.frame < 0 )
                e.v.frame = 0;

            PrintFrameName( m, ( Int32 ) e.v.frame );
        }

        /// <summary>
        /// Host_Status_f
        /// </summary>
        private void Status_f( CommandMessage msg )
        {
            var flag = true;
            if ( msg.Source == CommandSource.Command )
            {
                if ( !_serverState.Data.active )
                {
                    _client.ForwardToServer_f( msg );
                    return;
                }
            }
            else
                flag = false;

            var sb = new StringBuilder( 256 );
            sb.Append( String.Format( "host:    {0}\n", _cvars.Get( "hostname" ).Get<String>( ) ) );
            sb.Append( String.Format( "version: {0:F2}\n", QDef.VERSION ) );
            if ( _network.TcpIpAvailable )
            {
                sb.Append( "tcp/ip:  " );
                sb.Append( _network.MyTcpIpAddress );
                sb.Append( '\n' );
            }

            sb.Append( "map:     " );
            sb.Append( _serverState.Data.name );
            sb.Append( '\n' );
            sb.Append( String.Format( "players: {0} active ({1} max)\n\n", _network.ActiveConnections, _serverState.StaticData.maxclients ) );
            for ( var j = 0; j < _serverState.StaticData.maxclients; j++ )
            {
                var client = _serverState.StaticData.clients[j];

                if ( !client.active )
                    continue;

                var seconds = ( Int32 ) ( _network.Time - client.netconnection.connecttime );
                Int32 hours, minutes = seconds / 60;
                if ( minutes > 0 )
                {
                    seconds -= ( minutes * 60 );
                    hours = minutes / 60;
                    if ( hours > 0 )
                        minutes -= ( hours * 60 );
                }
                else
                    hours = 0;
                sb.Append( String.Format( "#{0,-2} {1,-16}  {2}  {2}:{4,2}:{5,2}",
                    j + 1, client.name, ( Int32 ) client.edict.v.frags, hours, minutes, seconds ) );
                sb.Append( "   " );
                sb.Append( client.netconnection.address );
                sb.Append( '\n' );
            }

            if ( flag )
                _logger.Print( sb.ToString( ) );
            else
                _server.ClientPrint( _localHost.HostClient, sb.ToString( ) );
        }

        /// <summary>
        /// Host_God_f
        /// Sets client to godmode
        /// </summary>
        private void God_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            if ( _programsState.GlobalStruct.deathmatch != 0 && msg.Client?.privileged == false )
                return;

            _serverState.Player.v.flags = ( Int32 ) _serverState.Player.v.flags ^ EdictFlags.FL_GODMODE;

            if ( ( ( Int32 ) _serverState.Player.v.flags & EdictFlags.FL_GODMODE ) == 0 )
                _server.ClientPrint( _localHost.HostClient, "godmode OFF\n" );
            else
                _server.ClientPrint( _localHost.HostClient, "godmode ON\n" );
        }

        /// <summary>
        /// Host_Notarget_f
        /// </summary>
        private void Notarget_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            if ( _programsState.GlobalStruct.deathmatch != 0 && msg.Client?.privileged == false )
                return;

            _serverState.Player.v.flags = ( Int32 ) _serverState.Player.v.flags ^ EdictFlags.FL_NOTARGET;

            if ( ( ( Int32 ) _serverState.Player.v.flags & EdictFlags.FL_NOTARGET ) == 0 )
                _server.ClientPrint( _localHost.HostClient, "notarget OFF\n" );
            else
                _server.ClientPrint( _localHost.HostClient, "notarget ON\n" );
        }

        /// <summary>
        /// Host_Noclip_f
        /// </summary>
        private void Noclip_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            if ( _programsState.GlobalStruct.deathmatch > 0 && msg.Client?.privileged == false )
                return;

            if ( _serverState.Player.v.movetype != Movetypes.MOVETYPE_NOCLIP )
            {
                _engine.NoClipAngleHack = true;
                _serverState.Player.v.movetype = Movetypes.MOVETYPE_NOCLIP;
                _server.ClientPrint( _localHost.HostClient, "noclip ON\n" );
            }
            else
            {
                _engine.NoClipAngleHack = false;
                _serverState.Player.v.movetype = Movetypes.MOVETYPE_WALK;
                _server.ClientPrint( _localHost.HostClient, "noclip OFF\n" );
            }
        }

        /// <summary>
        /// Host_Fly_f
        /// Sets client to flymode
        /// </summary>
        private void Fly_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            if ( _programsState.GlobalStruct.deathmatch > 0 && msg.Client?.privileged == false )
                return;

            if ( _serverState.Player.v.movetype != Movetypes.MOVETYPE_FLY )
            {
                _serverState.Player.v.movetype = Movetypes.MOVETYPE_FLY;
                _server.ClientPrint( _localHost.HostClient, "flymode ON\n" );
            }
            else
            {
                _serverState.Player.v.movetype = Movetypes.MOVETYPE_WALK;
                _server.ClientPrint( _localHost.HostClient, "flymode OFF\n" );
            }
        }

        /// <summary>
        /// Host_Ping_f
        /// </summary>
        private void Ping_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            _server.ClientPrint( _localHost.HostClient, "Client ping times:\n" );
            for ( var i = 0; i < _serverState.StaticData.maxclients; i++ )
            {
                var client = _serverState.StaticData.clients[i];

                if ( !client.active )
                    continue;

                Single total = 0;

                for ( var j = 0; j < ServerDef.NUM_PING_TIMES; j++ )
                    total += client.ping_times[j];

                total /= ServerDef.NUM_PING_TIMES;

                _server.ClientPrint( _localHost.HostClient, "{0,4} {1}\n", ( Int32 ) ( total * 1000 ), client.name );
            }
        }

        /// <summary>
        /// Host_Map_f
        ///
        /// handle a
        /// map [servername]
        /// command from the _logger.  Active clients are kicked off.
        /// </summary>
        /// <param name="msg"></param>
        private void Map_f( CommandMessage msg )
        {
            if ( msg.Source != CommandSource.Command )
                return;

            _clientState.StaticData.demonum = -1;		// stop demo loop in case this fails

            _client.Disconnect( );
            _engine.ShutdownServer( false );

            _keyboard.Destination = KeyDestination.key_game;			// remove console or menu
            _screen.BeginLoadingPlaque( );

            _clientState.StaticData.mapstring = msg.FullCommand + "\n";

            _serverState.StaticData.serverflags = 0;			// haven't completed an episode yet
            var name = msg.Parameters[0];
            _server.SpawnServer( name );

            if ( !_serverState.IsActive )
                return;

            if ( _clientState.StaticData.state != cactive_t.ca_dedicated )
            {
                _clientState.StaticData.spawnparms = msg.FullCommand;
                _commands.ExecuteString( "connect local", CommandSource.Command );
            }
        }

        /// <summary>
        /// Host_Changelevel_f
        /// Goes to a new map, taking all clients along
        /// </summary>
        private void Changelevel_f( CommandMessage msg )
        {
            if ( msg.Parameters == null || msg.Parameters.Length != 1 )
            {
                _logger.Print( "changelevel <levelname> : continue game on a new level\n" );
                return;
            }
            if ( !_serverState.Data.active || _clientState.StaticData.demoplayback )
            {
                _logger.Print( "Only the server may changelevel\n" );
                return;
            }
            _server.SaveSpawnparms( );
            var level = msg.Parameters[0];
            _server.SpawnServer( level );
        }

        // Host_Restart_f
        //
        // Restarts the current server for a dead player
        private void Restart_f( CommandMessage msg )
        {
            if ( _clientState.StaticData.demoplayback || !_serverState.IsActive )
                return;

            if ( msg.Source != CommandSource.Command )
                return;

            var mapname = _serverState.Data.name; // must copy out, because it gets cleared
                                                  // in sv_spawnserver
            _server.SpawnServer( mapname );
        }

        /// <summary>
        /// Host_Reconnect_f
        /// This command causes the client to wait for the signon messages again.
        /// This is sent just before a server changes levels
        /// </summary>
        private void Reconnect_f( CommandMessage msg )
        {
            _screen.BeginLoadingPlaque( );
            _clientState.StaticData.signon = 0;		// need new connection messages
        }

        /// <summary>
        /// Host_Connect_f
        /// User command to connect to server
        /// </summary>
        private void Connect_f( CommandMessage msg )
        {
            _clientState.StaticData.demonum = -1;		// stop demo loop in case this fails
            if ( _clientState.StaticData.demoplayback )
            {
                _client.StopPlayback( );
                _client.Disconnect( );
            }
            var name = msg.Parameters[0];
            _client.EstablishConnection( name );
            Reconnect_f( null );
        }

        /// <summary>
        /// Host_SavegameComment
        /// Writes a SAVEGAME_COMMENT_LENGTH character comment describing the current
        /// </summary>
        private String SavegameComment( )
        {
            var result = String.Format( "{0} kills:{1,3}/{2,3}", _clientState.Data.levelname,
                _clientState.Data.stats[QStatsDef.STAT_MONSTERS], _clientState.Data.stats[QStatsDef.STAT_TOTALMONSTERS] );

            // convert space to _ to make stdio happy
            result = result.Replace( ' ', '_' );

            if ( result.Length < QDef.SAVEGAME_COMMENT_LENGTH - 1 )
                result = result.PadRight( QDef.SAVEGAME_COMMENT_LENGTH - 1, '_' );

            if ( result.Length > QDef.SAVEGAME_COMMENT_LENGTH - 1 )
                result = result.Remove( QDef.SAVEGAME_COMMENT_LENGTH - 2 );

            return result + '\0';
        }

        /// <summary>
        /// Host_Savegame_f
        /// </summary>
        private void Savegame_f( CommandMessage msg )
        {
            if ( msg.Source != CommandSource.Command )
                return;

            if ( !_serverState.Data.active )
            {
                _logger.Print( "Not playing a local game.\n" );
                return;
            }

            if ( _clientState.Data.intermission != 0 )
            {
                _logger.Print( "Can't save in intermission.\n" );
                return;
            }

            if ( _serverState.StaticData.maxclients != 1 )
            {
                _logger.Print( "Can't save multiplayer games.\n" );
                return;
            }

            if ( msg.Parameters == null || msg.Parameters.Length != 1 )
            {
                _logger.Print( "save <savename> : save a game\n" );
                return;
            }

            if ( msg.Parameters[0].Contains( ".." ) )
            {
                _logger.Print( "Relative pathnames are not allowed.\n" );
                return;
            }

            for ( var i = 0; i < _serverState.StaticData.maxclients; i++ )
            {
                if ( _serverState.StaticData.clients[i].active && ( _serverState.StaticData.clients[i].edict.v.health <= 0 ) )
                {
                    _logger.Print( "Can't savegame with a dead player\n" );
                    return;
                }
            }

            var name = Path.ChangeExtension( Path.Combine( FileSystem.GameDir, msg.Parameters[0] ), ".sav" );

            _logger.Print( "Saving game to {0}...\n", name );
            var fs = FileSystem.OpenWrite( name, true );
            if ( fs == null )
            {
                _logger.Print( "ERROR: couldn't open.\n" );
                return;
            }
            using ( var writer = new StreamWriter( fs, Encoding.ASCII ) )
            {
                writer.WriteLine( HostDef.SAVEGAME_VERSION );
                writer.WriteLine( SavegameComment( ) );

                for ( var i = 0; i < ServerDef.NUM_SPAWN_PARMS; i++ )
                    writer.WriteLine( _serverState.StaticData.clients[0].spawn_parms[i].ToString( "F6",
                        CultureInfo.InvariantCulture.NumberFormat ) );

                writer.WriteLine( _serverState.CurrentSkill );
                writer.WriteLine( _serverState.Data.name );
                writer.WriteLine( _serverState.Data.time.ToString( "F6",
                    CultureInfo.InvariantCulture.NumberFormat ) );

                // write the light styles

                for ( var i = 0; i < QDef.MAX_LIGHTSTYLES; i++ )
                {
                    if ( !String.IsNullOrEmpty( _serverState.Data.lightstyles[i] ) )
                        writer.WriteLine( _serverState.Data.lightstyles[i] );
                    else
                        writer.WriteLine( "m" );
                }

                _programsEdict.WriteGlobals( writer );

                for ( var i = 0; i < _serverState.Data.num_edicts; i++ )
                {
                    _programsEdict.WriteEdict( writer, _serverState.EdictNum( i ) );
                    writer.Flush( );
                }
            }
            _logger.Print( "done.\n" );
        }

        /// <summary>
        /// Host_Loadgame_f
        /// </summary>
        private void Loadgame_f( CommandMessage msg )
        {
            if ( msg.Source != CommandSource.Command )
                return;

            if ( msg.Parameters == null || msg.Parameters.Length != 1 )
            {
                _logger.Print( "load <savename> : load a game\n" );
                return;
            }

            _clientState.StaticData.demonum = -1;		// stop demo loop in case this fails

            var name = Path.ChangeExtension( Path.Combine( FileSystem.GameDir, msg.Parameters[0] ), ".sav" );

            // we can't call SCR_BeginLoadingPlaque, because too much stack space has
            // been used.  The menu calls it before stuffing loadgame command
            //	SCR_BeginLoadingPlaque ();

            _logger.Print( "Loading game from {0}...\n", name );

            var fs = FileSystem.OpenRead( name );

            if ( fs == null )
            {
                _logger.Print( "ERROR: couldn't open.\n" );
                return;
            }

            using ( var reader = new StreamReader( fs, Encoding.ASCII ) )
            {
                var line = reader.ReadLine( );
                var version = MathLib.atoi( line );

                if ( version != HostDef.SAVEGAME_VERSION )
                {
                    _logger.Print( "Savegame is version {0}, not {1}\n", version, HostDef.SAVEGAME_VERSION );
                    return;
                }

                line = reader.ReadLine( );

                var spawn_parms = new Single[ServerDef.NUM_SPAWN_PARMS];

                for ( var i = 0; i < spawn_parms.Length; i++ )
                {
                    line = reader.ReadLine( );
                    spawn_parms[i] = MathLib.atof( line );
                }

                // this silliness is so we can load 1.06 save files, which have float skill values
                line = reader.ReadLine( );
                var tfloat = MathLib.atof( line );
                _serverState.CurrentSkill = ( Int32 ) ( tfloat + 0.1 );
                _cvars.Set( "skill", ( Single ) _serverState.CurrentSkill );

                var mapname = reader.ReadLine( );
                line = reader.ReadLine( );
                var time = MathLib.atof( line );

                _client.Disconnect_f( null );
                _server.SpawnServer( mapname );

                if ( !_serverState.Data.active )
                {
                    _logger.Print( "Couldn't load map\n" );
                    return;
                }
                _serverState.Data.paused = true;		// pause until all clients connect
                _serverState.Data.loadgame = true;

                // load the light styles

                for ( var i = 0; i < QDef.MAX_LIGHTSTYLES; i++ )
                {
                    line = reader.ReadLine( );
                    _serverState.Data.lightstyles[i] = line;
                }

                // load the edicts out of the savegame file
                var entnum = -1;		// -1 is the globals
                var sb = new StringBuilder( 32768 );

                while ( !reader.EndOfStream )
                {
                    line = reader.ReadLine( );
                    if ( line == null )
                        Utilities.Error( "EOF without closing brace" );

                    sb.AppendLine( line );
                    var idx = line.IndexOf( '}' );

                    if ( idx != -1 )
                    {
                        var length = 1 + sb.Length - ( line.Length - idx );
                        var data = Tokeniser.Parse( sb.ToString( 0, length ) );
                        if ( String.IsNullOrEmpty( Tokeniser.Token ) )
                            break; // end of file
                        if ( Tokeniser.Token != "{" )
                            Utilities.Error( "First token isn't a brace" );

                        if ( entnum == -1 )
                        {
                            // parse the global vars
                            _programsEdict.ParseGlobals( data );
                        }
                        else
                        {
                            // parse an edict
                            var ent = _serverState.EdictNum( entnum );
                            ent.Clear( );
                            _programsEdict.ParseEdict( data, ent );

                            // link it into the bsp tree
                            if ( !ent.free )
                                _serverWorld.LinkEdict( ent, false );
                        }

                        entnum++;
                        sb.Remove( 0, length );
                    }
                }

                _serverState.Data.num_edicts = entnum;
                _serverState.Data.time = time;

                for ( var i = 0; i < ServerDef.NUM_SPAWN_PARMS; i++ )
                    _serverState.StaticData.clients[0].spawn_parms[i] = spawn_parms[i];
            }

            if ( _clientState.StaticData.state != cactive_t.ca_dedicated )
            {
                _client.EstablishConnection( "local" );
                Reconnect_f( null );
            }
        }

        /// <summary>
        /// Host_Name_f
        /// </summary>
        /// <param name="msg"></param>
        private void Name_f( CommandMessage msg )
        {
            if ( msg.Parameters == null || msg.Parameters.Length <= 0 )
            {
                _logger.Print( "\"name\" is \"{0}\"\n", _client.Name );
                return;
            }

            String newName;
            if ( msg.Parameters.Length == 1 )
                newName = msg.Parameters[0];
            else
                newName = msg.StringParameters;

            if ( newName.Length > 16 )
                newName = newName.Remove( 15 );

            if ( msg.Source == CommandSource.Command )
            {
                if ( _client.Name == newName )
                    return;

                _cvars.Set( "_cl_name", newName );

                if ( _clientState.StaticData.state == cactive_t.ca_connected )
                    _client.ForwardToServer_f( msg );
                return;
            }

            if ( !String.IsNullOrEmpty( _localHost.HostClient.name ) && _localHost.HostClient.name != "unconnected" )
            {
                if ( _localHost.HostClient.name != newName )
                    _logger.Print( "{0} renamed to {1}\n", _localHost.HostClient.name, newName );
            }

            _localHost.HostClient.name = newName;
            _localHost.HostClient.edict.v.netname = _programsState.NewString( newName );

            // send notification to all clients
            var m = _serverState.Data.reliable_datagram;
            m.WriteByte( ProtocolDef.svc_updatename );
            m.WriteByte( _localHost.ClientNum );
            m.WriteString( newName );
        }

        /// <summary>
        /// Host_Version_f
        /// </summary>
        /// <param name="msg"></param>
        private void Version_f( CommandMessage msg )
        {
            _logger.Print( "Version {0}\n", QDef.VERSION );
            _logger.Print( "Exe hash code: {0}\n", System.Reflection.Assembly.GetExecutingAssembly( ).GetHashCode( ) );
        }

        /// <summary>
        /// Host_Say
        /// </summary>
        private void Say( CommandMessage msg, Boolean teamonly )
        {
            var fromServer = false;
            if ( msg.Source == CommandSource.Command )
            {
                if ( _clientState.StaticData.state == cactive_t.ca_dedicated )
                {
                    fromServer = true;
                    teamonly = false;
                }
                else
                {
                    _client.ForwardToServer_f( msg );
                    return;
                }
            }

            if ( msg.Parameters == null || msg.Parameters.Length < 1 )
                return;

            var p = msg.StringParameters;

            // remove quotes if present
            if ( p.StartsWith( "\"" ) )
                p = p.Substring( 1, p.Length - 2 );

            // turn on color set 1
            String text;
            if ( !fromServer )
                text = ( Char ) 1 + _localHost.HostClient.name + ": ";
            else
                text = ( Char ) 1 + "<" + _network.HostName + "> ";

            text += p + "\n";

            for ( var j = 0; j < _serverState.StaticData.maxclients; j++ )
            {
                var client = _serverState.StaticData.clients[j];

                if ( client == null || !client.active || !client.spawned )
                    continue;

                if ( Cvars.TeamPlay.Get<Int32>( ) != 0 && teamonly && client.edict.v.team != _localHost.HostClient.edict.v.team )
                    continue;

                _server.ClientPrint( client, text );
            }
        }

        /// <summary>
        /// Host_Say_f
        /// </summary>
        /// <param name="msg"></param>
        private void Say_f( CommandMessage msg )
        {
            Say( msg, false );
        }

        /// <summary>
        /// Host_Say_Team_f
        /// </summary>
        /// <param name="msg"></param>
        private void Say_Team_f( CommandMessage msg )
        {
            Say( msg, true );
        }

        /// <summary>
        /// Host_Tell_f
        /// </summary>
        /// <param name="msg"></param>
        private void Tell_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            if ( msg.Parameters == null || msg.Parameters.Length < 2 )
                return;

            var text = _localHost.HostClient.name + ": ";
            var p = msg.StringParameters;

            // remove quotes if present
            if ( p.StartsWith( "\"" ) )
            {
                p = p.Substring( 1, p.Length - 2 );
            }

            text += p + "\n";

            for ( var j = 0; j < _serverState.StaticData.maxclients; j++ )
            {
                var client = _serverState.StaticData.clients[j];

                if ( !client.active || !client.spawned )
                    continue;

                if ( client.name == msg.Parameters[0] )
                    continue;

                _server.ClientPrint( client, text );
                break;
            }
        }

        /// <summary>
        /// Host_Color_f
        /// </summary>
        /// <param name="msg"></param>
        private void Color_f( CommandMessage msg )
        {
            if ( msg.Parameters == null || msg.Parameters.Length <= 0 )
            {
                _logger.Print( "\"color\" is \"{0} {1}\"\n", ( ( Int32 ) _client.Color ) >> 4, ( ( Int32 ) _client.Color ) & 0x0f );
                _logger.Print( "color <0-13> [0-13]\n" );
                return;
            }

            Int32 top, bottom;
            if ( msg.Parameters?.Length == 1 )
                top = bottom = MathLib.atoi( msg.Parameters[0] );
            else
            {
                top = MathLib.atoi( msg.Parameters[0] );
                bottom = MathLib.atoi( msg.Parameters[1] );
            }

            top &= 15;

            if ( top > 13 )
                top = 13;

            bottom &= 15;

            if ( bottom > 13 )
                bottom = 13;

            var playercolor = top * 16 + bottom;

            if ( msg.Source == CommandSource.Command )
            {
                _cvars.Set( "_cl_color", playercolor );

                if ( _clientState.StaticData.state == cactive_t.ca_connected )
                    _client.ForwardToServer_f( msg );
                return;
            }

            _localHost.HostClient.colors = playercolor;
            _localHost.HostClient.edict.v.team = bottom + 1;

            // send notification to all clients
            var m = _serverState.Data.reliable_datagram;
            m.WriteByte( ProtocolDef.svc_updatecolors );
            m.WriteByte( _localHost.ClientNum );
            m.WriteByte( _localHost.HostClient.colors );
        }

        /// <summary>
        /// Host_Kill_f
        /// </summary>
        private void Kill_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            if ( _serverState.Player.v.health <= 0 )
            {
                _server.ClientPrint( _localHost.HostClient, "Can't suicide -- allready dead!\n" );
                return;
            }

            _programsState.GlobalStruct.time = ( Single ) _serverState.Data.time;
            _programsState.GlobalStruct.self = _serverState.EdictToProg( _serverState.Player );
            _programsExec.Execute( _programsState.GlobalStruct.ClientKill );
        }

        /// <summary>
        /// Host_Pause_f
        /// </summary>
        private void Pause_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            if ( !Cvars.Pausable.Get<Boolean>( ) )
            {
                _server.ClientPrint( _localHost.HostClient, "Pause not allowed.\n" );
            }
            else
            {
                _serverState.Data.paused = !_serverState.Data.paused;

                if ( _serverState.Data.paused )
                    _server.BroadcastPrint( "{0} paused the game\n", _programsState.GetString( _serverState.Player.v.netname ) );
                else
                    _server.BroadcastPrint( "{0} unpaused the game\n", _programsState.GetString( _serverState.Player.v.netname ) );

                // send notification to all clients
                _serverState.Data.reliable_datagram.WriteByte( ProtocolDef.svc_setpause );
                _serverState.Data.reliable_datagram.WriteByte( _serverState.Data.paused ? 1 : 0 );
            }
        }

        /// <summary>
        /// Host_PreSpawn_f
        /// </summary>
        private void PreSpawn_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _logger.Print( "prespawn is not valid from the console\n" );
                return;
            }

            if ( _localHost.HostClient.spawned )
            {
                _logger.Print( "prespawn not valid -- allready spawned\n" );
                return;
            }

            var m = _localHost.HostClient.message;
            m.Write( _serverState.Data.signon.Data, 0, _serverState.Data.signon.Length );
            m.WriteByte( ProtocolDef.svc_signonnum );
            m.WriteByte( 2 );
            _localHost.HostClient.sendsignon = true;
        }

        /// <summary>
        /// Host_Spawn_f
        /// </summary>
        private void Spawn_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _logger.Print( "spawn is not valid from the console\n" );
                return;
            }

            if ( _localHost.HostClient.spawned )
            {
                _logger.Print( "Spawn not valid -- allready spawned\n" );
                return;
            }

            MemoryEdict ent;

            // run the entrance script
            if ( _serverState.Data.loadgame )
            {
                // loaded games are fully inited allready
                // if this is the last client to be connected, unpause
                _serverState.Data.paused = false;
            }
            else
            {
                // set up the edict
                ent = _localHost.HostClient.edict;

                ent.Clear( ); //memset(&ent.v, 0, Programs.entityfields * 4);
                ent.v.colormap = _serverState.NumForEdict( ent );
                ent.v.team = ( _localHost.HostClient.colors & 15 ) + 1;
                ent.v.netname = _programsState.NewString( _localHost.HostClient.name );

                // copy spawn parms out of the client_t
                _programsState.GlobalStruct.SetParams( _localHost.HostClient.spawn_parms );

                // call the spawn function

                _programsState.GlobalStruct.time = ( Single ) _serverState.Data.time;
                _programsState.GlobalStruct.self = _serverState.EdictToProg( _serverState.Player );
                _programsExec.Execute( _programsState.GlobalStruct.ClientConnect );

                if ( ( Timer.GetFloatTime( ) - _localHost.HostClient.netconnection.connecttime ) <= _serverState.Data.time )
                {
                    _logger.DPrint( "^0{0}^9 entered the game\n", _localHost.HostClient.name );
                }

                _programsExec.Execute( _programsState.GlobalStruct.PutClientInServer );
            }

            // send all current names, colors, and frag counts
            var m = _localHost.HostClient.message;
            m.Clear( );

            // send time of update
            m.WriteByte( ProtocolDef.svc_time );
            m.WriteFloat( ( Single ) _serverState.Data.time );

            for ( var i = 0; i < _serverState.StaticData.maxclients; i++ )
            {
                var client = _serverState.StaticData.clients[i];
                m.WriteByte( ProtocolDef.svc_updatename );
                m.WriteByte( i );
                m.WriteString( client.name );
                m.WriteByte( ProtocolDef.svc_updatefrags );
                m.WriteByte( i );
                m.WriteShort( client.old_frags );
                m.WriteByte( ProtocolDef.svc_updatecolors );
                m.WriteByte( i );
                m.WriteByte( client.colors );
            }

            // send all current light styles
            for ( var i = 0; i < QDef.MAX_LIGHTSTYLES; i++ )
            {
                m.WriteByte( ProtocolDef.svc_lightstyle );
                m.WriteByte( ( Char ) i );
                m.WriteString( _serverState.Data.lightstyles[i] );
            }

            //
            // send some stats
            //
            m.WriteByte( ProtocolDef.svc_updatestat );
            m.WriteByte( QStatsDef.STAT_TOTALSECRETS );
            m.WriteLong( ( Int32 ) _programsState.GlobalStruct.total_secrets );

            m.WriteByte( ProtocolDef.svc_updatestat );
            m.WriteByte( QStatsDef.STAT_TOTALMONSTERS );
            m.WriteLong( ( Int32 ) _programsState.GlobalStruct.total_monsters );

            m.WriteByte( ProtocolDef.svc_updatestat );
            m.WriteByte( QStatsDef.STAT_SECRETS );
            m.WriteLong( ( Int32 ) _programsState.GlobalStruct.found_secrets );

            m.WriteByte( ProtocolDef.svc_updatestat );
            m.WriteByte( QStatsDef.STAT_MONSTERS );
            m.WriteLong( ( Int32 ) _programsState.GlobalStruct.killed_monsters );

            //
            // send a fixangle
            // Never send a roll angle, because savegames can catch the server
            // in a state where it is expecting the client to correct the angle
            // and it won't happen if the game was just loaded, so you wind up
            // with a permanent head tilt
            ent = _serverState.EdictNum( 1 + _localHost.ClientNum );
            m.WriteByte( ProtocolDef.svc_setangle );
            m.WriteAngle( ent.v.angles.x );
            m.WriteAngle( ent.v.angles.y );
            m.WriteAngle( 0 );

            _server.WriteClientDataToMessage( _serverState.Player, _localHost.HostClient.message );

            m.WriteByte( ProtocolDef.svc_signonnum );
            m.WriteByte( 3 );
            _localHost.HostClient.sendsignon = true;
        }

        /// <summary>
        /// Host_Begin_f
        /// </summary>
        /// <param name="msg"></param>
        private void Begin_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _logger.Print( "begin is not valid from the console\n" );
                return;
            }

            _localHost.HostClient.spawned = true;
        }

        /// <summary>
        /// Host_Kick_f
        /// Kicks a user off of the server
        /// </summary>
        private void Kick_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                if ( !_serverState.Data.active )
                {
                    _client.ForwardToServer_f( msg );
                    return;
                }
            }
            else if ( _programsState.GlobalStruct.deathmatch != 0 && msg.Client?.privileged == false )
                return;

            client_t clientToKick = null;

            var byNumber = false;
            Int32 i;
            if ( msg.Parameters?.Length > 1 && msg.Parameters[0] == "#" )
            {
                i = ( Int32 ) MathLib.atof( msg.Parameters[1] ) - 1;

                if ( i < 0 || i >= _serverState.StaticData.maxclients )
                    return;

                if ( !_serverState.StaticData.clients[i].active )
                    return;

                clientToKick = _serverState.StaticData.clients[i];
                byNumber = true;
            }
            else
            {
                for ( i = 0; i < _serverState.StaticData.maxclients; i++ )
                {
                    var client = _serverState.StaticData.clients[i];

                    if ( !client.active )
                        continue;

                    if ( Utilities.SameText( client.name, msg.Parameters[0] ) )
                    {
                        clientToKick = client;
                        break;
                    }
                }
            }

            if ( i < _serverState.StaticData.maxclients )
            {
                String who;
                if ( msg.Source == CommandSource.Command )
                    if ( _clientState.StaticData.state == cactive_t.ca_dedicated )
                        who = "Console";
                    else
                        who = _client.Name;
                else
                    who = _localHost.HostClient.name;

                // can't kick yourself!
                if ( _localHost.HostClient == clientToKick )
                    return;

                String message = null;
                if ( msg.Parameters?.Length > 1 )
                {
                    message = Tokeniser.Parse( msg.StringParameters );
                    if ( byNumber )
                    {
                        message = message.Substring( 1 ); // skip the #
                        message = message.Trim( ); // skip white space
                        message = message.Substring( msg.Parameters[1].Length );	// skip the number
                    }
                    message = message.Trim( );
                }
                if ( !String.IsNullOrEmpty( message ) )
                    _server.ClientPrint( clientToKick, "Kicked by {0}: {1}\n", who, message );
                else
                    _server.ClientPrint( clientToKick, "Kicked by {0}\n", who );
                _server.DropClient( clientToKick, false );
            }
        }

        /// <summary>
        /// Host_Give_f
        /// </summary>
        private void Give_f( CommandMessage msg )
        {
            if ( msg.Source == CommandSource.Command )
            {
                _client.ForwardToServer_f( msg );
                return;
            }

            if ( _programsState.GlobalStruct.deathmatch != 0 && msg.Client?.privileged == false )
                return;

            var t = msg.Parameters[0];
            var v = MathLib.atoi( msg.Parameters[1] );

            if ( String.IsNullOrEmpty( t ) )
                return;

            switch ( t[0] )
            {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    // CHANGE
                    // MED 01/04/97 added hipnotic give stuff
                    if ( Engine.Common.GameKind == GameKind.Hipnotic )
                    {
                        if ( t[0] == '6' )
                        {
                            if ( t[1] == 'a' )
                                _serverState.Player.v.items = ( Int32 ) _serverState.Player.v.items | QItemsDef.HIT_PROXIMITY_GUN;
                            else
                                _serverState.Player.v.items = ( Int32 ) _serverState.Player.v.items | QItemsDef.IT_GRENADE_LAUNCHER;
                        }
                        else if ( t[0] == '9' )
                            _serverState.Player.v.items = ( Int32 ) _serverState.Player.v.items | QItemsDef.HIT_LASER_CANNON;
                        else if ( t[0] == '0' )
                            _serverState.Player.v.items = ( Int32 ) _serverState.Player.v.items | QItemsDef.HIT_MJOLNIR;
                        else if ( t[0] >= '2' )
                            _serverState.Player.v.items = ( Int32 ) _serverState.Player.v.items | ( QItemsDef.IT_SHOTGUN << ( t[0] - '2' ) );
                    }
                    else
                    {
                        if ( t[0] >= '2' )
                            _serverState.Player.v.items = ( Int32 ) _serverState.Player.v.items | ( QItemsDef.IT_SHOTGUN << ( t[0] - '2' ) );
                    }
                    break;

                case 's':
                    if ( Engine.Common.GameKind == GameKind.Rogue )
                        _programsEdict.SetEdictFieldFloat( _serverState.Player, "ammo_shells1", v );

                    _serverState.Player.v.ammo_shells = v;
                    break;

                case 'n':
                    if ( Engine.Common.GameKind == GameKind.Rogue )
                    {
                        if ( _programsEdict.SetEdictFieldFloat( _serverState.Player, "ammo_nails1", v ) )
                        {
                            if ( _serverState.Player.v.weapon <= QItemsDef.IT_LIGHTNING )
                                _serverState.Player.v.ammo_nails = v;
                        }
                    }
                    else
                    {
                        _serverState.Player.v.ammo_nails = v;
                    }
                    break;

                case 'l':
                    if ( Engine.Common.GameKind == GameKind.Rogue )
                    {
                        if ( _programsEdict.SetEdictFieldFloat( _serverState.Player, "ammo_lava_nails", v ) )
                        {
                            if ( _serverState.Player.v.weapon > QItemsDef.IT_LIGHTNING )
                                _serverState.Player.v.ammo_nails = v;
                        }
                    }
                    break;

                case 'r':
                    if ( Engine.Common.GameKind == GameKind.Rogue )
                    {
                        if ( _programsEdict.SetEdictFieldFloat( _serverState.Player, "ammo_rockets1", v ) )
                        {
                            if ( _serverState.Player.v.weapon <= QItemsDef.IT_LIGHTNING )
                                _serverState.Player.v.ammo_rockets = v;
                        }
                    }
                    else
                    {
                        _serverState.Player.v.ammo_rockets = v;
                    }
                    break;

                case 'm':
                    if ( Engine.Common.GameKind == GameKind.Rogue )
                    {
                        if ( _programsEdict.SetEdictFieldFloat( _serverState.Player, "ammo_multi_rockets", v ) )
                        {
                            if ( _serverState.Player.v.weapon > QItemsDef.IT_LIGHTNING )
                                _serverState.Player.v.ammo_rockets = v;
                        }
                    }
                    break;

                case 'h':
                    _serverState.Player.v.health = v;
                    break;

                case 'c':
                    if ( Engine.Common.GameKind == GameKind.Rogue )
                    {
                        if ( _programsEdict.SetEdictFieldFloat( _serverState.Player, "ammo_cells1", v ) )
                        {
                            if ( _serverState.Player.v.weapon <= QItemsDef.IT_LIGHTNING )
                                _serverState.Player.v.ammo_cells = v;
                        }
                    }
                    else
                    {
                        _serverState.Player.v.ammo_cells = v;
                    }
                    break;

                case 'p':
                    if ( Engine.Common.GameKind == GameKind.Rogue )
                    {
                        if ( _programsEdict.SetEdictFieldFloat( _serverState.Player, "ammo_plasma", v ) )
                        {
                            if ( _serverState.Player.v.weapon > QItemsDef.IT_LIGHTNING )
                                _serverState.Player.v.ammo_cells = v;
                        }
                    }
                    break;
            }
        }

        private MemoryEdict FindViewthing( )
        {
            for ( var i = 0; i < _serverState.Data.num_edicts; i++ )
            {
                var e = _serverState.EdictNum( i );

                if ( _programsState.GetString( e.v.classname ) == "viewthing" )
                    return e;
            }

            _logger.Print( "No viewthing on map\n" );
            return null;
        }

        /// <summary>
        /// Host_Startdemos_f
        /// </summary>
        /// <param name="msg"></param>
        private void Startdemos_f( CommandMessage msg )
        {
            if ( _clientState.StaticData.state == cactive_t.ca_dedicated )
            {
                if ( !_serverState.Data.active )
                    _commands.Buffer.Append( "map start\n" );
                return;
            }

            var c = msg.Parameters.Length;
            if ( c > ClientDef.MAX_DEMOS )
            {
                _logger.Print( "Max {0} demos in demoloop\n", ClientDef.MAX_DEMOS );
                c = ClientDef.MAX_DEMOS;
            }

            _logger.Print( "{0} demo(s) in loop\n", c );

            for ( var i = 0; i < c; i++ )
                _clientState.StaticData.demos[i] = Utilities.Copy( msg.Parameters[i], ClientDef.MAX_DEMONAME );

            if ( !_serverState.Data.active && _clientState.StaticData.demonum != -1 && !_clientState.StaticData.demoplayback )
            {
                _clientState.StaticData.demonum = 0;
                _client.NextDemo( );
            }
            else
                _clientState.StaticData.demonum = -1;
        }

        /// <summary>
        /// Host_Demos_f
        /// Return to looping demos
        /// </summary>
        private void Demos_f( CommandMessage msg )
        {
            if ( _clientState.StaticData.state == cactive_t.ca_dedicated )
                return;

            if ( _clientState.StaticData.demonum == -1 )
                _clientState.StaticData.demonum = 1;

            _client.Disconnect_f( null );
            _client.NextDemo( );
        }

        /// <summary>
        /// Host_Stopdemo_f
        /// Return to looping demos
        /// </summary>
        private void Stopdemo_f( CommandMessage msg )
        {
            if ( _clientState.StaticData.state == cactive_t.ca_dedicated || !_clientState.StaticData.demoplayback )
                return;

            _client.StopPlayback( );
            _client.Disconnect( );
        }
    }
}
