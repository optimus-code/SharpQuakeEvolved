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
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.IO.BSP;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Framework.Logging;
using SharpQuake.Game.Client;
using SharpQuake.Game.Data.Models;
using SharpQuake.Game.Networking.Server;
using SharpQuake.Game.Rendering.Memory;
using SharpQuake.Game.Rendering;
using SharpQuake.Networking.Server;
using SharpQuake.Sys;
using SharpQuake.Framework.Mathematics;
using SharpQuake.Sys.Programs;
using SharpQuake.Factories.Rendering;
using SharpQuake.Sys.Handlers;
using SharpQuake.Networking.Client;

namespace SharpQuake
{
    public partial class Server
    {
        public Single Gravity
        {
            get
            {
                return Cvars.Gravity.Get<Single>( );
            }
        }

        public Single Aim
        {
            get
            {
                return Cvars.Aim.Get<Single>( );
            }
        }

        private String[] _LocalModels = new String[QDef.MAX_MODELS]; //[MAX_MODELS][5];	// inline model names for precache

        private Int32 _FatBytes; // fatbytes
        private Byte[] _FatPvs = new Byte[BspDef.MAX_MAP_LEAFS / 8]; // fatpvs

        private readonly IConsoleLogger _logger;
        private readonly ICache _cache;
        private readonly IKeyboardInput _keyboard;
        private readonly ClientVariableFactory _cvars;
        private readonly CommandFactory _commands;
        private readonly ModelFactory _models;
        private readonly snd _sound;
        private readonly Network _network;
        private readonly ServerState _state;
        private readonly ProgramsState _programsState;
        private readonly ProgramsExec _programsExec;
        private readonly ProgramsEdict _programsEdict;
        private readonly ServerUser _serverUser;
        private readonly ServerPhysics _serverPhysics;
        private readonly ServerWorld _serverWorld;
        private readonly Scr _screen;
        private readonly MemoryHandler _memoryHandler;
        private readonly ClientState _clientState;

        public Server( IConsoleLogger logger, ClientVariableFactory cvars, CommandFactory commands,
            ModelFactory models, ICache cache, IKeyboardInput keyboard, snd sound, Network network, ServerState state,
            ProgramsState programsState, ProgramsExec programsExec, ProgramsEdict programsEdict,
            ServerUser serverUser, ServerPhysics serverPhysics, ServerWorld serverWorld, Scr screen, 
            MemoryHandler memoryHandler, ClientState clientState )
        {
            _logger = logger;
            _cache = cache;
            _keyboard = keyboard;
            _cvars = cvars;
            _commands = commands;
            _models = models;
            _sound = sound;
            _network = network;
            _state = state;
            _programsState = programsState;
            _programsExec = programsExec;
            _programsEdict = programsEdict;
            _serverUser = serverUser;
            _serverPhysics = serverPhysics;
            _serverWorld = serverWorld;
            _screen = screen;
            _memoryHandler = memoryHandler;
            _clientState = clientState;
        }

        // SV_Init
        public void Initialise( )
        {
            if ( Cvars.Friction == null )
            {
                Cvars.Friction = _cvars.Add( "sv_friction", 4f, ClientVariableFlags.Server );
                Cvars.EdgeFriction = _cvars.Add( "edgefriction", 2f );
                Cvars.StopSpeed = _cvars.Add( "sv_stopspeed", 100f );
                Cvars.Gravity = _cvars.Add( "sv_gravity", 800f, ClientVariableFlags.Server );
                Cvars.MaxVelocity = _cvars.Add( "sv_maxvelocity", 2000f );
                Cvars.NoStep = _cvars.Add( "sv_nostep", false );
                Cvars.MaxSpeed = _cvars.Add( "sv_maxspeed", 320f, ClientVariableFlags.Server );
                Cvars.Accelerate = _cvars.Add( "sv_accelerate", 10f );
                Cvars.Aim = _cvars.Add( "sv_aim", 0.93f );
                Cvars.IdealPitchScale = _cvars.Add( "sv_idealpitchscale", 0.8f );
            }

            for ( var i = 0; i < QDef.MAX_MODELS; i++ )
                _LocalModels[i] = "*" + i.ToString( );
        }

        /// <summary>
        /// SV_StartParticle
        /// Make sure the event gets sent to all clients
        /// </summary>
        public void StartParticle( ref Vector3 org, ref Vector3 dir, Int32 color, Int32 count )
        {
            if ( _state.Data.datagram.Length > QDef.MAX_DATAGRAM - 16 )
                return;

            _state.Data.datagram.WriteByte( ProtocolDef.svc_particle );
            _state.Data.datagram.WriteCoord( org.X );
            _state.Data.datagram.WriteCoord( org.Y );
            _state.Data.datagram.WriteCoord( org.Z );

            var max = Vector3.One * 127;
            var min = Vector3.One * -128;
            var v = Vector3.Clamp( dir * 16, min, max );
            _state.Data.datagram.WriteChar( ( Int32 ) v.X );
            _state.Data.datagram.WriteChar( ( Int32 ) v.Y );
            _state.Data.datagram.WriteChar( ( Int32 ) v.Z );
            _state.Data.datagram.WriteByte( count );
            _state.Data.datagram.WriteByte( color );
        }


