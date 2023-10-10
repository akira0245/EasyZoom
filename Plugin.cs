using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudApi;

namespace EasyZoom
{
	public unsafe class Plugin : IDalamudPlugin
	{

        [PluginService]
        public static IGameInteropProvider sigScanner { get; private set; } = null!;

        public static Configuration config;
		private PluginUI ui;

		internal static CameraManager* cameraManager = (CameraManager*)FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance();
        private static IntPtr CamCollisionJmp;
		private static IntPtr CamDistanceResetFunc;
		private static byte[] CamDistanceOriginalBytes = new byte[8];

        

        public static float zoomDelta = 0.75f;
        private delegate float GetZoomDeltaDelegate();
        private static Hook<GetZoomDeltaDelegate> GetZoomDeltaHook;
        private static float GetZoomDeltaDetour()
        {
            return cam->currentZoom * 0.075f;
        }

        public string Name => "EasyZoom";

		public Plugin(DalamudPluginInterface pluginInterface)
		{
            api.Initialize(this, pluginInterface);

            SigScanner _si = new SigScanner();

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

			CamCollisionJmp = _si.ScanText("E8 ?? ?? ?? ?? 4C 8D 45 C7 89 83 ?? ?? ?? ??") + 0x1D4;
			CamDistanceResetFunc = _si.ScanText("F3 0F 10 05 ?? ?? ?? ?? EB ?? F3 0F 10 05 ?? ?? ?? ?? F3 0F 10 94 24 B0 00 00 00"); // nop 8 bytes
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

			hook();
		}

		private void hook()
		{
            var vtbl = cameraManager->worldCamera->vtbl;
            GetZoomDeltaHook = sigScanner.HookFromAddress<GetZoomDeltaDelegate>(vtbl[28], GetZoomDeltaDetour);
            GetZoomDeltaHook.Enable();
        }

		private void ClientState_OnLogin()
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
        private static GameCamera* cam => cameraManager->worldCamera;

        public static IntPtr ZoomCurrent => (IntPtr)(&cam->currentZoom);
		public static IntPtr ZoomMin => (IntPtr)(&cam->minZoom);
		public static IntPtr ZoomMax => (IntPtr)(&cam->maxZoom);
		public static IntPtr FovCurrent => (IntPtr)(&cam->maxFoV);
		public static IntPtr FovMin => (IntPtr)(&cam->minFoV);
		public static IntPtr FovMax => (IntPtr)(&cam->currentFoV);
        public static IntPtr AngleMin => (IntPtr)(&cam->minVRotation);
        public static IntPtr AngleMax => (IntPtr)(&cam->maxVRotation);


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
        [FieldOffset(0x0)] public GameCamera* worldCamera;
        [FieldOffset(0x8)] public GameCamera* idleCamera;
        [FieldOffset(0x10)] public GameCamera* menuCamera;
        [FieldOffset(0x18)] public GameCamera* spectatorCamera;
    }

    /// <summary>
    /// https://github.com/UnknownX7/Cammy/blob/master/Structures/GameCamera.cs
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct GameCamera
    {
        [FieldOffset(0x0)] public nint* vtbl;
        [FieldOffset(0x60)] public float x;
        [FieldOffset(0x64)] public float y;
        [FieldOffset(0x68)] public float z;
        [FieldOffset(0x90)] public float lookAtX; // Position that the camera is focused on (Actual position when zoom is 0)
        [FieldOffset(0x94)] public float lookAtY;
        [FieldOffset(0x98)] public float lookAtZ;
        [FieldOffset(0x114)] public float currentZoom; // 6
        [FieldOffset(0x118)] public float minZoom; // 1.5
        [FieldOffset(0x11C)] public float maxZoom; // 20
        [FieldOffset(0x120)] public float currentFoV; // 0.78
        [FieldOffset(0x124)] public float minFoV; // 0.69
        [FieldOffset(0x128)] public float maxFoV; // 0.78
        [FieldOffset(0x12C)] public float addedFoV; // 0
        [FieldOffset(0x130)] public float currentHRotation; // -pi -> pi, default is pi
        [FieldOffset(0x134)] public float currentVRotation; // -0.349066
        [FieldOffset(0x138)] public float hRotationDelta;
        [FieldOffset(0x148)] public float minVRotation; // -1.483530, should be -+pi/2 for straight down/up but camera breaks so use -+1.569
        [FieldOffset(0x14C)] public float maxVRotation; // 0.785398 (pi/4)
        [FieldOffset(0x160)] public float tilt;
        [FieldOffset(0x170)] public int mode; // Camera mode? (0 = 1st person, 1 = 3rd person, 2+ = weird controller mode? cant look up/down)
        [FieldOffset(0x174)] public int controlType; // 0 first person, 1 legacy, 2 standard, 4 talking to npc in first person (with option enabled), 5 talking to npc (with option enabled), 3/6 ???
        [FieldOffset(0x17C)] public float interpolatedZoom;
        [FieldOffset(0x190)] public float transition; // Seems to be related to the 1st <-> 3rd camera transition
        [FieldOffset(0x1B0)] public float viewX;
        [FieldOffset(0x1B4)] public float viewY;
        [FieldOffset(0x1B8)] public float viewZ;
        [FieldOffset(0x1E4)] public byte isFlipped; // 1 while holding the keybind
        [FieldOffset(0x21C)] public float interpolatedY;
        [FieldOffset(0x224)] public float lookAtHeightOffset; // No idea what to call this (0x230 is the interpolated value)
        [FieldOffset(0x228)] public byte resetLookatHeightOffset; // No idea what to call this
        [FieldOffset(0x230)] public float interpolatedLookAtHeightOffset;
        [FieldOffset(0x2B0)] public byte lockPosition;
        [FieldOffset(0x2C4)] public float lookAtY2;
    }
}
