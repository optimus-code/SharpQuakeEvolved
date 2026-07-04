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
using SharpQuake.Factories.Rendering.UI;
using SharpQuake.Factories.Rendering;
using SharpQuake.Framework.Factories.IO.WAD;
using SharpQuake.Framework.Factories.IO;
using SharpQuake.Rendering;
using SharpQuake.Desktop;
using SharpQuake.Services;
using SharpQuake.Sys.Programs;
using SharpQuake.Rendering.Cameras;
using SharpQuake.Networking;
using SharpQuake.Networking.Server;
using SharpQuake.Networking.Client;
using SharpQuake.Logging;

namespace SharpQuake.Extensions
{
    /// <summary>
    /// Service Collection extensions to help with DI configuration
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add sound interfaces
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddSound( this IServiceCollection services )
        {
            services
                .AddSingleton<snd>( )
                .AddSingleton<cd_audio>( );

            return services;
        }

        /// <summary>
        /// Add the input interfaces
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddInput( this IServiceCollection services )
        {
            services
                .AddSingleton<IKeyboardInput, KeyboardInput>( )
                .AddSingleton<IMouseInput, MouseInput>( );

            return services;
        }

        /// <summary>
        /// Add the visual interfaces
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddVisual( this IServiceCollection services )
        {
            services
                .AddSingleton<Vid>( )
                .AddSingleton<Scr>( )
                .AddSingleton<View>( )
                .AddSingleton<ChaseView>( )
                .AddSingleton<Drawer>( )
                .AddSingleton<IGameRenderer, GameRenderer>( )
                .AddSingleton<VideoState>( )
                .AddSingleton<render>( )
                .AddSingleton<RenderState>( )
                .AddUIElements( )
                .AddMenus( );

            return services;
        }

        /// <summary>
        /// Add the core networking interfaces
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddNetworking( this IServiceCollection services )
        {
            services
                .AddClient( )
                .AddServer( )
                .AddSingleton<Network>( )
                .AddSingleton<LocalHost>( )
                .AddNetworkProtocols( );

            return services;
        }

        /// <summary>
        /// Add the core networking interfaces
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private static IServiceCollection AddClient( this IServiceCollection services )
        {
            services
                .AddSingleton<client>( )
                .AddSingleton<ClientState>( );

            return services;
        }

        /// <summary>
        /// Add the core networking interfaces
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private static IServiceCollection AddServer( this IServiceCollection services )
        {
            services
                .AddSingleton<Server>( )
                .AddSingleton<ServerState>( )
                .AddSingleton<ServerMovement>( )
                .AddSingleton<ServerCommands>( )
                .AddSingleton<ServerPhysics>( )
                .AddSingleton<ServerWorld>( )
                .AddSingleton<ServerSound>( )
                .AddSingleton<ServerUser>( );

            return services;
        }
        /// <summary>
        /// Add networking protocols
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private static IServiceCollection AddNetworkProtocols( this IServiceCollection services )
        {
            services
                .AddSingleton<net_datagram>( )
                .AddSingleton<net_tcp_ip>( )
                .AddSingleton<net_loop>( )
                .AddSingleton<net_vcr>( );

            return services;
        }

        /// <summary>
        /// Add all factory classes to the service collection
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddFactories( this IServiceCollection services )
        {
            services
                .AddSingleton<CommandFactory>( )
                .AddSingleton<ClientVariableFactory>( )
                .AddSingleton<WadFactory>( )
                .AddSingleton<ModelFactory>( )
                .AddSingleton<MenuFactory>( )
                .AddSingleton<PictureFactory>( )
                .AddSingleton<ElementFactory>( );

            return services;
        }

        /// <summary>
        /// Add all UI elements to the service collection
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private static IServiceCollection AddUIElements( this IServiceCollection services )
        {
            foreach ( var type in ElementFactory.FACTORY_TYPES )
            {
                if ( typeof( IGameConsoleLogger ).IsAssignableFrom( type ) )
                    services.AddSingleton( typeof( IGameConsoleLogger ), type );
                else
                    services.AddSingleton( type );
            }

            return services;
        }

        /// <summary>
        /// Add all menus to the service collection
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private static IServiceCollection AddMenus( this IServiceCollection services )
        {
            foreach ( var type in MenuFactory.FACTORY_TYPES )
                services.AddSingleton( type );

            return services;
        }

        /// <summary>
        /// Adds the QuakeC programs interfaces
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddQuakeC( this IServiceCollection services )
        {
            services
                .AddSingleton<ProgramErrorHandler>( )
                .AddSingleton<ProgramsState>( )
                .AddSingleton<ProgramsBuiltIn>( )
                .AddSingleton<ProgramsEdict>( )
                .AddSingleton<ProgramsExec>( );

            return services;
        }

        /// <summary>
        /// Adds core services
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddServices( this IServiceCollection services )
        {
            services
                .AddSingleton<VCRService>( )
                .AddSingleton<SaveFileService>( );

            return services;
        }
    }
}