        /// <summary>
        /// SV_DropClient
        /// Called when the player is getting totally kicked off the host
        /// if (crash = true), don't bother sending signofs
        /// </summary>
        public void DropClient( client_t client, Boolean crash )
        {
            if ( !crash )
            {
                // send any final messages (don't check for errors)
                if ( _network.CanSendMessage( client.netconnection ) )
                {
                    var msg = client.message;
                    msg.WriteByte( ProtocolDef.svc_disconnect );
                    _network.SendMessage( client.netconnection, msg );
                }

                if ( client.edict != null && client.spawned )
                {
                    // call the prog function for removing a client
                    // this will set the body to a dead frame, among other things
                    var saveSelf = _programsState.GlobalStruct.self;
                    _programsState.GlobalStruct.self = _state.EdictToProg( client.edict );
                    _programsExec.Execute( _programsState.GlobalStruct.ClientDisconnect );
                    _programsState.GlobalStruct.self = saveSelf;
                }

                _logger.DPrint( "Client {0} removed\n", client.name );
            }

            // break the net connection
            _network.Close( client.netconnection );
            client.netconnection = null;

            // free the client (the body stays around)
            client.active = false;
            client.name = null;
            client.old_frags = -999999;
            _network.ActiveConnections--;

            var clientIndex = Array.IndexOf( _state.StaticData.clients, client );

            // send notification to all clients
            for ( var i = 0; i < _state.StaticData.maxclients; i++ )
            {
                var cl = _state.StaticData.clients[i];

                if ( !cl.active )
                    continue;

                cl.message.WriteByte( ProtocolDef.svc_updatename );
                cl.message.WriteByte( clientIndex );
                cl.message.WriteString( "" );
                cl.message.WriteByte( ProtocolDef.svc_updatefrags );
                cl.message.WriteByte( clientIndex );
                cl.message.WriteShort( 0 );
                cl.message.WriteByte( ProtocolDef.svc_updatecolors );
                cl.message.WriteByte( clientIndex );
                cl.message.WriteByte( 0 );
            }
        }

        /// <summary>
        /// SV_SendClientMessages
        /// </summary>
        private void SendClientMessages( )
        {
            // update frags, names, etc
            UpdateToReliableMessages( );

            // build individual updates
            for ( var i = 0; i < _state.StaticData.maxclients; i++ )
            {
                var client = _state.StaticData.clients[i];

                if ( !client.active )
                    continue;

                if ( client.spawned )
                {
                    if ( !SendClientDatagram( client ) )
                        continue;
                }
                else
                {
                    // the player isn't totally in the game yet
                    // send small keepalive messages if too much time has passed
                    // send a full message when the next signon stage has been requested
                    // some other message data (name changes, etc) may accumulate
                    // between signon stages
                    if ( !client.sendsignon )
                    {
                        if ( Time.Absolute - client.last_message > 5 )
                            SendNop( client );
                        continue;   // don't send out non-signon messages
                    }
                }

                // check for an overflowed message.  Should only happen
                // on a very fucked up connection that backs up a lot, then
                // changes level
                if ( client.message.IsOveflowed )
                {
                    DropClient( client, true );
                    client.message.IsOveflowed = false;
                    continue;
                }

                if ( client.message.Length > 0 || client.dropasap )
                {
                    if ( !_network.CanSendMessage( client.netconnection ) )
                        continue;

                    if ( client.dropasap )
                        DropClient( client, false );    // went to another level
                    else
                    {
                        if ( _network.SendMessage( client.netconnection, client.message ) == -1 )
                            DropClient( client, true ); // if the message couldn't send, kick off
                        client.message.Clear( );
                        client.last_message = Time.Absolute;
                        client.sendsignon = false;
                    }
                }
            }

            // clear muzzle flashes
            CleanupEnts( );
        }

        /// <summary>
        /// The start of server frame
        /// </summary>
        public void Frame( )
        {
            // set the time and clear the general datagram
            ClearDatagram( );

            // check for new clients
            CheckForNewClients( );

            // read client messages
            _serverUser.RunClients( ( client ) => DropClient( client, false ) );

            // move things around and think
            // always pause in single player if in console or menus
            if ( !_state.Data.paused && ( _state.StaticData.maxclients > 1 || _keyboard.Destination == KeyDestination.key_game ) )
                _serverPhysics.Physics( );

            // send all messages to the clients
            SendClientMessages( );
        }

        /// <summary>
        /// SV_ClearDatagram
        /// </summary>
        private void ClearDatagram( )
        {
            _state.Data.datagram.Clear( );
        }

        /// <summary>
        /// SV_ModelIndex
        /// </summary>
        public Int32 ModelIndex( String name )
        {
            if ( String.IsNullOrEmpty( name ) )
                return 0;

            Int32 i;
            for ( i = 0; i < QDef.MAX_MODELS && _state.Data.model_precache[i] != null; i++ )
                if ( _state.Data.model_precache[i] == name )
                    return i;

            if ( i == QDef.MAX_MODELS || String.IsNullOrEmpty( _state.Data.model_precache[i] ) )
                Utilities.Error( "SV_ModelIndex: model {0} not precached", name );
            return i;
        }

        /// <summary>
        /// SV_ClientPrintf
        /// Sends text across to be displayed
        /// FIXME: make this just a stuffed echo?
        /// </summary>
        [Obsolete( "This is not obsolete but it appears to be used for local and remote print for clients, parts of code mistakenly assume it's local context only?")]
        public void ClientPrint( client_t client, String fmt, params Object[] args )
        {
            //_logger.Print( String.Format( fmt, args ) );
            //var client = Host.HostClient;
            var tmp = String.Format( fmt, args );
            client.message.WriteByte( ProtocolDef.svc_print );
            client.message.WriteString( tmp );
        }

