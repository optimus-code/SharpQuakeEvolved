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

using SharpQuake.Framework.IO;

namespace SharpQuake.Sys
{
    /// <summary>
    /// Global store for all cvar references
    /// </summary>
	public static class Cvars
	{
        // Input
		public static ClientVariable MouseFilter;// = { "m_filter", "0" };
       
        // Client
        public static ClientVariable Name;// = { "_cl_name", "player", true };
        public static ClientVariable Color;// = { "_cl_color", "0", true };
        public static ClientVariable ShowNet;// = { "cl_shownet", "0" };	// can be 0, 1, or 2
        public static ClientVariable NoLerp;// = { "cl_nolerp", "0" };
        public static ClientVariable LookSpring;// = { "lookspring", "0", true };
        public static ClientVariable LookStrafe;// = { "lookstrafe", "0", true };
        public static ClientVariable Sensitivity;// = { "sensitivity", "3", true };
        public static ClientVariable MPitch;// = { "m_pitch", "0.022", true };
        public static ClientVariable MYaw;// = { "m_yaw", "0.022", true };
        public static ClientVariable MForward;// = { "m_forward", "1", true };
        public static ClientVariable MSide;// = { "m_side", "0.8", true };
        public static ClientVariable UpSpeed;// = { "cl_upspeed", "200" };
        public static ClientVariable ForwardSpeed;// = { "cl_forwardspeed", "200", true };
        public static ClientVariable BackSpeed;// = { "cl_backspeed", "200", true };
        public static ClientVariable SideSpeed;// = { "cl_sidespeed", "350" };
        public static ClientVariable MoveSpeedKey;// = { "cl_movespeedkey", "2.0" };
        public static ClientVariable YawSpeed;// = { "cl_yawspeed", "140" };
        public static ClientVariable PitchSpeed;// = { "cl_pitchspeed", "150" };
        public static ClientVariable AngleSpeedKey;// = { "cl_anglespeedkey", "1.5" };
        public static ClientVariable AnimationBlend;

        // Network
        public static ClientVariable MessageTimeout; // = { "net_messagetimeout", "300" };
        public static ClientVariable HostName;

        // Server
        public static ClientVariable Friction;// = { "sv_friction", "4", false, true };
        public static ClientVariable EdgeFriction;// = { "edgefriction", "2" };
        public static ClientVariable StopSpeed;// = { "sv_stopspeed", "100" };
        public static ClientVariable Gravity;// = { "sv_gravity", "800", false, true };
        public static ClientVariable MaxVelocity;// = { "sv_maxvelocity", "2000" };
        public static ClientVariable NoStep;// = { "sv_nostep", "0" };
        public static ClientVariable MaxSpeed;// = { "sv_maxspeed", "320", false, true };
        public static ClientVariable Accelerate;// = { "sv_accelerate", "10" };
        public static ClientVariable Aim;// = { "sv_aim", "0.93" };
        public static ClientVariable IdealPitchScale;// = { "sv_idealpitchscale", "0.8" };

        // Chase View
        public static ClientVariable Back;// = { "chase_back", "100" };
        public static ClientVariable Up;// = { "chase_up", "16" };
        public static ClientVariable Right;// = { "chase_right", "0" };
        public static ClientVariable Active;// = { "chase_active", "0" };

        // Draw
        public static ClientVariable glNoBind; // = {"gl_nobind", "0"};
        public static ClientVariable glMaxSize; // = {"gl_max_size", "1024"};
        public static ClientVariable glPicMip;

