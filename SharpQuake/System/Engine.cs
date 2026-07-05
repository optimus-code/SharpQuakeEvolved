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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpQuake.Desktop;
using SharpQuake.Extensions;
using SharpQuake.Framework;
using SharpQuake.Framework.IO.Input;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.Logging;
using SharpQuake.Game.Client;
using SharpQuake.Logging;
using SharpQuake.Networking.Client;
using SharpQuake.Networking.Server;
using SharpQuake.Rendering;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using SharpQuake.Factories.Rendering.UI;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Rendering.Cameras;
using SharpQuake.Sys.Programs;
using SharpQuake.Factories.Rendering;
using SharpQuake.Framework.Factories.IO.WAD;
using SharpQuake.Services;
using SharpQuake.Sys.Handlers;
using SharpQuake.Rendering.UI.Elements;

namespace SharpQuake.Sys
{
    /// <summary>
    /// Core engine class, handles initial setup and creates the game window.
    /// </summary>
    public class Engine : IDisposable, IEngine
    {
        /// <summary>
        /// Global variable to determine if in debug mode or not
        /// </summary>
        public Boolean IsDeveloper
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Global variable for whether we are in dedicated server mode
        /// </summary>
        public Boolean IsDedicated
        {
            get
            {
                return CommandLine.HasParam( "-dedicated" );
            }
        }

        public Boolean IsInitialised
        {
            get;
            private set;
        }

        public Boolean IsDisposing
        {
            get;
            private set;
        }

        public GameKind Game
        {
            get;
            private set;
        }

        // TODO - Find a better place for this
        public Boolean NoClipAngleHack
        {
            get;
            set;
        }

        public static Common Common
        {
            get;
            private set;
        }

        private static String DumpFilePath
        {
            get
            {
                return Path.Combine( AppDomain.CurrentDomain.BaseDirectory, "error.txt" );
            }
        }

        private IConsoleLogger Logger
        {
            get;
            set;
        }

        private IKeyboardInput Keyboard
        {
            get;
            set;
        }

        public IMouseInput Mouse
        {
            get;
            private set;
        }

        private Network Network
        {
            get;
            set;
        }

        private ClientState ClientState
        {
            get;
            set;
        }

        private ServerState ServerState
        {
            get;
            set;
        }

        private client Client
        {
            get;
            set;
        }

        private Server Server
        {
            get;
            set;
        }

        private ServiceProvider ServiceProvider
        {
            get;
            set;
        }

        private IGameRenderer GameRenderer
        {
            get;
            set;
        }

        private MenuFactory Menus
        {
            get;
            set;
        }

        private CommandFactory Commands
        {
            get;
            set;
        }

        private ClientVariableFactory CVars
        {
            get;
            set;
        }

        private Scr Screen
        {
            get;
            set;
        }

        private QuakeParameters LaunchParameters
        {
            get;
            set;
        }

        private ProgramsState ProgramsState
        {
            get;
            set;
        }

        private EngineThink EngineThink
        {
            get;
            set;
        }

        private MainWindow Window
        {
            get;
            set;
        }

        private int ErrorDepth
        {
            get;
            set;
        }

        public SystemInformation System
        {
            get;
            private set;
        } = new SystemInformation( );

        /// <summary>
        /// Returns the dynamic mode of the engine.
        /// </summary>
        /// <remarks>
        /// (E.g. Whether it's a dedicated server, listen server or client.)
        /// </remarks>
        public EngineMode Mode
        {
            get
            {
                var isDisconnected = ClientState.StaticData.state == cactive_t.ca_disconnected;
                var isDedicated = ClientState.StaticData.state == cactive_t.ca_dedicated;
                var isServer = ServerState.Data.active;

                if ( isDedicated )
                    return EngineMode.DedicatedServer;
                else if ( isServer )
                    return EngineMode.ListenServer;
                else if ( !isDisconnected )
                    return EngineMode.Client;
                else
                    return EngineMode.None;
            }
        }

        public Engine()
        {
        }

        /// <summary>
        /// Initialise command line arguments
        /// </summary>
        /// <param name="commandLineArgs"></param>
        /// <returns></returns>
        /// <exception cref="QuakeException"></exception>
        private void InitialiseCommandLine( String[] commandLineArgs )
        {
            var args2 = new String[commandLineArgs.Length + 1];
            args2[0] = String.Empty;
            commandLineArgs.CopyTo( args2, 1 );

            CommandLine.InitArgv( args2 );

            DetermineGame( );

            LaunchParameters = new QuakeParameters
            {
                basedir = AppDomain.CurrentDomain.BaseDirectory, //Application.StartupPath;
                argv = new String[CommandLine.Argc]
            };

            CommandLine.Args.CopyTo( LaunchParameters.argv, 0 );

            if ( CommandLine.HasParam( "-dedicated" ) )
                throw new QuakeException( "Dedicated server mode not supported!" );
        }