        /// <summary>
        /// host_ClientCommands
        /// Send text over to the client to be executed
        /// </summary>
        public void ClientCommands( client_t client, String fmt, params Object[] args )
        {
            var tmp = String.Format( fmt, args );
            client.message.WriteByte( ProtocolDef.svc_stufftext );
            client.message.WriteString( tmp );
        }

        /// <summary>
        /// SV_BroadcastPrint
        /// </summary>
        public void BroadcastPrint( String fmt, params Object[] args )
        {
            var tmp = args.Length > 0 ? String.Format( fmt, args ) : fmt;
            for ( var i = 0; i < _state.StaticData.maxclients; i++ )
                if ( _state.StaticData.clients[i].active && _state.StaticData.clients[i].spawned )
                {
                    var msg = _state.StaticData.clients[i].message;
                    msg.WriteByte( ProtocolDef.svc_print );
                    msg.WriteString( tmp );
                }
        }

        private void WriteClientDamageMessage( MemoryEdict ent, MessageWriter msg )
        {
            if ( ent.v.dmg_take != 0 || ent.v.dmg_save != 0 )
            {
                var other = _state.ProgToEdict( ent.v.dmg_inflictor );
                msg.WriteByte( ProtocolDef.svc_damage );
                msg.WriteByte( ( Int32 ) ent.v.dmg_save );
                msg.WriteByte( ( Int32 ) ent.v.dmg_take );
                msg.WriteCoord( other.v.origin.x + 0.5f * ( other.v.mins.x + other.v.maxs.x ) );
                msg.WriteCoord( other.v.origin.y + 0.5f * ( other.v.mins.y + other.v.maxs.y ) );
                msg.WriteCoord( other.v.origin.z + 0.5f * ( other.v.mins.z + other.v.maxs.z ) );

                ent.v.dmg_take = 0;
                ent.v.dmg_save = 0;
            }
        }

        private void WriteClientWeapons( MemoryEdict ent, MessageWriter msg )
        {
            if ( Engine.Common.GameKind == GameKind.StandardQuake )
            {
                msg.WriteByte( ( Int32 ) ent.v.weapon );
            }
            else
            {
                for ( var i = 0; i < 32; i++ )
                {
                    if ( ( ( ( Int32 ) ent.v.weapon ) & ( 1 << i ) ) != 0 )
                    {
                        msg.WriteByte( i );
                        break;
                    }
                }
            }
        }

        private void WriteClientHeader( MessageWriter msg, Int32 bits )
        {
            msg.WriteByte( ProtocolDef.svc_clientdata );
            msg.WriteShort( bits );
        }

        private void WriteClientAmmo( MemoryEdict ent, MessageWriter msg )
        {
            msg.WriteByte( ( Int32 ) ent.v.currentammo );
            msg.WriteByte( ( Int32 ) ent.v.ammo_shells );
            msg.WriteByte( ( Int32 ) ent.v.ammo_nails );
            msg.WriteByte( ( Int32 ) ent.v.ammo_rockets );
            msg.WriteByte( ( Int32 ) ent.v.ammo_cells );
        }

        private void WriteClientFixAngle( MemoryEdict ent, MessageWriter msg )
        {
            if ( ent.v.fixangle != 0 )
            {
                msg.WriteByte( ProtocolDef.svc_setangle );
                msg.WriteAngle( ent.v.angles.x );
                msg.WriteAngle( ent.v.angles.y );
                msg.WriteAngle( ent.v.angles.z );
                ent.v.fixangle = 0;
            }
        }

        private void WriteClientView( MemoryEdict ent, MessageWriter msg, Int32 bits )
        {
            if ( ( bits & ProtocolDef.SU_VIEWHEIGHT ) != 0 )
                msg.WriteChar( ( Int32 ) ent.v.view_ofs.z );

            if ( ( bits & ProtocolDef.SU_IDEALPITCH ) != 0 )
                msg.WriteChar( ( Int32 ) ent.v.idealpitch );
        }

        private void WriteClientPunches( MemoryEdict ent, MessageWriter msg, Int32 bits )
        {
            if ( ( bits & ProtocolDef.SU_PUNCH1 ) != 0 )
                msg.WriteChar( ( Int32 ) ent.v.punchangle.x );
            if ( ( bits & ProtocolDef.SU_VELOCITY1 ) != 0 )
                msg.WriteChar( ( Int32 ) ( ent.v.velocity.x / 16 ) );

            if ( ( bits & ProtocolDef.SU_PUNCH2 ) != 0 )
                msg.WriteChar( ( Int32 ) ent.v.punchangle.y );
            if ( ( bits & ProtocolDef.SU_VELOCITY2 ) != 0 )
                msg.WriteChar( ( Int32 ) ( ent.v.velocity.y / 16 ) );

            if ( ( bits & ProtocolDef.SU_PUNCH3 ) != 0 )
                msg.WriteChar( ( Int32 ) ent.v.punchangle.z );
            if ( ( bits & ProtocolDef.SU_VELOCITY3 ) != 0 )
                msg.WriteChar( ( Int32 ) ( ent.v.velocity.z / 16 ) );
        }

        private void WriteClientItems( MemoryEdict ent, MessageWriter msg, Int32 items, Int32 bits )
        {
            msg.WriteLong( items );

            if ( ( bits & ProtocolDef.SU_WEAPONFRAME ) != 0 )
                msg.WriteByte( ( Int32 ) ent.v.weaponframe );
            if ( ( bits & ProtocolDef.SU_ARMOR ) != 0 )
                msg.WriteByte( ( Int32 ) ent.v.armorvalue );
            if ( ( bits & ProtocolDef.SU_WEAPON ) != 0 )
                msg.WriteByte( ModelIndex( _programsState.GetString( ent.v.weaponmodel ) ) );
        }

