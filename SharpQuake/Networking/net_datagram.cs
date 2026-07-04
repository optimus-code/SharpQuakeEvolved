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
using System.Net;
using System.Net.Sockets;
using SharpQuake.Factories.Rendering.UI;
using SharpQuake.Framework;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Networking.Server;

namespace SharpQuake
{
    internal class net_datagram : INetDriver
    {
        public String Name
        {
            get
            {
                return "Datagram";
            }
        }

        public Boolean IsInitialised
        {
            get
            {
                return _IsInitialised;
            }
        }

        private Int32 _DriverLevel;
        private Boolean _IsInitialised;
        private Byte[] _PacketBuffer;

        // statistic counters
        private Int32 packetsSent;

        private Int32 packetsReSent;
        private Int32 packetsReceived;
        private Int32 receivedDuplicateCount;
        private Int32 shortPacketCount;
        private Int32 droppedDatagrams;

        private readonly IConsoleLogger _logger;
        private readonly CommandFactory _commands;
        private readonly ClientVariableFactory _cvars;
        private readonly Network _network;
        private readonly ServerState _serverState;
        private readonly Scr _screen;
        private readonly MenuFactory _menus;

        public net_datagram( IConsoleLogger logger, CommandFactory commands, ClientVariableFactory cvars, 
            Network network, ServerState serverState, Scr screen, MenuFactory menus )
        {
            _logger = logger;
            _commands = commands;
            _cvars = cvars;
            _network = network;
            _serverState = serverState;
            _screen = screen;
            _menus = menus;
            _PacketBuffer = new Byte[NetworkDef.NET_DATAGRAMSIZE];
        }

        private static String StrAddr( EndPoint ep )
        {
            return ep.ToString( );
        }

        private void PrintStatsLocally()
        {
            _logger.Print( "unreliable messages sent   = %i\n", _network.UnreliableMessagesSent );
            _logger.Print( "unreliable messages recv   = %i\n", _network.UnreliableMessagesReceived );
            _logger.Print( "reliable messages sent     = %i\n", _network.MessagesSent );
            _logger.Print( "reliable messages received = %i\n", _network.MessagesReceived );
            _logger.Print( "packetsSent                = %i\n", packetsSent );
            _logger.Print( "packetsReSent              = %i\n", packetsReSent );
            _logger.Print( "packetsReceived            = %i\n", packetsReceived );
            _logger.Print( "receivedDuplicateCount     = %i\n", receivedDuplicateCount );
            _logger.Print( "shortPacketCount           = %i\n", shortPacketCount );
            _logger.Print( "droppedDatagrams           = %i\n", droppedDatagrams );
        }

        private void PrintStatsForAllSockets()
		{
            foreach ( var s in _network.ActiveSockets )
			{
				PrintStats( s );
			}

			foreach ( var s in _network.FreeSockets )
			{
				PrintStats( s );
			}
		}

        private qsocket_t SearchActiveSockets( String cmdAddr )
        {
            qsocket_t sock = null;
            foreach ( var s in _network.ActiveSockets )
            {
                if ( Utilities.SameText( s.address, cmdAddr ) )
                {
                    sock = s;
                    break;
                }
            }
            return sock;
        }

        private qsocket_t SearchFreeSockets( String cmdAddr )
        {
            qsocket_t sock = null;
            foreach ( var s in _network.FreeSockets )
            {
                if ( Utilities.SameText( s.address, cmdAddr ) )
                {
                    sock = s;
                    break;
                }
            }
            return sock;
        }

        private void PrintStatsForSocket( CommandMessage msg )
		{
            qsocket_t sock = null;
            var cmdAddr = msg.Parameters[0];

            sock = SearchActiveSockets( cmdAddr );

            if ( sock == null )
			{
				sock = SearchFreeSockets( cmdAddr );
			}

			if ( sock == null )
			{
				return;
			}

			PrintStats( sock );
        }

        // NET_Stats_f
        private void Stats_f( CommandMessage msg )
        {
            if ( msg.Parameters == null || msg.Parameters.Length == 0 )
			{
				PrintStatsLocally();
			}
			else if ( msg.Parameters[0] == "*" )
			{
				PrintStatsForAllSockets();
			}
			else
			{
				PrintStatsForSocket( msg );
			}
		}

        // PrintStats(qsocket_t* s)
        private void PrintStats( qsocket_t s )
        {
            _logger.Print( "canSend = {0:4}   \n", s.canSend );
            _logger.Print( "sendSeq = {0:4}   ", s.sendSequence );
            _logger.Print( "recvSeq = {0:4}   \n", s.receiveSequence );
            _logger.Print( "\n" );
        }

        private void AssignHostDetails( INetLanDriver driver )
		{
            if ( driver is net_tcp_ip )
            {
                var tcpIP = ( ( net_tcp_ip ) driver );

                tcpIP.HostName = _cvars.Get( "hostname" ).Get<String>();
                tcpIP.HostPort = _network.HostPort;
            }
        }

        private void ReturnHostDetails( INetLanDriver driver )
        {
            if ( driver is net_tcp_ip )
            {
                var tcpIP = ( ( net_tcp_ip ) driver );

                _network.MyTcpIpAddress = tcpIP.HostAddress;

                _cvars.Set( "hostname", tcpIP.HostName );
            }
        }