        private void DetermineGame()
        {
            Game = GameKind.StandardQuake;

            if ( CommandLine.HasParam( "-rogue" ) )
                Game = GameKind.Rogue;
            else if ( CommandLine.HasParam( "-hipnotic" ) )
                Game = GameKind.Hipnotic;
        }

        /// <summary>
        /// Run the engine
        /// </summary>
        /// <remarks>
        /// (Blocking)
        /// </remarks>
        /// <param name="commandLineArgs"></param>
        public void Run( String[] commandLineArgs )
        {
            if ( File.Exists( DumpFilePath ) )
                File.Delete( DumpFilePath );

            InitialiseCommandLine( commandLineArgs );

            var size = new Size( 1280, 720 );

            Window = new MainWindow( this, size, false );
            Window.OnFrame += Think;

            ConfigureServices( );

            Logger.DPrint( "Host.Init\n" );

            Initialise( );

            Window.CursorVisible = false; //Hides mouse cursor during main menu on start up
            Window.Run( );
        }

        /// <summary>
        /// Dispose the engine and associated resources
        /// </summary>
        public void Dispose( )
        {
            if ( IsDisposing ) // Prevent multiple executions
                return;

            IsDisposing = true;

            // keep Con_Printf from trying to update the screen
            //Get<VideoState>().IsScreenDisabledForLoading = true;

            WriteConfiguration( );

            // Calling this ensures all the DI'ed services which are
            // disposable are cleaned up appropriately.
            ServiceProvider?.Dispose( );
        }


        /// <summary>
        /// Host_WriteConfiguration
        /// Writes key bindings and archived cvars to config.cfg
        /// </summary>
        private void WriteConfiguration( )
        {
            // dedicated servers initialize the host but don't parse and set the
            // config.cfg cvars
            if ( IsInitialised & !IsDedicated )
            {
                var path = Path.Combine( FileSystem.GameDir, "config.cfg" );

                using ( var fs = FileSystem.OpenWrite( path, true ) )
                {
                    if ( fs != null )
                    {
                        Keyboard.WriteBindings( fs );
                        CVars.WriteVariables( fs );
                    }
                }
            }
        }

        /// <summary>
        /// Host_ReadConfiguration
        /// Reads key bindings and archived cvars to config.cfg
        /// </summary>
        private void ReadConfiguration( )
        {            
            var path = Path.Combine( FileSystem.GameDir, "config.cfg" );

            using ( var fs = FileSystem.OpenRead( path ) )
            {
                if ( fs != null )
                {
                    byte[] data;

                    using ( var ms = new MemoryStream( ) )
                    {
                        fs.CopyTo( ms );
                        data = ms.ToArray( );
                    }

                    using ( var bindingsStream = new MemoryStream( data ) )
                    {
                        Keyboard.ReadBindings( bindingsStream );
                    }

                    using ( var variablesStream = new MemoryStream( data ) )
                    {
                        CVars.ReadVariables( variablesStream );
                    }
                }
            }
        }

        /// <summary>
        /// Dump an exception to the error log
        /// </summary>
        /// <param name="ex"></param>
        public static void DumpError( Exception ex )
        {
            try
            {
                var fs = new FileStream( DumpFilePath, FileMode.Append, FileAccess.Write, FileShare.Read );
                using ( var writer = new StreamWriter( fs ) )
                {
                    writer.WriteLine( );

                    var ex1 = ex;
                    while ( ex1 != null )
                    {
                        writer.WriteLine( "[" + DateTime.Now.ToString( ) + "] Unhandled exception:" );
                        writer.WriteLine( ex1.Message );
                        writer.WriteLine( );
                        writer.WriteLine( "Stack trace:" );
                        writer.WriteLine( ex1.StackTrace );
                        writer.WriteLine( );

                        ex1 = ex1.InnerException;
                    }
                }
            }
            catch ( Exception )
            {
            }
        }

        /// <summary>
        /// Safely shut down the game, dumping the contents of any error
        /// encountered.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void SafeShutdown( )
        {
            try
            {
                Dispose( );
            }
            catch ( Exception ex )
            {                
                DumpError( ex );

                if ( Debugger.IsAttached )
                    throw new Exception( "Exception in SafeShutdown()!", ex );
            }
        }