        private void WriteClientHealth( MemoryEdict ent, MessageWriter msg )
        {
            msg.WriteShort( ( Int32 ) ent.v.health );
        }

        private Int32 GenerateClientBits( MemoryEdict ent, out Int32 items )
        {
            var bits = 0;

            if ( ent.v.view_ofs.z != ProtocolDef.DEFAULT_VIEWHEIGHT )
                bits |= ProtocolDef.SU_VIEWHEIGHT;

            if ( ent.v.idealpitch != 0 )
                bits |= ProtocolDef.SU_IDEALPITCH;

            // stuff the sigil bits into the high bits of items for sbar, or else
            // mix in items2
            var val = _programsEdict.GetEdictFieldFloat( ent, "items2", 0 );

            if ( val != 0 )
                items = ( Int32 ) ent.v.items | ( ( Int32 ) val << 23 );
            else
                items = ( Int32 ) ent.v.items | ( ( Int32 ) _programsState.GlobalStruct.serverflags << 28 );

            bits |= ProtocolDef.SU_ITEMS;

            if ( ( ( Int32 ) ent.v.flags & EdictFlags.FL_ONGROUND ) != 0 )
                bits |= ProtocolDef.SU_ONGROUND;

            if ( ent.v.waterlevel >= 2 )
                bits |= ProtocolDef.SU_INWATER;

            if ( ent.v.punchangle.x != 0 )
                bits |= ProtocolDef.SU_PUNCH1;
            if ( ent.v.punchangle.y != 0 )
                bits |= ProtocolDef.SU_PUNCH2;
            if ( ent.v.punchangle.z != 0 )
                bits |= ProtocolDef.SU_PUNCH3;

            if ( ent.v.velocity.x != 0 )
                bits |= ProtocolDef.SU_VELOCITY1;
            if ( ent.v.velocity.y != 0 )
                bits |= ProtocolDef.SU_VELOCITY2;
            if ( ent.v.velocity.z != 0 )
                bits |= ProtocolDef.SU_VELOCITY3;

            if ( ent.v.weaponframe != 0 )
                bits |= ProtocolDef.SU_WEAPONFRAME;

            if ( ent.v.armorvalue != 0 )
                bits |= ProtocolDef.SU_ARMOR;

            //	if (ent.v.weapon)
            bits |= ProtocolDef.SU_WEAPON;

            return bits;
        }

        /// <summary>
        /// SV_WriteClientdataToMessage
        /// </summary>
        public void WriteClientDataToMessage( MemoryEdict ent, MessageWriter msg )
        {
            //
            // send a damage message
            //
            WriteClientDamageMessage( ent, msg );

            //
            // send the current viewpos offset from the view entity
            //
            _serverUser.SetIdealPitch( );        // how much to look up / down ideally

            // a fixangle might get lost in a dropped packet.  Oh well.
            WriteClientFixAngle( ent, msg );

            var bits = GenerateClientBits( ent, out var items );

            // send the data
            WriteClientHeader( msg, bits );
            WriteClientView( ent, msg, bits );
            WriteClientPunches( ent, msg, bits );

            // always sent
            WriteClientItems( ent, msg, items, bits );
            WriteClientHealth( ent, msg );
            WriteClientAmmo( ent, msg );
            WriteClientWeapons( ent, msg );
        }

        /// <summary>
        /// SV_CheckForNewClients
        /// </summary>
        private void CheckForNewClients( )
        {
            //
            // check for new connections
            //
            while ( true )
            {
                var ret = _network.CheckNewConnections( );
                if ( ret == null )
                    break;

                //
                // init a new client structure
                //
                Int32 i;
                for ( i = 0; i < _state.StaticData.maxclients; i++ )
                    if ( !_state.StaticData.clients[i].active )
                        break;
                if ( i == _state.StaticData.maxclients )
                    Utilities.Error( "Host_CheckForNewClients: no free clients" );

                _state.StaticData.clients[i].netconnection = ret;
                ConnectClient( i );

                _network.ActiveConnections++;
            }
        }

        /// <summary>
        /// SV_SaveSpawnparms
        /// Grabs the current state of each client for saving across the
        /// transition to another level
        /// </summary>
        public void SaveSpawnparms( )
        {
            _state.StaticData.serverflags = ( Int32 ) _programsState.GlobalStruct.serverflags;

            for ( var i = 0; i < _state.StaticData.maxclients; i++ )
            {
                var client = _state.StaticData.clients[i];

                if ( !client.active )
                    continue;

                // call the progs to get default spawn parms for the new client
                _programsState.GlobalStruct.self = _state.EdictToProg( client.edict );
                _programsExec.Execute( _programsState.GlobalStruct.SetChangeParms );
                AssignGlobalSpawnparams( client );
            }
        }