        public void Initialise( )
        {
            _DriverLevel = Array.IndexOf( _network.Drivers, this );
            _commands.Add( "net_stats", Stats_f );

            if ( CommandLine.HasParam( "-nolan" ) )
				return;

			foreach ( var driver in _network.LanDrivers )
            {
                AssignHostDetails( driver );

                driver.Initialise( );

                ReturnHostDetails( driver );
            }

#if BAN_TEST
	        Cmd_AddCommand ("ban", NET_Ban_f);
#endif
            //Cmd.Add("test", Test_f);
            //Cmd.Add("test2", Test2_f);

            _IsInitialised = true;
        }

        /// <summary>
        /// Datagram_Listen
        /// </summary>
        public void Listen( Boolean state )
        {
            foreach ( var drv in _network.LanDrivers )
            {
                if ( drv.IsInitialised )
				{
					drv.Listen( state );
				}
			}
        }

        /// <summary>
        /// Datagram_SearchForHosts
        /// </summary>
        public void SearchForHosts( Boolean xmit )
        {
            for ( _network.LanDriverLevel = 0; _network.LanDriverLevel < _network.LanDrivers.Length; _network.LanDriverLevel++ )
            {
                if ( _network.HostCacheCount == NetworkDef.HOSTCACHESIZE )
				{
					break;
				}

				if ( _network.LanDrivers[_network.LanDriverLevel].IsInitialised )
				{
					InternalSearchForHosts( xmit );
				}
			}
        }

        /// <summary>
        /// Datagram_Connect
        /// </summary>
        public qsocket_t Connect( String host )
        {
            qsocket_t ret = null;

            for ( _network.LanDriverLevel = 0; _network.LanDriverLevel < _network.LanDrivers.Length; _network.LanDriverLevel++ )
			{
				if ( _network.LanDrivers[_network.LanDriverLevel].IsInitialised )
                {
                    ret = InternalConnect( host );

                    if ( ret != null )
						break;
				}
			}

			return ret;
        }

        /// <summary>
        /// Datagram_CheckNewConnections
        /// </summary>
        public qsocket_t CheckNewConnections( )
        {
            qsocket_t ret = null;

            for ( _network.LanDriverLevel = 0; _network.LanDriverLevel < _network.LanDrivers.Length; _network.LanDriverLevel++ )
			{
				if ( _network.LanDriver.IsInitialised )
                {
                    ret = InternalCheckNewConnections( );

                    if ( ret != null )
						break;
				}
			}

			return ret;
        }

        private qsocket_t ProcessRequestServerInfo( Socket acceptsock, EndPoint clientaddr )
		{
            var tmp = _network.Reader.ReadString();

            if ( tmp != "QUAKE" )
                return null;

            _network.Message.Clear();

            // save space for the header, filled in later
            _network.Message.WriteLong( 0 );
            _network.Message.WriteByte( CCRep.CCREP_SERVER_INFO );
            var newaddr = acceptsock.LocalEndPoint; //dfunc.GetSocketAddr(acceptsock, &newaddr);
            _network.Message.WriteString( newaddr.ToString() ); // dfunc.AddrToString(&newaddr));
            _network.Message.WriteString( _network.HostName );
            _network.Message.WriteString( _serverState.Data.name );
            _network.Message.WriteByte( _network.ActiveConnections );
            _network.Message.WriteByte( _serverState.StaticData.maxclients );
            _network.Message.WriteByte( NetworkDef.NET_PROTOCOL_VERSION );
            Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );
            _network.LanDriver.Write( acceptsock, _network.Message.Data, _network.Message.Length, clientaddr );
            _network.Message.Clear();
            return null;
        }

        private qsocket_t ProcessRequestPlayerInfo( Socket acceptsock, EndPoint clientaddr )
        {
            var playerNumber = _network.Reader.ReadByte();
            Int32 clientNumber, activeNumber = -1;
            client_t client = null;

            for ( clientNumber = 0; clientNumber < _serverState.StaticData.maxclients; clientNumber++ )
            {
                client = _serverState.StaticData.clients[clientNumber];
                if ( client.active )
                {
                    activeNumber++;

                    if ( activeNumber == playerNumber )
                        break;
                }
            }

            if ( clientNumber == _serverState.StaticData.maxclients )
                return null;

            _network.Message.Clear();
            // save space for the header, filled in later
            _network.Message.WriteLong( 0 );
            _network.Message.WriteByte( CCRep.CCREP_PLAYER_INFO );
            _network.Message.WriteByte( playerNumber );
            _network.Message.WriteString( client.name );
            _network.Message.WriteLong( client.colors );
            _network.Message.WriteLong( ( Int32 ) client.edict.v.frags );
            _network.Message.WriteLong( ( Int32 ) ( _network.Time - client.netconnection.connecttime ) );
            _network.Message.WriteString( client.netconnection.address );
            Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );
            _network.LanDriver.Write( acceptsock, _network.Message.Data, _network.Message.Length, clientaddr );
            _network.Message.Clear();

