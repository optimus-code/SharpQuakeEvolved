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

using Hardware.Info;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SharpQuake.Sys
{
    public class SystemInformation
    {
        private Process Process
        {
            get;
            set;
        } = Process.GetCurrentProcess( );

        readonly IHardwareInfo _hardwareInfo = new HardwareInfo( );

        public Double TotalRAM
        {
            get
            {
                return _hardwareInfo.MemoryStatus.TotalPhysical / 1024.0 / 1024.0;
            }
        }

        public Double AvailableRAM
        {
            get
            {
                return _hardwareInfo.MemoryStatus.AvailablePhysical / 1024.0 / 1024.0;
            }
        }

        public Double RAMUsed
        {
            get
            {
                Process.Refresh( );
                return Process.WorkingSet64 / 1024.0 / 1024.0;
            }
        }
        public Double TotalVRAM
        {
            get
            {
                return _videoController.AdapterRAM / 1024.0 / 1024.0;
            }
        }

        private readonly VideoController _videoController;

        public SystemInformation()
        {
            _hardwareInfo.RefreshVideoControllerList( );

            _videoController = _hardwareInfo.VideoControllerList.OrderByDescending( v => v.AdapterRAM ).FirstOrDefault( );
        }

        public override String ToString( )
        {
            var sb = new StringBuilder( );

            _hardwareInfo.RefreshMemoryStatus( );
           
            sb.AppendLine( "========System Information========" );
            
            sb.AppendLine( String.Format( "^9RAM: Available: ^0{0}^9 Total: ^0{1}", 
                ToFriendlyString( AvailableRAM ),
                ToFriendlyString( TotalRAM ) ) );

            sb.AppendLine( $"^9GPU: ^0{_videoController.Description}^9, VRAM: ^0{ToFriendlyString( TotalVRAM )}^9 Native resolution: (^0{_videoController.CurrentHorizontalResolution}x{_videoController.CurrentVerticalResolution}^9)^0" );

            sb.AppendLine( "==================================" );

            return sb.ToString( );
        }

        private String ToFriendlyString( Double mb )
        {
            if ( mb < 1 )
                return $"{(mb * 1024.0):N0} KB";
            else if ( mb < 1024 )
                return $"{mb:N0} MB";
            else
                return $"{(mb / 1024.0):N0} GB";
        }
    }
}