        /// <summary>
        /// SV_SpawnServer
        /// </summary>
        public void SpawnServer( String server )
        {
            // let's not have any servers with no name
            if ( String.IsNullOrEmpty( _network.HostName ) )
                _cvars.Set( "hostname", "UNNAMED" );

            _screen.Elements.Reset( ElementFactory.CENTRE_PRINT );

            _logger.DPrint( "SpawnServer: {0}\n", server );
            _state.StaticData.changelevel_issued = false;     // now safe to issue another

            //
            // tell all connected clients that we are going to a new level
            //
            if ( _state.Data.active )
                SendReconnect( );

            //
            // make cvars consistant
            //
            if ( Cvars.Coop.Get<Boolean>( ) )
                _cvars.Set( "deathmatch", 0 );

            _state.CurrentSkill = ( Int32 ) ( Cvars.Skill.Get<Int32>( ) + 0.5 );

            if ( _state.CurrentSkill < 0 )
                _state.CurrentSkill = 0;
            else if ( _state.CurrentSkill > 3 )
                _state.CurrentSkill = 3;

            _cvars.Set( "skill", _state.CurrentSkill );

            //
            // set up the new server
            //
            _memoryHandler.ClearMemory( );

            _state.Data.Clear( );

            _state.Data.name = server;

            // load progs to get entity field count
            _programsState.Load( );

            // allocate server memory
            _state.Data.max_edicts = QDef.MAX_EDICTS;

            _state.Data.edicts = new MemoryEdict[_state.Data.max_edicts];

            for ( var i = 0; i < _state.Data.edicts.Length; i++ )
            {
                _state.Data.edicts[i] = new MemoryEdict( );
            }

            // leave slots at start for clients only
            _state.Data.num_edicts = _state.StaticData.maxclients + 1;

            MemoryEdict ent;

            for ( var i = 0; i < _state.StaticData.maxclients; i++ )
            {
                ent = _state.EdictNum( i + 1 );
                _state.StaticData.clients[i].edict = ent;
            }

            _state.Data.state = server_state_t.Loading;
            _state.Data.paused = false;
            _state.Data.time = 1.0;
            _state.Data.modelname = String.Format( "maps/{0}.bsp", server );
            _state.Data.worldmodel = ( BrushModelData ) _models.ForName( _state.Data.modelname, false, ModelType.Brush, true );
            
            if ( _state.Data.worldmodel == null )
            {
                _logger.Print( "Couldn't spawn server {0}\n", _state.Data.modelname );
                _state.Data.active = false;
                return;
            }

            _state.Data.models[1] = _state.Data.worldmodel;

            //
            // clear world interaction links
            //
            _serverWorld.ClearWorld( );

            _state.Data.sound_precache[0] = String.Empty;
            _state.Data.model_precache[0] = String.Empty;

            _state.Data.model_precache[1] = _state.Data.modelname;

            for ( var i = 1; i < _state.Data.worldmodel.NumSubModels; i++ )
            {
                _state.Data.model_precache[1 + i] = _LocalModels[i];
                _state.Data.models[i + 1] = _models.ForName( _LocalModels[i], false, ModelType.Brush, false );
            }

            //
            // load the rest of the entities
            //
            ent = _state.EdictNum( 0 );
            ent.Clear( );
            ent.v.model = _programsState.StringOffset( _state.Data.worldmodel.Name );

            if ( ent.v.model == -1 )
            {
                ent.v.model = _programsState.NewString( _state.Data.worldmodel.Name );
            }

            ent.v.modelindex = 1;       // world model
            ent.v.solid = Solids.SOLID_BSP;
            ent.v.movetype = Movetypes.MOVETYPE_PUSH;

            if ( Cvars.Coop.Get<Boolean>( ) )
                _programsState.GlobalStruct.coop = 1; //coop.value;
            else
                _programsState.GlobalStruct.deathmatch = Cvars.Deathmatch.Get<Int32>( );

            var offset = _programsState.NewString( _state.Data.name );
            _programsState.GlobalStruct.mapname = offset;

            // serverflags are for cross level information (sigils)
            _programsState.GlobalStruct.serverflags = _state.StaticData.serverflags;

            _programsEdict.LoadFromFile( _state.Data.worldmodel.Entities );

            _state.Data.active = true;

            // all setup is completed, any further precache statements are errors
            _state.Data.state = server_state_t.Active;

            // run two frames to allow everything to settle
            Time.SetToMaxDelta( );

            _serverPhysics.Physics( );
            _serverPhysics.Physics( );

            // create a baseline for more efficient communications
            CreateBaseline( );

            // send serverinfo to all connected clients
            for ( var i = 0; i < _state.StaticData.maxclients; i++ )
            {
                var client = _state.StaticData.clients[i];

                if ( client.active )
                    SendServerInfo( client );
            }

            GC.Collect( ); // TODO - Get rid of this bad practice.
            _logger.DPrint( "Server spawned.\n" );
        }

        /// <summary>
        /// SV_CleanupEnts
        /// </summary>
        private void CleanupEnts( )
        {
            for ( var i = 1; i < _state.Data.num_edicts; i++ )
            {
                var ent = _state.Data.edicts[i];
                ent.v.effects = ( Int32 ) ent.v.effects & ~EntityEffects.EF_MUZZLEFLASH;
            }
        }

        /// <summary>
        /// SV_SendNop
        /// Send a nop message without trashing or sending the accumulated client
        /// message buffer
        /// </summary>
        private void SendNop( client_t client )
        {
            var msg = new MessageWriter( 4 );
            msg.WriteChar( ProtocolDef.svc_nop );

            if ( _network.SendUnreliableMessage( client.netconnection, msg ) == -1 )
                DropClient( client, true ); // if the message couldn't send, kick off

            client.last_message = Time.Absolute;
        }