        /// <summary>
        /// Sys_Quit
        /// </summary>
        public void Quit( )
        {
            Window?.Exit( );
            Dispose( );

            // FIX ME - Something is holding up the process and not cleanly disposing
            Environment.Exit( 0 );
        }

        public void Quit_f( CommandMessage msg )
        {
            if ( Keyboard.Destination != KeyDestination.key_console && ClientState.StaticData.state != cactive_t.ca_dedicated )
            {
                Menus.Show( "menu_quit" );
                return;
            }

            Client.Disconnect( );
            ShutdownServer( false );
            Quit( );
        }

        /// <summary>
        /// Configure DI services
        /// </summary>
        /// <param name="onAddServices">Callback for adding services</param>
        private void ConfigureServices( Action<IServiceCollection> onAddServices = null )
        {
            var services = new ServiceCollection( )
                // Main Window needs to be here??
                .AddSingleton( Window )
                .AddSingleton( ( IEngine ) this )
                .AddSingleton( LaunchParameters ) // Add QuakeParameters here so we don't have to pass references around
                .AddSingleton<ICache, Cache>( )
                .AddSingleton<IConsoleLogger, ConsoleLogger>( )
                .AddSingleton<Common>( )
                .AddSingleton<EngineThink>( )
                .AddSingleton<MemoryHandler>( )
                .AddNetworking( )
                .AddFactories( )
                .AddVisual( )
                .AddSound( )
                .AddInput( )
                .AddQuakeC( )
                .AddServices( );

            onAddServices?.Invoke( services );

            ServiceProvider = services.BuildServiceProvider( );
           
            // Move elsewhere - these are things that need to be eager loaded
            ClientState = Get<ClientState>( );
            ServerState = Get<ServerState>( );
            Client = Get<client>( );
            Server = Get<Server>( );
            Keyboard = Get<IKeyboardInput>( );
            Mouse = Get<IMouseInput>( );
            Logger = Get<IConsoleLogger>( );
            GameRenderer = Get<IGameRenderer>( );
            Menus = Get<MenuFactory>( );
            Commands = Get<CommandFactory>( );
            CVars = Get<ClientVariableFactory>( );
            Screen = Get<Scr>( );
            Common = Get<Common>( );
            ProgramsState = Get<ProgramsState>( );
            EngineThink = Get<EngineThink>( );
            Network = Get<Network>( );

            _ = Get<IGameConsoleLogger>( );

            Window.Configure( Logger, Keyboard, Mouse );
        }

        private void InitialiseServices()
        {
            Get<IKeyboardInput>( ).Initialise( );
            Get<IMouseInput>( ).Initialise( );

            Commands.Add( "quit", Quit_f );
        }

        /// <summary>
        /// Get a DI service
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public TService Get<TService>()
            where TService : class
        {
            var service = ServiceProvider?.GetService<TService>();

            ArgumentNullException.ThrowIfNull( service, typeof( TService ).FullName );
            
            return service;
        }

        /// <summary>
        /// Get a DI service
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public object Get( Type type )
        {
            return ServiceProvider?.GetService( type );
        }

        /// <summary>
        /// Instead of calling service.AddHostedService<T> you call this make sure that you can also access the hosted service by interface TImplementation
        /// https://stackoverflow.com/a/64689263/619465
        /// </summary>
        /// <param name="services">The service collection</param>
        private void AddService<TService, TImplementation>( IServiceCollection services )
            where TService : class
            where TImplementation : class, IHostedService, TService
        {
            services.AddSingleton<TImplementation>( );
            services.AddSingleton<IHostedService>( provider => provider.GetRequiredService<TImplementation>( ) );
            services.AddSingleton<TService>( provider => provider.GetRequiredService<TImplementation>( ) );
        }

        /// <summary>
        /// host_Error
        /// This shuts down both the client and server
        /// </summary>
        public void Error( string error, params object[] args )
        {
            ErrorDepth++;
            try
            {
                if ( ErrorDepth > 1 )
                    Utilities.Error( "host_Error: recursively entered. " + error, args );

                Screen.EndLoadingPlaque( );		// reenable screen updates

                var message = args.Length > 0 ? string.Format( error, args ) : error;
                Logger.Print( "host_Error: {0}\n", message );

                if ( ServerState.Data.active )
                    ShutdownServer( false );

                if ( ClientState.StaticData.state == cactive_t.ca_dedicated )
                    Utilities.Error( "host_Error: {0}\n", message );	// dedicated servers exit

                Client.Disconnect( );
                ClientState.StaticData.demonum = -1;

                throw new EndGameException( ); // longjmp (host_old_abortserver, 1);
            }
            finally
            {
                ErrorDepth--;
            }
        }