            return null;
        }

        private qsocket_t ProcessRequestRuleInfo( Socket acceptsock, EndPoint clientaddr )
        {
            // find the search start location
            var prevCvarName = _network.Reader.ReadString();
            ClientVariable var;
            if ( !String.IsNullOrEmpty( prevCvarName ) )
            {
                var = _cvars.Get( prevCvarName );

                if ( var == null )
                    return null;

                var index = _cvars.IndexOf( var.Name );

                var = _cvars.GetByIndex( index + 1 );
            }
            else
                var = _cvars.GetByIndex( 0 );

            // search for the next server cvar
            while ( var != null )
            {
                if ( var.IsServer )
                    break;

                var index = _cvars.IndexOf( var.Name );

                var = _cvars.GetByIndex( index + 1 );
            }

            // send the response
            _network.Message.Clear();

            // save space for the header, filled in later
            _network.Message.WriteLong( 0 );
            _network.Message.WriteByte( CCRep.CCREP_RULE_INFO );
            if ( var != null )
            {
                _network.Message.WriteString( var.Name );
                _network.Message.WriteString( var.Get().ToString() );
            }

            Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );

            _network.LanDriver.Write( acceptsock, _network.Message.Data, _network.Message.Length, clientaddr );
            _network.Message.Clear();

            return null;
        }

        private qsocket_t ProcessInvalidProtocol( Socket acceptsock, EndPoint clientaddr )
		{
            _network.Message.Clear();
            // save space for the header, filled in later
            _network.Message.WriteLong( 0 );
            _network.Message.WriteByte( CCRep.CCREP_REJECT );
            _network.Message.WriteString( "Incompatible version.\n" );
            Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );
            _network.LanDriver.Write( acceptsock, _network.Message.Data, _network.Message.Length, clientaddr );
            _network.Message.Clear();
            return null;
        }

        private Boolean CheckForExistingConnection( Socket acceptsock, EndPoint clientaddr )
		{
            var exit = false;

            // see if this guy is already connected
            foreach ( var s in _network.ActiveSockets )
            {
                if ( s.driver != _network.DriverLevel )
                    continue;

                var ret = _network.LanDriver.AddrCompare( clientaddr, s.addr );
                if ( ret >= 0 )
                {
                    // is this a duplicate connection reqeust?
                    if ( ret == 0 && _network.Time - s.connecttime < 2.0 )
                    {
                        // yes, so send a duplicate reply
                        _network.Message.Clear();
                        // save space for the header, filled in later
                        _network.Message.WriteLong( 0 );
                        _network.Message.WriteByte( CCRep.CCREP_ACCEPT );
                        var newaddr = s.socket.LocalEndPoint; //dfunc.GetSocketAddr(s.socket, &newaddr);
                        _network.Message.WriteLong( _network.LanDriver.GetSocketPort( newaddr ) );
                        Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                            ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );
                        _network.LanDriver.Write( acceptsock, _network.Message.Data, _network.Message.Length, clientaddr );
                        _network.Message.Clear();
                        exit = true;
                    }

                    // it's somebody coming back in from a crash/disconnect
                    // so close the old qsocket and let their retry get them back in
                    if ( !exit )
                    {
                        _network.Close( s );
                        exit = true;
                    }
                }

                if ( exit )
                    break;
            }

            return exit;
        }

        private Boolean AllocateQSocket( Socket acceptsock, EndPoint clientaddr, out qsocket_t sock )
		{
            var success = true;

            // allocate a QSocket
            sock = _network.NewSocket();

            if ( sock == null )
            {
                // no room; try to let him know
                _network.Message.Clear();
                // save space for the header, filled in later
                _network.Message.WriteLong( 0 );
                _network.Message.WriteByte( CCRep.CCREP_REJECT );
                _network.Message.WriteString( "Server is full.\n" );
                Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                    ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );
                _network.LanDriver.Write( acceptsock, _network.Message.Data, _network.Message.Length, clientaddr );
                _network.Message.Clear();
                success = false;
            }

            return success;
        }

        private void SendHostDetails( Socket acceptsock, Socket newsock, EndPoint clientaddr )
		{
            _network.Message.Clear();
            // save space for the header, filled in later
            _network.Message.WriteLong( 0 );
            _network.Message.WriteByte( CCRep.CCREP_ACCEPT );
            var newaddr2 = newsock.LocalEndPoint;// dfunc.GetSocketAddr(newsock, &newaddr);
            _network.Message.WriteLong( _network.LanDriver.GetSocketPort( newaddr2 ) );
            Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );
            _network.LanDriver.Write( acceptsock, _network.Message.Data, _network.Message.Length, clientaddr );
            _network.Message.Clear();
        }
        /// <summary>
        /// _Datagram_CheckNewConnections
        /// </summary>
        public qsocket_t InternalCheckNewConnections( )
        {
            var acceptsock = _network.LanDriver.CheckNewConnections( );

            if ( acceptsock == null )
				return null;

			EndPoint clientaddr = new IPEndPoint( IPAddress.Any, 0 );
            _network.Message.FillFrom( _network, acceptsock, ref clientaddr );

            if ( _network.Message.Length < sizeof( Int32 ) )
				return null;

			_network.Reader.Reset( );

            var control = EndianHelper.BigLong( _network.Reader.ReadLong( ) );
            var isControlInvalid = ( control == -1 ||
                ( ( control & ( ~NetFlags.NETFLAG_LENGTH_MASK ) ) != NetFlags.NETFLAG_CTL ) ||
                ( control & NetFlags.NETFLAG_LENGTH_MASK ) != _network.Message.Length );

            if ( isControlInvalid )
				return null;

			var command = _network.Reader.ReadByte( );

            switch ( command )
			{
                case CCReq.CCREQ_SERVER_INFO:
                    return ProcessRequestServerInfo( acceptsock, clientaddr );

                case CCReq.CCREQ_PLAYER_INFO:
                    return ProcessRequestPlayerInfo( acceptsock, clientaddr );

                case CCReq.CCREQ_RULE_INFO:
                    return ProcessRequestRuleInfo( acceptsock, clientaddr );

                case CCReq.CCREQ_CONNECT:
                    if ( _network.Reader.ReadString() != "QUAKE" )
                        return null;

                    if ( _network.Reader.ReadByte() != NetworkDef.NET_PROTOCOL_VERSION )
                        return ProcessInvalidProtocol( acceptsock, clientaddr );
#if BAN_TEST
                    // check for a ban
                    if (clientaddr.sa_family == AF_INET)
                    {
                        unsigned long testAddr;
                        testAddr = ((struct sockaddr_in *)&clientaddr)->sin_addr.s_addr;
                        if ((testAddr & banMask) == banAddr)
                        {
                            SZ_Clear(&net_message);
                            // save space for the header, filled in later
                            MSG_WriteLong(&net_message, 0);
                            MSG_WriteByte(&net_message, CCREP_REJECT);
                            MSG_WriteString(&net_message, "You have been banned.\n");
                            *((int *)net_message.data) = BigLong(NETFLAG_CTL | (net_message.cursize & NETFLAG_LENGTH_MASK));
                            dfunc.Write (acceptsock, net_message.data, net_message.cursize, &clientaddr);
                            SZ_Clear(&net_message);
                            return NULL;
                        }
                    }
#endif
                    // see if this guy is already connected
                    if ( CheckForExistingConnection( acceptsock, clientaddr ) )
                        return null;

                    // allocate a QSocket
                    if ( !AllocateQSocket( acceptsock, clientaddr, out var sock ) )
                        return null;

                    // allocate a network socket
                    var newsock = _network.LanDriver.OpenSocket( 0 );

                    if ( newsock == null )
                    {
                        _network.FreeSocket( sock );
                        return null;
                    }

                    // connect to the client
                    if ( _network.LanDriver.Connect( newsock, clientaddr ) == -1 )
                    {
                        _network.LanDriver.CloseSocket( newsock );
                        _network.FreeSocket( sock );
                        return null;
                    }

                    // everything is allocated, just fill in the details
                    sock.socket = newsock;
                    sock.landriver = _network.LanDriverLevel;
                    sock.addr = clientaddr;
                    sock.address = clientaddr.ToString();

                    // send him back the info about the server connection he has been allocated
                    SendHostDetails( acceptsock, newsock, clientaddr );

                    return sock;
            }                  		

            return null;
        }

        public Int32 GetMessage( qsocket_t sock )
        {
            if ( !sock.canSend )
			{
				if ( ( _network.Time - sock.lastSendTime ) > 1.0 )
					ReSendMessage( sock );
			}

			var ret = 0;
            EndPoint readaddr = new IPEndPoint( IPAddress.Any, 0 );
            while ( true )
            {
                var length = sock.Read( _PacketBuffer, NetworkDef.NET_DATAGRAMSIZE, ref readaddr );

                if ( length == 0 )
					break;

				if ( length == -1 )
                {
                    _logger.Print( "Read error\n" );
                    return -1;
                }

                if ( sock.LanDriver.AddrCompare( readaddr, sock.addr ) != 0 )
                {
#if DEBUG
                    _logger.DPrint( "Forged packet received\n" );
                    _logger.DPrint( "Expected: {0}\n", StrAddr( sock.addr ) );
                    _logger.DPrint( "Received: {0}\n", StrAddr( readaddr ) );
#endif
                    continue;
                }

                if ( length < NetworkDef.NET_HEADERSIZE )
                {
                    shortPacketCount++;
                    continue;
                }

                var header = Utilities.BytesToStructure<PacketHeader>( _PacketBuffer, 0 );

                length = EndianHelper.BigLong( header.length );
                var flags = length & ( ~NetFlags.NETFLAG_LENGTH_MASK );
                length &= NetFlags.NETFLAG_LENGTH_MASK;

                if ( ( flags & NetFlags.NETFLAG_CTL ) != 0 )
					continue;

				var sequence = ( UInt32 ) EndianHelper.BigLong( header.sequence );
                packetsReceived++;

                if ( ( flags & NetFlags.NETFLAG_UNRELIABLE ) != 0 )
                {
                    if ( sequence < sock.unreliableReceiveSequence )
                    {
                        _logger.DPrint( "Got a stale datagram\n" );
                        ret = 0;
                        break;
                    }
                    if ( sequence != sock.unreliableReceiveSequence )
                    {
                        var count = ( Int32 ) ( sequence - sock.unreliableReceiveSequence );
                        droppedDatagrams += count;
                        _logger.DPrint( "Dropped {0} datagram(s)\n", count );
                    }
                    sock.unreliableReceiveSequence = sequence + 1;

                    length -= NetworkDef.NET_HEADERSIZE;

                    _network.Message.FillFrom( _PacketBuffer, PacketHeader.SizeInBytes, length );

                    ret = 2;
                    break;
                }

                if ( ( flags & NetFlags.NETFLAG_ACK ) != 0 )
                {
                    if ( sequence != ( sock.sendSequence - 1 ) )
                    {
                        _logger.DPrint( "Stale ACK received\n" );
                        continue;
                    }
                    if ( sequence == sock.ackSequence )
                    {
                        sock.ackSequence++;
                        if ( sock.ackSequence != sock.sendSequence )
						{
							_logger.DPrint( "ack sequencing error\n" );
						}
					}
                    else
                    {
                        _logger.DPrint( "Duplicate ACK received\n" );
                        continue;
                    }
                    sock.sendMessageLength -= QDef.MAX_DATAGRAM;
                    if ( sock.sendMessageLength > 0 )
                    {
                        Buffer.BlockCopy( sock.sendMessage, QDef.MAX_DATAGRAM, sock.sendMessage, 0, sock.sendMessageLength );
                        sock.sendNext = true;
                    }
                    else
                    {
                        sock.sendMessageLength = 0;
                        sock.canSend = true;
                    }
                    continue;
                }

                if ( ( flags & NetFlags.NETFLAG_DATA ) != 0 )
                {
                    header.length = EndianHelper.BigLong( NetworkDef.NET_HEADERSIZE | NetFlags.NETFLAG_ACK );
                    header.sequence = EndianHelper.BigLong( ( Int32 ) sequence );

                    Utilities.StructureToBytes( ref header, _PacketBuffer, 0 );
                    sock.Write( _PacketBuffer, NetworkDef.NET_HEADERSIZE, readaddr );

                    if ( sequence != sock.receiveSequence )
                    {
                        receivedDuplicateCount++;
                        continue;
                    }
                    sock.receiveSequence++;

                    length -= NetworkDef.NET_HEADERSIZE;

                    if ( ( flags & NetFlags.NETFLAG_EOM ) != 0 )
                    {
                        _network.Message.Clear( );
                        _network.Message.FillFrom( sock.receiveMessage, 0, sock.receiveMessageLength );
                        _network.Message.AppendFrom( _PacketBuffer, PacketHeader.SizeInBytes, length );
                        sock.receiveMessageLength = 0;

                        ret = 1;
                        break;
                    }

                    Buffer.BlockCopy( _PacketBuffer, PacketHeader.SizeInBytes, sock.receiveMessage, sock.receiveMessageLength, length );
                    sock.receiveMessageLength += length;
                    continue;
                }
            }

            if ( sock.sendNext )
				SendMessageNext( sock );

			return ret;
        }

        /// <summary>
        /// Datagram_SendMessage
        /// </summary>
        public Int32 SendMessage( qsocket_t sock, MessageWriter data )
        {
#if DEBUG
            if ( data.IsEmpty )
				Utilities.Error( "Datagram_SendMessage: zero length message\n" );

			if ( data.Length > NetworkDef.NET_MAXMESSAGE )
				Utilities.Error( "Datagram_SendMessage: message too big {0}\n", data.Length );

			if ( !sock.canSend )
				Utilities.Error( "SendMessage: called with canSend == false\n" );
#endif
			Buffer.BlockCopy( data.Data, 0, sock.sendMessage, 0, data.Length );
            sock.sendMessageLength = data.Length;

            Int32 dataLen, eom;
            if ( data.Length <= QDef.MAX_DATAGRAM )
            {
                dataLen = data.Length;
                eom = NetFlags.NETFLAG_EOM;
            }
            else
            {
                dataLen = QDef.MAX_DATAGRAM;
                eom = 0;
            }
            var packetLen = NetworkDef.NET_HEADERSIZE + dataLen;

            PacketHeader header;
            header.length = EndianHelper.BigLong( packetLen | NetFlags.NETFLAG_DATA | eom );
            header.sequence = EndianHelper.BigLong( ( Int32 ) sock.sendSequence++ );
            Utilities.StructureToBytes( ref header, _PacketBuffer, 0 );
            Buffer.BlockCopy( data.Data, 0, _PacketBuffer, PacketHeader.SizeInBytes, dataLen );

            sock.canSend = false;

            if ( sock.Write( _PacketBuffer, packetLen, sock.addr ) == -1 )
				return -1;

			sock.lastSendTime = _network.Time;
            packetsSent++;
            return 1;
        }

        /// <summary>
        /// Datagram_SendUnreliableMessage
        /// </summary>
        public Int32 SendUnreliableMessage( qsocket_t sock, MessageWriter data )
        {
            Int32 packetLen;

#if DEBUG
            if ( data.IsEmpty )
				Utilities.Error( "Datagram_SendUnreliableMessage: zero length message\n" );

			if ( data.Length > QDef.MAX_DATAGRAM )
				Utilities.Error( "Datagram_SendUnreliableMessage: message too big {0}\n", data.Length );
#endif

			packetLen = NetworkDef.NET_HEADERSIZE + data.Length;

            PacketHeader header;
            header.length = EndianHelper.BigLong( packetLen | NetFlags.NETFLAG_UNRELIABLE );
            header.sequence = EndianHelper.BigLong( ( Int32 ) sock.unreliableSendSequence++ );
            Utilities.StructureToBytes( ref header, _PacketBuffer, 0 );
            Buffer.BlockCopy( data.Data, 0, _PacketBuffer, PacketHeader.SizeInBytes, data.Length );

            if ( sock.Write( _PacketBuffer, packetLen, sock.addr ) == -1 )
				return -1;

			packetsSent++;
            return 1;
        }

        /// <summary>
        /// Datagram_CanSendMessage
        /// </summary>
        public Boolean CanSendMessage( qsocket_t sock )
        {
            if ( sock.sendNext )
				SendMessageNext( sock );

			return sock.canSend;
        }

        /// <summary>
        /// Datagram_CanSendUnreliableMessage
        /// </summary>
        public Boolean CanSendUnreliableMessage( qsocket_t sock )
        {
            return true;
        }

        /// <summary>
        /// Datagram_Close
        /// </summary>
        public void Close( qsocket_t sock )
        {
            sock.LanDriver.CloseSocket( sock.socket );
        }

        /// <summary>
        /// Datagram_Shutdown
        /// </summary>
        public void Shutdown( )
        {
            //
            // shutdown the lan drivers
            //
            foreach ( var driver in _network.LanDrivers )
            {
                if ( driver.IsInitialised )
					driver.Dispose( );
			}

            _IsInitialised = false;
        }

        /// <summary>
        /// _Datagram_SearchForHosts
        /// </summary>
        private void InternalSearchForHosts( Boolean xmit )
        {
            var myaddr = _network.LanDriver.ControlSocket.LocalEndPoint;
            if ( xmit )
            {
                _network.Message.Clear( );
                // save space for the header, filled in later
                _network.Message.WriteLong( 0 );
                _network.Message.WriteByte( CCReq.CCREQ_SERVER_INFO );
                _network.Message.WriteString( "QUAKE" );
                _network.Message.WriteByte( NetworkDef.NET_PROTOCOL_VERSION );
                Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                    ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );
                _network.LanDriver.Broadcast( _network.LanDriver.ControlSocket, _network.Message.Data, _network.Message.Length );
                _network.Message.Clear( );
            }

            EndPoint readaddr = new IPEndPoint( IPAddress.Any, 0 );
            while ( true )
            {
                _network.Message.FillFrom( _network, _network.LanDriver.ControlSocket, ref readaddr );
                if ( _network.Message.IsEmpty )
					break;

				if ( _network.Message.Length < sizeof( Int32 ) )
					continue;

				// don't answer our own query
				if ( _network.LanDriver.AddrCompare( readaddr, myaddr ) >= 0 )
					continue;

				// is the cache full?
				if ( _network.HostCacheCount == NetworkDef.HOSTCACHESIZE )
					continue;

				_network.Reader.Reset( );
                var control = EndianHelper.BigLong( _network.Reader.ReadLong( ) );// BigLong(*((int *)net_message.data));
                //MSG_ReadLong();

                if ( control == -1 )
					continue;

				if ( ( control & ( ~NetFlags.NETFLAG_LENGTH_MASK ) ) != NetFlags.NETFLAG_CTL )
					continue;

				if ( ( control & NetFlags.NETFLAG_LENGTH_MASK ) != _network.Message.Length )
					continue;

				if ( _network.Reader.ReadByte( ) != CCRep.CCREP_SERVER_INFO )
					continue;

				var _hostIP = readaddr;

                readaddr = _network.LanDriver.GetAddrFromName( _network.Reader.ReadString( ) );
                Int32 n;
                // search the cache for this server
                for ( n = 0; n < _network.HostCacheCount; n++ )
				{
					if ( _network.LanDriver.AddrCompare( readaddr, _network.HostCache[n].addr ) == 0 )
						break;
				}

				// is it already there?
				if ( n < _network.HostCacheCount )
					continue;

				// add it
				_network.HostCacheCount++;
                var hc = _network.HostCache[n];
                hc.name = _network.Reader.ReadString( );
                hc.map = _network.Reader.ReadString( );
                hc.users = _network.Reader.ReadByte( );
                hc.maxusers = _network.Reader.ReadByte( );
                if ( _network.Reader.ReadByte( ) != NetworkDef.NET_PROTOCOL_VERSION )
                {
                    hc.cname = hc.name;
                    hc.name = "*" + hc.name;
                }
                //IPEndPoint ep = (IPEndPoint)readaddr;
                //hc.addr = new IPEndPoint( ep.Address, ep.Port );
                var ip = readaddr.ToString( ).Split( ':' ); //readaddr.ToString()
                IPAddress _ipAddress;
                Int32 _port;
                IPAddress.TryParse( ip[0].ToString( ), out _ipAddress );
                Int32.TryParse( ip[1].ToString( ), out _port );
                hc.addr = new IPEndPoint( _ipAddress, _port );
                hc.driver = _network.DriverLevel;
                hc.ldriver = _network.LanDriverLevel;
                hc.cname = _hostIP.ToString( ); //readaddr.ToString();

                // check for a name conflict
                for ( var i = 0; i < _network.HostCacheCount; i++ )
                {
                    if ( i == n )
						continue;

					var hc2 = _network.HostCache[i];
                    if ( hc.name == hc2.name )
                    {
                        i = hc.name.Length;
                        if ( i < 15 && hc.name[i - 1] > '8' )
                        {
                            hc.name = hc.name.Substring( 0, i ) + '0';
                        }
                        else
						{
							hc.name = hc.name.Substring( 0, i - 1 ) + ( Char ) ( hc.name[i - 1] + 1 );
						}

						i = 0;// -1;
                    }
                }
            }
        }

        private void WriteInternalConnectHeader( Socket newsock, EndPoint sendaddr )
		{
            _network.Message.Clear();
            // save space for the header, filled in later
            _network.Message.WriteLong( 0 );
            _network.Message.WriteByte( CCReq.CCREQ_CONNECT );
            _network.Message.WriteString( "QUAKE" );
            _network.Message.WriteByte( NetworkDef.NET_PROTOCOL_VERSION );
            Utilities.WriteInt( _network.Message.Data, 0, EndianHelper.BigLong( NetFlags.NETFLAG_CTL |
                ( _network.Message.Length & NetFlags.NETFLAG_LENGTH_MASK ) ) );
            //*((int *)net_message.data) = BigLong(NETFLAG_CTL | (net_message.cursize & NETFLAG_LENGTH_MASK));
            _network.LanDriver.Write( newsock, _network.Message.Data, _network.Message.Length, sendaddr );
            _network.Message.Clear();
        }

        private void CloseSocketAndError( qsocket_t sock, Socket newsock )
		{
            _network.FreeSocket( sock );
            CloseNewSocketAndError( newsock );
        }

        private void CloseNewSocketAndError( Socket newsock )
        {
            _network.LanDriver.CloseSocket( newsock );

            if ( _menus.ReturnOnError && _menus.ReturnMenu != null )
            {
                _menus.ReturnMenu.Show( );
                _menus.ReturnOnError = false;
            }
        }

        private qsocket_t InternalSetupSocket( String host, out EndPoint sendaddr, out Socket newsock )
        {
            // see if we can resolve the host name
            sendaddr = _network.LanDriver.GetAddrFromName( host );

            if ( sendaddr == null )
            {
                newsock = null;
                return null;
            }

            newsock = _network.LanDriver.OpenSocket( 0 );

            if ( newsock == null )
                return null;

            var sock = _network.NewSocket();

            if ( sock == null )
            {
                CloseNewSocketAndError( newsock );
                return null;
            }

            sock.socket = newsock;
            sock.landriver = _network.LanDriverLevel;

            return sock;
        }

        private Int32 InternalAttemptConnection( qsocket_t sock, Socket newsock, EndPoint sendaddr )
        {
            // send the connection request
            _logger.Print( "Connecting to " + sendaddr + "\n" );
            _logger.Print( "trying...\n" );
            _screen.UpdateScreen();
            var start_time = _network.Time;
            var ret = 0;
            for ( var reps = 0; reps < 3; reps++ )
            {
                WriteInternalConnectHeader( newsock, sendaddr );
                EndPoint readaddr = new IPEndPoint( IPAddress.Any, 0 );
                do
                {
                    ret = _network.Message.FillFrom( _network, newsock, ref readaddr );
                    // if we got something, validate it
                    if ( ret > 0 )
                    {
                        // is it from the right place?
                        if ( sock.LanDriver.AddrCompare( readaddr, sendaddr ) != 0 )
                        {
#if DEBUG
                            _logger.Print( "wrong reply address\n" );
                            _logger.Print( "Expected: {0}\n", StrAddr( sendaddr ) );
                            _logger.Print( "Received: {0}\n", StrAddr( readaddr ) );
                            _screen.UpdateScreen();
#endif
                            ret = 0;
                            continue;
                        }

                        if ( ret < sizeof( Int32 ) )
                        {
                            ret = 0;
                            continue;
                        }

                        _network.Reader.Reset();

                        var control = EndianHelper.BigLong( _network.Reader.ReadLong() );// BigLong(*((int *)net_message.data));
                        //MSG_ReadLong();
                        if ( control == -1 )
                        {
                            ret = 0;
                            continue;
                        }
                        if ( ( control & ( ~NetFlags.NETFLAG_LENGTH_MASK ) ) != NetFlags.NETFLAG_CTL )
                        {
                            ret = 0;
                            continue;
                        }
                        if ( ( control & NetFlags.NETFLAG_LENGTH_MASK ) != ret )
                        {
                            ret = 0;
                            continue;
                        }
                    }
                }
                while ( ( ret == 0 ) && ( _network.SetNetTime() - start_time ) < 2.5 );
                if ( ret > 0 )
                    break;

                _logger.Print( "still trying...\n" );
                _screen.UpdateScreen();
                start_time = _network.SetNetTime();
            }

            return ret;
        }

        /// <summary>
        /// _Datagram_Connect
        /// </summary>
        private qsocket_t InternalConnect( String host )
        {
            var sock = InternalSetupSocket( host, out var sendaddr, out var newsock );

            if ( sock == null )
                return null;

            // connect to the host
            if ( _network.LanDriver.Connect( newsock, sendaddr ) == -1 )
			{
                CloseSocketAndError( sock, newsock );
                return null;
            }

            // send the connection request
            var ret = InternalAttemptConnection( sock, newsock, sendaddr );
            var reason = String.Empty;
            if ( ret == 0 )
            {
                reason = "No Response";
                _logger.Print( "{0}\n", reason );
                _menus.ReturnReason = reason;
                CloseSocketAndError( sock, newsock );
                return null;
            }

            if ( ret == -1 )
            {
                reason = "Network Error";
                _logger.Print( "{0}\n", reason );
                _menus.ReturnReason = reason;
                CloseSocketAndError( sock, newsock );
                return null;
            }

            ret = _network.Reader.ReadByte( );
            if ( ret == CCRep.CCREP_REJECT )
            {
                reason = _network.Reader.ReadString( );
                _logger.Print( reason );
                _menus.ReturnReason = reason;
                CloseSocketAndError( sock, newsock );
                return null;
            }

            if ( ret == CCRep.CCREP_ACCEPT )
            {
                var ep = ( IPEndPoint ) sendaddr;
                sock.addr = new IPEndPoint( ep.Address, ep.Port );
                _network.LanDriver.SetSocketPort( sock.addr, _network.Reader.ReadLong( ) );
            }
            else
            {
                reason = "Bad Response";
                _logger.Print( "{0}\n", reason );
                _menus.ReturnReason = reason;
                CloseSocketAndError( sock, newsock );
                return null;
            }

            sock.address = _network.LanDriver.GetNameFromAddr( sendaddr );

            _logger.Print( "Connection accepted\n" );
            sock.lastMessageTime = _network.SetNetTime( );

            // switch the connection to the specified address
            if ( _network.LanDriver.Connect( newsock, sock.addr ) == -1 )
            {
                reason = "Connect to Game failed";
                _logger.Print( "{0}\n", reason );
                _menus.ReturnReason = reason;
                CloseSocketAndError( sock, newsock );
                return null;
            }

            _menus.ReturnOnError = false;
            return sock;
        }

        /// <summary>
        /// SendMessageNext
        /// </summary>
        private Int32 SendMessageNext( qsocket_t sock )
        {
            Int32 dataLen;
            Int32 eom;
            if ( sock.sendMessageLength <= QDef.MAX_DATAGRAM )
            {
                dataLen = sock.sendMessageLength;
                eom = NetFlags.NETFLAG_EOM;
            }
            else
            {
                dataLen = QDef.MAX_DATAGRAM;
                eom = 0;
            }
            var packetLen = NetworkDef.NET_HEADERSIZE + dataLen;

            PacketHeader header;
            header.length = EndianHelper.BigLong( packetLen | ( NetFlags.NETFLAG_DATA | eom ) );
            header.sequence = EndianHelper.BigLong( ( Int32 ) sock.sendSequence++ );
            Utilities.StructureToBytes( ref header, _PacketBuffer, 0 );
            Buffer.BlockCopy( sock.sendMessage, 0, _PacketBuffer, PacketHeader.SizeInBytes, dataLen );

            sock.sendNext = false;

            if ( sock.Write( _PacketBuffer, packetLen, sock.addr ) == -1 )
				return -1;

			sock.lastSendTime = _network.Time;
            packetsSent++;
            return 1;
        }

        /// <summary>
        /// ReSendMessage
        /// </summary>
        private Int32 ReSendMessage( qsocket_t sock )
        {
            Int32 dataLen, eom;
            if ( sock.sendMessageLength <= QDef.MAX_DATAGRAM )
            {
                dataLen = sock.sendMessageLength;
                eom = NetFlags.NETFLAG_EOM;
            }
            else
            {
                dataLen = QDef.MAX_DATAGRAM;
                eom = 0;
            }
            var packetLen = NetworkDef.NET_HEADERSIZE + dataLen;

            PacketHeader header;
            header.length = EndianHelper.BigLong( packetLen | ( NetFlags.NETFLAG_DATA | eom ) );
            header.sequence = EndianHelper.BigLong( ( Int32 ) ( sock.sendSequence - 1 ) );
            Utilities.StructureToBytes( ref header, _PacketBuffer, 0 );
            Buffer.BlockCopy( sock.sendMessage, 0, _PacketBuffer, PacketHeader.SizeInBytes, dataLen );

            sock.sendNext = false;

            if ( sock.Write( _PacketBuffer, packetLen, sock.addr ) == -1 )
				return -1;

			sock.lastSendTime = _network.Time;
            packetsReSent++;
            return 1;
        }
    }
}