        // Render
        public static ClientVariable NoRefresh;// = { "r_norefresh", "0" };
        public static ClientVariable DrawEntities;// = { "r_drawentities", "1" };
        public static ClientVariable DrawViewModel;// = { "r_drawviewmodel", "1" };
        public static ClientVariable Speeds;// = { "r_speeds", "0" };
        public static ClientVariable FullBright;// = { "r_fullbright", "0" };
        public static ClientVariable LightMap;// = { "r_lightmap", "0" };
        public static ClientVariable Shadows;// = { "r_shadows", "0" };
        //public CVar _MirrorAlpha;// = { "r_mirroralpha", "1" };
        public static ClientVariable WaterAlpha;// = { "r_wateralpha", "1" };
        public static ClientVariable NoiseGrain;// = { "r_noisegrain", "1" };
        public static ClientVariable Bloom;
        public static ClientVariable Dynamic;// = { "r_dynamic", "1" };
        public static ClientVariable NoVis;// = { "r_novis", "0" };
        public static ClientVariable glFinish;// = { "gl_finish", "0" };
        public static ClientVariable glClear;// = { "gl_clear", "0" };
        public static ClientVariable glCull;// = { "gl_cull", "1" };
        public static ClientVariable glTexSort;// = { "gl_texsort", "1" };
        public static ClientVariable glSmoothModels;// = { "gl_smoothmodels", "1" };
        public static ClientVariable glAffineModels;// = { "gl_affinemodels", "0" };
        public static ClientVariable glPolyBlend;// = { "gl_polyblend", "1" };
        public static ClientVariable glFlashBlend;// = { "gl_flashblend", "1" };
        public static ClientVariable glPlayerMip;// = { "gl_playermip", "0" };
        public static ClientVariable glNoColors;// = { "gl_nocolors", "0" };
        public static ClientVariable glKeepTJunctions;// = { "gl_keeptjunctions", "0" };
        public static ClientVariable glReportTJunctions;// = { "gl_reporttjunctions", "0" };
        public static ClientVariable glDoubleEyes;// = { "gl_doubleeys", "1" };

        // Screen
        public static ClientVariable ViewSize; // = { "viewsize", "100", true };
        public static ClientVariable Fov;// = { "fov", "90" };	// 10 - 170
        public static ClientVariable ConSpeed;// = { "scr_conspeed", "300" };
        public static ClientVariable CenterTime;// = { "scr_centertime", "2" };
        public static ClientVariable ShowRam;// = { "showram", "1" };
        public static ClientVariable ShowTurtle;// = { "showturtle", "0" };
        public static ClientVariable ShowPause;// = { "showpause", "1" };
        public static ClientVariable PrintSpeed;// = { "scr_printspeed", "8" };
        public static ClientVariable glTripleBuffer;// = { "gl_triplebuffer", "1", true };

        // Console
        public static ClientVariable NotifyTime; // con_notifytime = { "con_notifytime", "3" };		//seconds

        // Vid
        public static ClientVariable glZTrick;// = { "gl_ztrick", "1" };
        public static ClientVariable Mode;// = { "vid_mode", "0", false };
        // Note that 0 is MODE_WINDOWED
        public static ClientVariable DefaultMode;// = { "_vid_default_mode", "0", true };
        // Note that 3 is MODE_FULLSCREEN_DEFAULT
        public static ClientVariable DefaultModeWin;// = { "_vid_default_mode_win", "3", true };
        public static ClientVariable Wait;// = { "vid_wait", "0" };
        public static ClientVariable NoPageFlip;// = { "vid_nopageflip", "0", true };
        public static ClientVariable WaitOverride;// = { "_vid_wait_override", "0", true };
        public static ClientVariable ConfigX;// = { "vid_config_x", "800", true };
        public static ClientVariable ConfigY;// = { "vid_config_y", "600", true };
        public static ClientVariable StretchBy2;// = { "vid_stretch_by_2", "1", true };
        public static ClientVariable WindowedMouse;// = { "_windowed_mouse", "1", true };

        // View
        public static ClientVariable LcdX; // = { "lcd_x", "0" };
        public static ClientVariable LcdYaw; // = { "lcd_yaw", "0" };

        public static ClientVariable ScrOfsX; // = { "scr_ofsx", "0", false };
        public static ClientVariable ScrOfsY; // = { "scr_ofsy", "0", false };
        public static ClientVariable ScrOfsZ; // = { "scr_ofsz", "0", false };