        /// <summary>
        /// SV_SendClientDatagram
        /// </summary>
        private Boolean SendClientDatagram( client_t client )
        {
            var msg = new MessageWriter( QDef.MAX_DATAGRAM ); // Uze todo: make static?

            msg.WriteByte( ProtocolDef.svc_time );
            msg.WriteFloat( ( Single ) _state.Data.time );

            // add the client specific data to the datagram
            WriteClientDataToMessage( client.edict, msg );

            WriteEntitiesToClient( client.edict, msg );

            // copy the server datagram if there is space
            if ( msg.Length + _state.Data.datagram.Length < msg.Capacity )
                msg.Write( _state.Data.datagram.Data, 0, _state.Data.datagram.Length );

            // send the datagram
            if ( _network.SendUnreliableMessage( client.netconnection, msg ) == -1 )
            {
                DropClient( client, true );// if the message couldn't send, kick off
                return false;
            }

            return true;
        }

        private Int32 SetupEntityBits( Int32 e, MemoryEdict ent )
        {
            var bits = 0;
            Vector3f miss;
            MathLib.VectorSubtract( ref ent.v.origin, ref ent.baseline.origin, out miss );
            if ( miss.x < -0.1f || miss.x > 0.1f )
                bits |= ProtocolDef.U_ORIGIN1;
            if ( miss.y < -0.1f || miss.y > 0.1f )
                bits |= ProtocolDef.U_ORIGIN2;
            if ( miss.z < -0.1f || miss.z > 0.1f )
                bits |= ProtocolDef.U_ORIGIN3;

            if ( ent.v.angles.x != ent.baseline.angles.x )
                bits |= ProtocolDef.U_ANGLE1;

            if ( ent.v.angles.y != ent.baseline.angles.y )
                bits |= ProtocolDef.U_ANGLE2;

            if ( ent.v.angles.z != ent.baseline.angles.z )
                bits |= ProtocolDef.U_ANGLE3;

            if ( ent.v.movetype == Movetypes.MOVETYPE_STEP )
                bits |= ProtocolDef.U_NOLERP;   // don't mess up the step animation

            if ( ent.baseline.colormap != ent.v.colormap )
                bits |= ProtocolDef.U_COLORMAP;

            if ( ent.baseline.skin != ent.v.skin )
                bits |= ProtocolDef.U_SKIN;

            if ( ent.baseline.frame != ent.v.frame )
                bits |= ProtocolDef.U_FRAME;

            if ( ent.baseline.effects != ent.v.effects )
                bits |= ProtocolDef.U_EFFECTS;

            if ( ent.baseline.modelindex != ent.v.modelindex )
                bits |= ProtocolDef.U_MODEL;

            if ( e >= 256 )
                bits |= ProtocolDef.U_LONGENTITY;

            if ( bits >= 256 )
                bits |= ProtocolDef.U_MOREBITS;

            return bits;
        }

        private void WriteEntityBytes( Int32 bits, Int32 e, MemoryEdict ent, MessageWriter msg )
        {
            msg.WriteByte( bits | ProtocolDef.U_SIGNAL );

            if ( ( bits & ProtocolDef.U_MOREBITS ) != 0 )
                msg.WriteByte( bits >> 8 );
            if ( ( bits & ProtocolDef.U_LONGENTITY ) != 0 )
                msg.WriteShort( e );
            else
                msg.WriteByte( e );

            if ( ( bits & ProtocolDef.U_MODEL ) != 0 )
                msg.WriteByte( ( Int32 ) ent.v.modelindex );
            if ( ( bits & ProtocolDef.U_FRAME ) != 0 )
                msg.WriteByte( ( Int32 ) ent.v.frame );
            if ( ( bits & ProtocolDef.U_COLORMAP ) != 0 )
                msg.WriteByte( ( Int32 ) ent.v.colormap );
            if ( ( bits & ProtocolDef.U_SKIN ) != 0 )
                msg.WriteByte( ( Int32 ) ent.v.skin );
            if ( ( bits & ProtocolDef.U_EFFECTS ) != 0 )
                msg.WriteByte( ( Int32 ) ent.v.effects );
            if ( ( bits & ProtocolDef.U_ORIGIN1 ) != 0 )
                msg.WriteCoord( ent.v.origin.x );
            if ( ( bits & ProtocolDef.U_ANGLE1 ) != 0 )
                msg.WriteAngle( ent.v.angles.x );
            if ( ( bits & ProtocolDef.U_ORIGIN2 ) != 0 )
                msg.WriteCoord( ent.v.origin.y );
            if ( ( bits & ProtocolDef.U_ANGLE2 ) != 0 )
                msg.WriteAngle( ent.v.angles.y );
            if ( ( bits & ProtocolDef.U_ORIGIN3 ) != 0 )
                msg.WriteCoord( ent.v.origin.z );
            if ( ( bits & ProtocolDef.U_ANGLE3 ) != 0 )
                msg.WriteAngle( ent.v.angles.z );
        }

        /// <summary>
        /// SV_WriteEntitiesToClient
        /// </summary>
        private void WriteEntitiesToClient( MemoryEdict clent, MessageWriter msg )
        {
            // find the client's PVS
            var org = Utilities.ToVector( ref clent.v.origin ) + Utilities.ToVector( ref clent.v.view_ofs );
            var pvs = FatPVS( ref org );

            // send over all entities (except the client) that touch the pvs
            for ( var e = 1; e < _state.Data.num_edicts; e++ )
            {
                var ent = _state.Data.edicts[e];
                // ignore if not touching a PV leaf
                if ( ent != clent ) // clent is ALLWAYS sent
                {
                    // ignore ents without visible models
                    var mname = _programsState.GetString( ent.v.model );
                    if ( String.IsNullOrEmpty( mname ) )
                        continue;

                    Int32 i;
                    for ( i = 0; i < ent.num_leafs; i++ )
                        if ( ( pvs[ent.leafnums[i] >> 3] & ( 1 << ( ent.leafnums[i] & 7 ) ) ) != 0 )
                            break;

                    if ( i == ent.num_leafs )
                        continue;       // not visible
                }

                if ( msg.Capacity - msg.Length < 16 )
                {
                    _logger.Print( "packet overflow\n" );
                    return;
                }

                // Send an update
                var bits = SetupEntityBits( e, ent );

                // Write the message
                WriteEntityBytes( bits, e, ent, msg );
            }
        }

