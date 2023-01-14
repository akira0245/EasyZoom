using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin;
using DalamudApi;

namespace EasyZoom
{
	public unsafe class Plugin : IDalamudPlugin
	{
		public static Configuration config;
		private PluginUI ui;

		internal static CameraManager* cameraManager = (CameraManager*)FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance;
        private static IntPtr CamCollisionJmp;
		private static IntPtr CamDistanceResetFunc;
		private static byte[] CamDistanceOriginalBytes = new byte[8];

        public static float zoomDelta = 0.75f;
        private delegate float GetZoomDeltaDelegate();
        private static Hook<GetZoomDeltaDelegate> GetZoomDeltaHook;
        private static float GetZoomDeltaDetour()
        {
            return cam->CurrentZoom * 0.075f;
        }

        public string Name => "EasyZoom";

		public unsafe Plugin(DalamudPluginInterface pluginInterface)
		{
			api.Initialize(this, pluginInterface);

            var vtbl = cameraManager->WorldCamera->VTable;
            GetZoomDeltaHook = new(vtbl[28], GetZoomDeltaDetour); // Client__Game__Camera_vf28
            GetZoomDeltaHook.Enable();


            ZeroFloat = Marshal.AllocHGlobal(4);
			Marshal.StructureToPtr(0f, ZeroFloat, true);
			MaxFloat = Marshal.AllocHGlobal(4);
			Marshal.StructureToPtr(10000f, MaxFloat, true);
			PiFloat = Marshal.AllocHGlobal(4);
			Marshal.StructureToPtr((float)Math.PI, PiFloat, true);

            config = (Configuration)pluginInterface.GetPluginConfig() ?? new Configuration();
			config.Initialize(pluginInterface);

			this.ui = new PluginUI();
			pluginInterface.UiBuilder.Draw += this.ui.Draw;

			CamCollisionJmp = api.SigScanner.ScanText("0F 84 ?? ?? ?? ?? F3 0F 10 54 24 70 41 B7 01 F3 0F 10 44 24 74");
			CamDistanceResetFunc = api.SigScanner.ScanText("F3 0F 10 05 ?? ?? ?? ?? EB ?? F3 0F 10 05 ?? ?? ?? ?? F3 0F 10 94 24 B0 00 00 00"); // nop 8 bytes
			Marshal.Copy(CamDistanceResetFunc, CamDistanceOriginalBytes, 0, 8);
            
			api.ClientState.Login += ClientState_OnLogin;

			SetCamDistanceNoReset(true);
			if (config.NoCollision)
			{
				SetCamNoCollision(true);
			}

			Marshal.StructureToPtr(-1.569f, AngleMin, true);
			Marshal.StructureToPtr(1.569f, AngleMax, true);

			Marshal.StructureToPtr(config.FovMin, FovMin, true);
			Marshal.StructureToPtr(config.FovMax, FovMax, true);
			Marshal.StructureToPtr(config.ZoomMin, ZoomMin, true);
			Marshal.StructureToPtr(config.ZoomMax, ZoomMax, true);
		}

		private void ClientState_OnLogin(object sender, EventArgs e)
		{
			SetCamDistanceNoReset(true);
			if (config.NoCollision)
			{
				SetCamNoCollision(true);
			}

			Marshal.StructureToPtr(-1.569f, AngleMin, true);
			Marshal.StructureToPtr(1.569f, AngleMax, true);

			Marshal.StructureToPtr(config.FovMin, FovMin, true);
			Marshal.StructureToPtr(config.FovMax, FovMax, true);
			Marshal.StructureToPtr(config.ZoomMin, ZoomMin, true);
			Marshal.StructureToPtr(config.ZoomMax, ZoomMax, true);
		}
        private static GameCamera* cam => cameraManager->WorldCamera;