        public static ClientVariable ClRollSpeed; // = { "cl_rollspeed", "200" };
        public static ClientVariable ClRollAngle; // = { "cl_rollangle", "2.0" };

        public static ClientVariable ClBob; // = { "cl_bob", "0.02", false };
        public static ClientVariable ClBobCycle; // = { "cl_bobcycle", "0.6", false };
        public static ClientVariable ClBobUp; // = { "cl_bobup", "0.5", false };

        public static ClientVariable KickTime; // = { "v_kicktime", "0.5", false };
        public static ClientVariable KickRoll; // = { "v_kickroll", "0.6", false };
        public static ClientVariable KickPitch; // = { "v_kickpitch", "0.6", false };

        public static ClientVariable IYawCycle; // = { "v_iyaw_cycle", "2", false };
        public static ClientVariable IRollCycle; // = { "v_iroll_cycle", "0.5", false };
        public static ClientVariable IPitchCycle;// = { "v_ipitch_cycle", "1", false };
        public static ClientVariable IYawLevel;// = { "v_iyaw_level", "0.3", false };
        public static ClientVariable IRollLevel;// = { "v_iroll_level", "0.1", false };
        public static ClientVariable IPitchLevel;// = { "v_ipitch_level", "0.3", false };

        public static ClientVariable IdleScale;// = { "v_idlescale", "0", false };

        public static ClientVariable Crosshair;// = { "crosshair", "0", true };
        public static ClientVariable ClCrossX;// = { "cl_crossx", "0", false };
        public static ClientVariable ClCrossY;// = { "cl_crossy", "0", false };

        public static ClientVariable glCShiftPercent;// = { "gl_cshiftpercent", "100", false };

        public static ClientVariable Gamma;// = { "gamma", "1", true };
        public static ClientVariable CenterMove;// = { "v_centermove", "0.15", false };
        public static ClientVariable CenterSpeed;// = { "v_centerspeed", "500" };

        // Sound
        public static ClientVariable BgmVolume;
        public static ClientVariable Volume;
        public static ClientVariable NoSound;
        public static ClientVariable Precache;
        public static ClientVariable LoadAs8bit;
        public static ClientVariable BgmBuffer;
        public static ClientVariable AmbientLevel;
        public static ClientVariable AmbientFade;
        public static ClientVariable NoExtraUpdate;
        public static ClientVariable Show;
        public static ClientVariable MixAhead;

        // Common
        public static ClientVariable Registered;
        public static ClientVariable CmdLine;

        // Programs Edict
        public static ClientVariable NoMonsters;// = { "nomonsters", "0" };
        public static ClientVariable GameCfg;// = { "gamecfg", "0" };
        public static ClientVariable Scratch1;// = { "scratch1", "0" };
        public static ClientVariable Scratch2;// = { "scratch2", "0" };
        public static ClientVariable Scratch3;// = { "scratch3", "0" };
        public static ClientVariable Scratch4;// = { "scratch4", "0" };
        public static ClientVariable SavedGameCfg;// = { "savedgamecfg", "0", true };
        public static ClientVariable Saved1;// = { "saved1", "0", true };
        public static ClientVariable Saved2;// = { "saved2", "0", true };
        public static ClientVariable Saved3;// = { "saved3", "0", true };
        public static ClientVariable Saved4;// = { "saved4", "0", true };

        // Host
        public static ClientVariable SystemTickRate;
        public static ClientVariable Developer;
        public static ClientVariable FrameRate;
        public static ClientVariable HostSpeeds;
        public static ClientVariable ServerProfile;
        public static ClientVariable FragLimit;
        public static ClientVariable TimeLimit;
        public static ClientVariable TeamPlay;
        public static ClientVariable SameLevel;
        public static ClientVariable NoExit;
        public static ClientVariable Skill;
        public static ClientVariable Deathmatch;
        public static ClientVariable Coop;
        public static ClientVariable Pausable;
        public static ClientVariable Temp1;

        // Custom
        public static ClientVariable ShowFPS;
        public static ClientVariable TrueTypeFonts;
        public static ClientVariable NewUI;
    }
}