        /// <summary>
        /// SV_FatPVS
        /// Calculates a PVS that is the inclusive or of all leafs within 8 pixels of the
        /// given point.
        /// </summary>
        private Byte[] FatPVS( ref Vector3 org )
        {
            _FatBytes = ( _state.Data.worldmodel.NumLeafs + 31 ) >> 3;
            Array.Clear( _FatPvs, 0, _FatPvs.Length );
            AddToFatPVS( ref org, _state.Data.worldmodel.Nodes[0] );
            return _FatPvs;
        }

        /// <summary>
        /// SV_AddToFatPVS
        /// The PVS must include a small area around the client to allow head bobbing
        /// or other small motion on the client side.  Otherwise, a bob might cause an
        /// entity that should be visible to not show up, especially when the bob
        /// crosses a waterline.
        /// </summary>
        private void AddToFatPVS( ref Vector3 org, MemoryNodeBase node )
        {
            while ( true )
            {
                // if this is a leaf, accumulate the pvs bits
                if ( node.contents < 0 )
                {
                    if ( node.contents != ( Int32 ) Q1Contents.Solid )
                    {
                        var pvs = _state.Data.worldmodel.LeafPVS( ( MemoryLeaf ) node );
                        for ( var i = 0; i < _FatBytes; i++ )
                            _FatPvs[i] |= pvs[i];
                    }
                    return;
                }

                var n = ( MemoryNode ) node;
                var plane = n.plane;
                var d = Vector3.Dot( org, plane.normal ) - plane.dist;
                if ( d > 8 )
                    node = n.children[0];
                else if ( d < -8 )
                    node = n.children[1];
                else
                {   // go down both
                    AddToFatPVS( ref org, n.children[0] );
                    node = n.children[1];
                }
            }
        }

        /// <summary>
        /// SV_UpdateToReliableMessages
        /// </summary>
        private void UpdateToReliableMessages( )
        {
            // check for changes to be sent over the reliable streams
            for ( var i = 0; i < _state.StaticData.maxclients; i++ )
            {
                var client = _state.StaticData.clients[i];
                if ( client.old_frags != client.edict.v.frags )
                {
                    for ( var j = 0; j < _state.StaticData.maxclients; j++ )
                    {
                        var otherClient = _state.StaticData.clients[j];
                        if ( !otherClient.active )
                            continue;

                        otherClient.message.WriteByte( ProtocolDef.svc_updatefrags );
                        otherClient.message.WriteByte( i );
                        otherClient.message.WriteShort( ( Int32 ) client.edict.v.frags );
                    }

                    client.old_frags = ( Int32 ) client.edict.v.frags;
                }
            }
            // TODO - Could this be moved to the loop above?
            for ( var j = 0; j < _state.StaticData.maxclients; j++ )
            {
                var client = _state.StaticData.clients[j];

                if ( !client.active )
                    continue;

                client.message.Write( _state.Data.reliable_datagram.Data, 0, _state.Data.reliable_datagram.Length );
            }

            _state.Data.reliable_datagram.Clear( );
        }

        /// <summary>
        /// SV_ConnectClient
        /// Initializes a client_t for a new net connection.  This will only be called
        /// once for a player each game, not once for each level change.
        /// </summary>
        private void ConnectClient( Int32 clientnum )
        {
            var client = _state.StaticData.clients[clientnum];

            _logger.DPrint( "Client {0} connected\n", client.netconnection.address );

            var edictnum = clientnum + 1;
            var ent = _state.EdictNum( edictnum );

            // set up the client_t
            var netconnection = client.netconnection;

            var spawn_parms = new Single[ServerDef.NUM_SPAWN_PARMS];
            if ( _state.Data.loadgame )
            {
                Array.Copy( client.spawn_parms, spawn_parms, spawn_parms.Length );
            }

            client.Clear( );
            client.netconnection = netconnection;
            client.name = "unconnected";
            client.active = true;
            client.spawned = false;
            client.edict = ent;
            client.message.AllowOverflow = true; // we can catch it
            client.privileged = false;

            if ( _state.Data.loadgame )
            {
                Array.Copy( spawn_parms, client.spawn_parms, spawn_parms.Length );
            }
            else
            {
                // call the progs to get default spawn parms for the new client
                _programsExec.Execute( _programsState.GlobalStruct.SetNewParms );

                AssignGlobalSpawnparams( client );
            }

            SendServerInfo( client );
        }