        public static IntPtr ZoomCurrent => (IntPtr)(&cam->CurrentZoom);
		public static IntPtr ZoomMin => (IntPtr)(&cam->MinZoom);
		public static IntPtr ZoomMax => (IntPtr)(&cam->MaxZoom);
		public static IntPtr FovCurrent => (IntPtr)(&cam->MaxFoV);
		public static IntPtr FovMin => (IntPtr)(&cam->MinFoV);
		public static IntPtr FovMax => (IntPtr)(&cam->CurrentFoV);
        public static IntPtr AngleMin => (IntPtr)(&cam->MinVRotation);
        public static IntPtr AngleMax => (IntPtr)(&cam->MaxVRotation);

        //public static IntPtr UpDown;
        //public static IntPtr UpDownMin;
        //public static IntPtr UpDownMax;



        public static IntPtr ZeroFloat;
		public static IntPtr PiFloat;
		public static IntPtr MaxFloat;

		internal static void SetCamDistanceNoReset(bool on)
		{
			Dalamud.SafeMemory.WriteBytes(CamDistanceResetFunc, @on ? Enumerable.Repeat((byte)0x90, 8).ToArray() : CamDistanceOriginalBytes);
		}

		internal static void SetCamNoCollision(bool on)
		{
			Dalamud.SafeMemory.WriteBytes(CamCollisionJmp, on ? new byte[] { 0x90, 0xE9 } : new byte[] { 0x0f, 0x84 });
		}


		[Command("/ezoom")]
		[HelpMessage("/ezoom: open config window.\n/ezoom <max/min/current> <value>: set max/min/current zoom\n/ezoom nocollision <on/off/toggle>: set camera collision\n/ezoom reset: reset camera settings.")]
		public void ExampleCommand1(string command, string args)
		{
			if (string.IsNullOrWhiteSpace(args))
			{
				ui.IsVisible ^= true;
			}
			else
			{
				args = args.ToLower().Trim();
				if (args == "reset")
				{
					ResetFovs();
					ResetZooms(true);
					api.ChatGui.Print($"[EasyZoom] Camera settings has been reset.");
				}
				else
				{
					var argarray = args.Split(' ');

					if (argarray.Length == 2)
					{
						if (argarray[0] == "max")
						{
							if (float.TryParse(argarray[1], out var value))
							{
								Marshal.StructureToPtr(value, ZoomMax, true);
								config.ZoomMax = value;
								config.Save();
								api.ChatGui.Print($"[EasyZoom] Max zoom {value}");
							}
						}
						if (argarray[0] == "min")
						{
							if (float.TryParse(argarray[1], out var value))
							{
								Marshal.StructureToPtr(value, ZoomMin, true);
								config.ZoomMin = value;
								config.Save();
								api.ChatGui.Print($"[EasyZoom] Min zoom {value}");
							}
						}
						if (argarray[0] == "current")
						{
							if (float.TryParse(argarray[1], out var value))
							{
								Marshal.StructureToPtr(value, ZoomCurrent, true);
								api.ChatGui.Print($"[EasyZoom] Current zoom {value}");
							}
						}
						if (argarray[0] == "nocollision")
						{
							if (argarray[1] == "on")
							{
								if (!config.NoCollision)
								{
									SetCamNoCollision(true);
									config.NoCollision = true;
									config.Save();
								}
							}
							else if (argarray[1] == "off")
							{
								if (config.NoCollision)
								{
									SetCamNoCollision(false);
									config.NoCollision = false;
									config.Save();
								}
							}
							else if (argarray[1] == "toggle")
							{
								SetCamNoCollision(!config.NoCollision);
								config.NoCollision ^= true;
								config.Save();
							}
							api.ChatGui.Print($"[EasyZoom] Camera collision {(config.NoCollision ? "disabled" : "enabled")}.");
						}
					}

				}
			}
		}

		#region IDisposable Support
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			api.ClientState.Login -= ClientState_OnLogin;

			SetCamDistanceNoReset(false);
			SetCamNoCollision(false);

			ResetFovs();
			ResetZooms();
			Marshal.StructureToPtr(Configuration.AngleMinDefault, AngleMin, true);
			Marshal.StructureToPtr(Configuration.AngleMaxDefault, AngleMax, true);


            api.PluginInterface.SavePluginConfig(config);