        /// Host_ShutdownServer
        /// This only happens at the end of a game, not between levels
        /// </summary>
        public void ShutdownServer( Boolean crash )
        {
            if ( !ServerState.IsActive )
                return;

            ServerState.Data.active = false;

            // stop all client sounds immediately
            if ( ClientState.StaticData.state == cactive_t.ca_connected )
                Client.Disconnect( );

            // flush any pending messages - like the score!!!
            var start = Timer.GetFloatTime( );
            Int32 count;
            do
            {
                count = 0;
                for ( var i = 0; i < ServerState.StaticData.maxclients; i++ )
                {
                    var client = ServerState.StaticData.clients[i];
                    if ( client.active && !client.message.IsEmpty )
                    {
                        if ( Network.CanSendMessage( client.netconnection ) )
                        {
                            Network.SendMessage( client.netconnection, client.message );
                            client.message.Clear( );
                        }
                        else
                        {
                            Network.GetMessage( client.netconnection );
                            count++;
                        }
                    }
                }
                if ( ( Timer.GetFloatTime( ) - start ) > 3.0 )
                    break;
            }
            while ( count > 0 );

            // make sure all the clients know we're disconnecting
            var writer = new MessageWriter( 4 );
            writer.WriteByte( ProtocolDef.svc_disconnect );
            count = Network.SendToAll( writer, 5 );

            if ( count != 0 )
                Logger.Print( "Host_ShutdownServer: NET_SendToAll failed for {0} clients\n", count );

            for ( var i = 0; i < ServerState.StaticData.maxclients; i++ )
            {
                var client = ServerState.StaticData.clients[i];

                if ( client.active )
                    Server.DropClient( client, crash );
            }

            //
            // clear structures
            //
            ServerState.Data.Clear( );

            for ( var i = 0; i < ServerState.StaticData.clients.Length; i++ )
                ServerState.StaticData.clients[i].Clear( );
        }

        /// <summary>
        /// host_EndGame
        /// </summary>
        public void EndGame( String message, params Object[] args )
        {
            var str = String.Format( message, args );
            Logger.DPrint( "host_old_EndGame: {0}\n", str );

            if ( ServerState.IsActive )
                ShutdownServer( false );

            if ( ClientState.StaticData.state == cactive_t.ca_dedicated )
                Utilities.Error( "host_old_EndGame: {0}\n", str );	// dedicated servers exit

            if ( ClientState.StaticData.demonum != -1 )
                Client.NextDemo( );
            else
                Client.Disconnect( );

            throw new EndGameException( );  //longjmp (host_old_abortserver, 1);
        }

        /// <summary>
        /// Host_FindMaxClients
        /// </summary>
        private void FindMaxClients( )
        {
            var svs = ServerState.StaticData;
            var cls = ClientState.StaticData;

            svs.maxclients = 1;

            var i = CommandLine.CheckParm( "-dedicated" );
            if ( i > 0 )
            {
                cls.state = cactive_t.ca_dedicated;
                if ( i != ( CommandLine.Argc - 1 ) )
                {
                    svs.maxclients = MathLib.atoi( CommandLine.Argv( i + 1 ) );
                }
                else
                    svs.maxclients = 8;
            }
            else
                cls.state = cactive_t.ca_disconnected;

            i = CommandLine.CheckParm( "-listen" );
            if ( i > 0 )
            {
                if ( cls.state == cactive_t.ca_dedicated )
                    Utilities.Error( "Only one of -dedicated or -listen can be specified" );
                if ( i != ( CommandLine.Argc - 1 ) )
                    svs.maxclients = MathLib.atoi( CommandLine.Argv( i + 1 ) );
                else
                    svs.maxclients = 8;
            }
            if ( svs.maxclients < 1 )
                svs.maxclients = 8;
            else if ( svs.maxclients > QDef.MAX_SCOREBOARD )
                svs.maxclients = QDef.MAX_SCOREBOARD;

            svs.maxclientslimit = svs.maxclients;
            if ( svs.maxclientslimit < 4 )
                svs.maxclientslimit = 4;
            svs.clients = new client_t[svs.maxclientslimit]; // Hunk_AllocName (svs.maxclientslimit*sizeof(client_t), "clients");
            for ( i = 0; i < svs.clients.Length; i++ )
                svs.clients[i] = new client_t( );

            if ( svs.maxclients > 1 )
                CVars.Set( "deathmatch", 1 );
            else
                CVars.Set( "deathmatch", 0 );
        }