        private void AssignGlobalSpawnparams( client_t client )
        {
            client.spawn_parms[0] = _programsState.GlobalStruct.parm1;
            client.spawn_parms[1] = _programsState.GlobalStruct.parm2;
            client.spawn_parms[2] = _programsState.GlobalStruct.parm3;
            client.spawn_parms[3] = _programsState.GlobalStruct.parm4;

            client.spawn_parms[4] = _programsState.GlobalStruct.parm5;
            client.spawn_parms[5] = _programsState.GlobalStruct.parm6;
            client.spawn_parms[6] = _programsState.GlobalStruct.parm7;
            client.spawn_parms[7] = _programsState.GlobalStruct.parm8;

            client.spawn_parms[8] = _programsState.GlobalStruct.parm9;
            client.spawn_parms[9] = _programsState.GlobalStruct.parm10;
            client.spawn_parms[10] = _programsState.GlobalStruct.parm11;
            client.spawn_parms[11] = _programsState.GlobalStruct.parm12;

            client.spawn_parms[12] = _programsState.GlobalStruct.parm13;
            client.spawn_parms[13] = _programsState.GlobalStruct.parm14;
            client.spawn_parms[14] = _programsState.GlobalStruct.parm15;
            client.spawn_parms[15] = _programsState.GlobalStruct.parm16;
        }

        /// <summary>
        /// SV_SendServerinfo
        /// Sends the first message from the server to a connected client.
        /// This will be sent on the initial connection and upon each server load.
        /// </summary>
        private void SendServerInfo( client_t client )
        {
            var writer = client.message;

            writer.WriteByte( ProtocolDef.svc_print );
            writer.WriteString( String.Format( "{0}\nVERSION {1,4:F2} SERVER ({2} CRC)", ( Char ) 2, QDef.VERSION, _programsState.CRC ) );

            writer.WriteByte( ProtocolDef.svc_serverinfo );
            writer.WriteLong( ProtocolDef.PROTOCOL_VERSION );
            writer.WriteByte( _state.StaticData.maxclients );

            if ( !Cvars.Coop.Get<Boolean>( ) && Cvars.Deathmatch.Get<Int32>( ) != 0 )
                writer.WriteByte( ProtocolDef.GAME_DEATHMATCH );
            else
                writer.WriteByte( ProtocolDef.GAME_COOP );

            var message = _programsState.GetString( _state.Data.edicts[0].v.message );

            writer.WriteString( message );

            for ( var i = 1; i < _state.Data.model_precache.Length; i++ )
            {
                var tmp = _state.Data.model_precache[i];
                if ( String.IsNullOrEmpty( tmp ) )
                    break;
                writer.WriteString( tmp );
            }
            writer.WriteByte( 0 );

            for ( var i = 1; i < _state.Data.sound_precache.Length; i++ )
            {
                var tmp = _state.Data.sound_precache[i];
                if ( tmp == null )
                    break;
                writer.WriteString( tmp );
            }
            writer.WriteByte( 0 );

            // send music
            writer.WriteByte( ProtocolDef.svc_cdtrack );
            writer.WriteByte( ( Int32 ) _state.Data.edicts[0].v.sounds );
            writer.WriteByte( ( Int32 ) _state.Data.edicts[0].v.sounds );

            // set view
            writer.WriteByte( ProtocolDef.svc_setview );
            writer.WriteShort( _state.NumForEdict( client.edict ) );

            writer.WriteByte( ProtocolDef.svc_signonnum );
            writer.WriteByte( 1 );

            client.sendsignon = true;
            client.spawned = false;     // need prespawn, spawn, etc
        }

        /// <summary>
        /// SV_SendReconnect
        /// Tell all the clients that the server is changing levels
        /// </summary>
        private void SendReconnect( )
        {
            var msg = new MessageWriter( 128 );

            msg.WriteChar( ProtocolDef.svc_stufftext );
            msg.WriteString( "reconnect\n" );
            _network.SendToAll( msg, 5 );

            if ( _clientState.StaticData.state != cactive_t.ca_dedicated )
                _commands.ExecuteString( "reconnect\n", CommandSource.Command );
        }

        /// <summary>
        /// SV_CreateBaseline
        /// </summary>
        private void CreateBaseline( )
        {
            for ( var entnum = 0; entnum < _state.Data.num_edicts; entnum++ )
            {
                // get the current server version
                var svent = _state.EdictNum( entnum );
                if ( svent.free )
                    continue;
                if ( entnum > _state.StaticData.maxclients && svent.v.modelindex == 0 )
                    continue;

                //
                // create entity baseline
                //
                svent.baseline.origin = svent.v.origin;
                svent.baseline.angles = svent.v.angles;
                svent.baseline.frame = ( Int32 ) svent.v.frame;
                svent.baseline.skin = ( Int32 ) svent.v.skin;
                if ( entnum > 0 && entnum <= _state.StaticData.maxclients )
                {
                    svent.baseline.colormap = entnum;
                    svent.baseline.modelindex = ModelIndex( "progs/player.mdl" );
                }
                else
                {
                    svent.baseline.colormap = 0;
                    svent.baseline.modelindex = ModelIndex( _programsState.GetString( svent.v.model ) );
                }

                //
                // add to the message
                //
                _state.Data.signon.WriteByte( ProtocolDef.svc_spawnbaseline );
                _state.Data.signon.WriteShort( entnum );

                _state.Data.signon.WriteByte( svent.baseline.modelindex );
                _state.Data.signon.WriteByte( svent.baseline.frame );
                _state.Data.signon.WriteByte( svent.baseline.colormap );
                _state.Data.signon.WriteByte( svent.baseline.skin );

                _state.Data.signon.WriteCoord( svent.baseline.origin.x );
                _state.Data.signon.WriteAngle( svent.baseline.angles.x );
                _state.Data.signon.WriteCoord( svent.baseline.origin.y );
                _state.Data.signon.WriteAngle( svent.baseline.angles.y );
                _state.Data.signon.WriteCoord( svent.baseline.origin.z );
                _state.Data.signon.WriteAngle( svent.baseline.angles.z );
            }
        }
    }    
}