			api.PluginInterface.UiBuilder.Draw -= this.ui.Draw;
			Marshal.FreeHGlobal(ZeroFloat);
			Marshal.FreeHGlobal(MaxFloat);
			Marshal.FreeHGlobal(PiFloat);
            GetZoomDeltaHook?.Dispose();

            api.Dispose();
		}

		private static void ResetZooms(bool resetCurrent = false)
		{
			if (resetCurrent)
			{
				Marshal.StructureToPtr(Configuration.ZoomMaxDefault, ZoomCurrent, true);
			}
			Marshal.StructureToPtr(Configuration.ZoomMinDefault, ZoomMin, true);
			Marshal.StructureToPtr(Configuration.ZoomMaxDefault, ZoomMax, true);
		}

		private static void ResetFovs()
		{
			Marshal.StructureToPtr(Configuration.FovMinDefault, FovMin, true);
			Marshal.StructureToPtr(Configuration.FovMaxDefault, FovMax, true);
			Marshal.StructureToPtr(Configuration.FovMaxDefault, FovCurrent, true);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}


    /// <summary>
    /// https://github.com/UnknownX7/Cammy/blob/master/Structures/CameraManager.cs
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct CameraManager
    {
        [FieldOffset(0x0)] internal GameCamera* WorldCamera;
        [FieldOffset(0x8)] internal GameCamera* IdleCamera;
        [FieldOffset(0x10)] internal GameCamera* MenuCamera;
        [FieldOffset(0x18)] internal GameCamera* SpectatorCamera;
    }

    /// <summary>
    /// https://github.com/UnknownX7/Cammy/blob/master/Structures/GameCamera.cs
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct GameCamera
    {
        [FieldOffset(0x0)] public IntPtr* VTable;
        [FieldOffset(0x60)] public float X;
        [FieldOffset(0x64)] public float Z;
        [FieldOffset(0x68)] public float Y;
        [FieldOffset(0x90)] public float LookAtX; // Position that the camera is focused on (Actual position when zoom is 0)
        [FieldOffset(0x94)] public float LookAtZ;
        [FieldOffset(0x98)] public float LookAtY;
        [FieldOffset(0x114)] public float CurrentZoom; // 6
        [FieldOffset(0x118)] public float MinZoom; // 1.5
        [FieldOffset(0x11C)] public float MaxZoom; // 20
        [FieldOffset(0x120)] public float CurrentFoV; // 0.78
        [FieldOffset(0x124)] public float MinFoV; // 0.69
        [FieldOffset(0x128)] public float MaxFoV; // 0.78
        [FieldOffset(0x12C)] public float AddedFoV; // 0
        [FieldOffset(0x130)] public float CurrentHRotation; // -pi -> pi, default is pi
        [FieldOffset(0x134)] public float CurrentVRotation; // -0.349066
        //[FieldOffset(0x138)] public float HRotationDelta;
        [FieldOffset(0x148)] public float MinVRotation; // -1.483530, should be -+pi/2 for straight down/up but camera breaks so use -+1.569
        [FieldOffset(0x14C)] public float MaxVRotation; // 0.785398 (pi/4)
        [FieldOffset(0x160)] public float Tilt;
        [FieldOffset(0x170)] public int Mode; // Camera mode? (0 = 1st person, 1 = 3rd person, 2+ = weird controller mode? cant look up/down)
        //[FieldOffset(0x174)] public int ControlType; // 0 first person, 1 legacy, 2 standard, 3/5/6 ???, 4 ???
        [FieldOffset(0x17C)] public float InterpolatedZoom;
        [FieldOffset(0x1B0)] public float ViewX;
        [FieldOffset(0x1B4)] public float ViewZ;
        [FieldOffset(0x1B8)] public float ViewY;
        //[FieldOffset(0x1E4)] public byte FlipCamera; // 1 while holding the keybind
        [FieldOffset(0x224)] public float LookAtHeightOffset; // No idea what to call this (0x230 is the interpolated value)
        [FieldOffset(0x228)] public byte ResetLookatHeightOffset; // No idea what to call this
        //[FieldOffset(0x230)] public float InterpolatedLookAtHeightOffset;
        [FieldOffset(0x2B4)] public float LookAtZ2;
    }
}