        // Host_InitLocal
        private void InitialiseLocal( )
        {
            if ( Cvars.SystemTickRate == null )
            {
                Cvars.SystemTickRate = CVars.Add( "sys_ticrate", 0.05 );
                Cvars.Developer = CVars.Add( "developer", false );
                Cvars.FrameRate = CVars.Add( "host_framerate", 0.0 ); // set for slow motion
                Cvars.HostSpeeds = CVars.Add( "host_speeds", false ); // set for running times
                Cvars.ServerProfile = CVars.Add( "serverprofile", false );
                Cvars.FragLimit = CVars.Add( "fraglimit", 0, ClientVariableFlags.Server );
                Cvars.TimeLimit = CVars.Add( "timelimit", 0, ClientVariableFlags.Server );
                Cvars.TeamPlay = CVars.Add( "teamplay", 0, ClientVariableFlags.Server );
                Cvars.SameLevel = CVars.Add( "samelevel", false );
                Cvars.NoExit = CVars.Add( "noexit", false, ClientVariableFlags.Server );
                Cvars.Skill = CVars.Add( "skill", 1 ); // 0 - 3
                Cvars.Deathmatch = CVars.Add( "deathmatch", 0 ); // 0, 1, or 2
                Cvars.Coop = CVars.Add( "coop", false );
                Cvars.Pausable = CVars.Add( "pausable", true );
                Cvars.Temp1 = CVars.Add( "temp1", 0 );
            }
            FindMaxClients( );

            Sys.Time.Initialise( ); // so a think at time 0 won't get called
        }

        public void Initialise( )
        {
            Logger.DPrint( System.ToString( ) ); // Print out system information

            Mouse.OnCheckMouseActive += () => Window.IsMouseActive;

            InitialiseServices( );

            Commands.Initialise( CVars );

            Get<View>( ).Initialise( );
            Get<ChaseView>( ).Initialise( );
            Get<VCRService>( ).Initialise( );
            Common.Initialise( );
            InitialiseLocal( );

            Get<WadFactory>( ).Initialise( );

            Keyboard.Initialise( );

            var gameLogger = Get<IGameConsoleLogger>( );
            gameLogger.Initialise( );

            Get<MenuFactory>( ).Initialise( );
            Get<ProgramsExec>( ).Initialise( );
            Get<ProgramsBuiltIn>( ).Initialise( );
            Get<ModelFactory>( ).Initialise( );
            Network.Initialise( );
            Server.Initialise( );
            Get<ServerCommands>( ).Initialise( );

            //Con.Print("Exe: "__TIME__" "__DATE__"\n");
            //Con.Print("%4.1f megabyte heap\n",parms->memsize/ (1024*1024.0));

            if ( ClientState.StaticData.state != cactive_t.ca_dedicated )
            {
                var gameRenderer = Get<IGameRenderer>( );
                gameRenderer.BasePal = FileSystem.LoadFile( "gfx/palette.lmp" );

                if ( gameRenderer.BasePal == null )
                    Utilities.Error( "Couldn't load gfx/palette.lmp" );

                gameRenderer.ColorMap = FileSystem.LoadFile( "gfx/colormap.lmp" );

                if ( gameRenderer.ColorMap == null )
                    Utilities.Error( "Couldn't load gfx/colormap.lmp" );

                // on non win32, mouse comes before video for security reasons
                Mouse.Initialise( );

                var vid = Get<Vid>( );

                vid.InitialiseClientVariables( );

                ReadConfiguration( );

                vid.Initialise( gameRenderer.BasePal );
                Get<Drawer>( ).Initialise( );
                Screen.Initialise( );
                gameLogger.InitialiseBackground( );
                Get<render>( ).Initialise( );
                Get<snd>( ).Initialise( );
                Get<cd_audio>( ).Initialise( );
                Screen.InitialiseHUD( );
                Client.Initialise( );
            }
            else
            {
                //DedicatedServer.Initialise( );
            }

            Commands.Buffer.Insert( "exec quake.rc\n" );

            IsInitialised = true;

            Logger.DPrint( "========Quake Initialized=========\n" );
        }

        private void Think( Double time )
        {
            EngineThink.FrameMain( time );
        }
    }
}
