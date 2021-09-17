using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using EasyZoom.Attributes;

namespace EasyZoom
{
	public class Plugin : IDalamudPlugin
	{
		public static DalamudPluginInterface pi;
		private PluginCommandManager<Plugin> commandManager;
		public static Configuration config;
		private PluginUI ui;

		private static IntPtr CamPtr;
		private static IntPtr CamCollisionJmp;
		private static IntPtr CamDistanceResetFunc;
		private static byte[] CamDistanceOriginalBytes = new byte[8];

		private static int ZoomOffset;
		private static int FovOffset;
		private static int AngleOffset;
		private static int UpDownOffset;

		public string Name => "EasyZoom";

		public unsafe void Initialize(DalamudPluginInterface pluginInterface)
		{
			ZeroFloat = Marshal.AllocHGlobal(4);
			Marshal.StructureToPtr(0f, ZeroFloat, true);
			MaxFloat = Marshal.AllocHGlobal(4);
			Marshal.StructureToPtr(10000f, MaxFloat, true);
			PiFloat = Marshal.AllocHGlobal(4);
			Marshal.StructureToPtr((float)Math.PI, PiFloat, true);
			//UpDownMin = Marshal.AllocHGlobal(4);
			//Marshal.StructureToPtr(-10f, UpDownMin, true);
			//UpDownMax = Marshal.AllocHGlobal(4);
			//Marshal.StructureToPtr(10f, UpDownMax, true);

			pi = pluginInterface;

			config = (Configuration)pluginInterface.GetPluginConfig() ?? new Configuration();
			config.Initialize(pluginInterface);

			this.ui = new PluginUI();
			pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

			this.commandManager = new PluginCommandManager<Plugin>(this, pluginInterface);

			var pf = pluginInterface.TargetModuleScanner;
			CamPtr = Marshal.ReadIntPtr(pf.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? 45 33 C0 33 D2 C6 40 09 01"));
			CamCollisionJmp = pf.ScanText("0F 84 ?? ?? ?? ?? F3 0F 10 54 24 60 F3 0F 10 44 24 64 F3 41 0F 5C D5");
			ZoomOffset = Marshal.ReadInt32(pf.ScanText("F3 0F ?? ?? ?? ?? ?? ?? 48 8B ?? ?? ?? ?? ?? 48 85 ?? 74 ?? F3 0F ?? ?? ?? ?? ?? ?? 48 83 C1") + 4);
			FovOffset = Marshal.ReadInt32(pf.ScanText("F3 0F ?? ?? ?? ?? ?? ?? 0F 2F ?? ?? ?? ?? ?? 72 ?? F3 0F ?? ?? ?? ?? ?? ?? 48 8B") + 4);
			AngleOffset = Marshal.ReadInt32(pf.ScanText("F3 0F 10 B3 ?? ?? ?? ?? 48 8D ?? ?? ?? F3 44 ?? ?? ?? ?? ?? ?? ?? F3 44") + 4);
			//UpDownOffset = Marshal.ReadInt32(pf.ScanText("F3 0F ?? ?? ?? ?? ?? ?? F3 44 ?? ?? ?? ?? ?? ?? ?? 0F 28 ?? F3 0F ?? ?? F3 41") + 4);
			CamDistanceResetFunc = pf.ScanText("F3 0F 10 05 ?? ?? ?? ?? EB ?? F3 0F 10 05 ?? ?? ?? ?? F3 0F 10 94 24 B0 00 00 00"); // nop 8 bytes
			Marshal.Copy(CamDistanceResetFunc, CamDistanceOriginalBytes, 0, 8);
			//Marshal.Copy(CamCollisionJmp, CamCollisionOriginalBytes, 0, 6);


			ZoomCurrent = CamPtr + ZoomOffset;
			ZoomMin = CamPtr + ZoomOffset + 4;
			ZoomMax = CamPtr + ZoomOffset + 8;

			FovCurrent = CamPtr + FovOffset;
			FovMin = CamPtr + FovOffset + 4;
			FovMax = CamPtr + FovOffset + 8;

			AngleMin = CamPtr + AngleOffset;
			AngleMax = CamPtr + AngleOffset + 4;

			//UpDown = CamPtr + UpDownOffset;

			pi.ClientState.OnLogin += ClientState_OnLogin;

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

		public static IntPtr ZoomCurrent;
		public static IntPtr ZoomMin;
		public static IntPtr ZoomMax;
		public static IntPtr FovCurrent;
		public static IntPtr FovMin;
		public static IntPtr FovMax;

		public static IntPtr AngleMin;
		public static IntPtr AngleMax;

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
					pi.Framework.Gui.Chat.Print($"[EasyZoom] Camera settings has been reset.");
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
								pi.Framework.Gui.Chat.Print($"[EasyZoom] Max zoom {value}");
							}
						}
						if (argarray[0] == "min")
						{
							if (float.TryParse(argarray[1], out var value))
							{
								Marshal.StructureToPtr(value, ZoomMin, true);
								config.ZoomMin = value;
								config.Save();
								pi.Framework.Gui.Chat.Print($"[EasyZoom] Min zoom {value}");
							}
						}
						if (argarray[0] == "current")
						{
							if (float.TryParse(argarray[1], out var value))
							{
								Marshal.StructureToPtr(value, ZoomCurrent, true);
								pi.Framework.Gui.Chat.Print($"[EasyZoom] Current zoom {value}");
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
							pi.Framework.Gui.Chat.Print($"[EasyZoom] Camera collision {(config.NoCollision ? "disabled" : "enabled")}.");
						}
					}

				}
			}
		}

		#region IDisposable Support
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			pi.ClientState.OnLogin -= ClientState_OnLogin;

			SetCamDistanceNoReset(false);
			SetCamNoCollision(false);

			ResetFovs();
			ResetZooms();
			Marshal.StructureToPtr(Configuration.AngleMinDefault, AngleMin, true);
			Marshal.StructureToPtr(Configuration.AngleMaxDefault, AngleMax, true);

			this.commandManager.Dispose();

			pi.SavePluginConfig(config);

			pi.UiBuilder.OnBuildUi -= this.ui.Draw;
			Marshal.FreeHGlobal(ZeroFloat);
			Marshal.FreeHGlobal(MaxFloat);
			Marshal.FreeHGlobal(PiFloat);
			//Marshal.FreeHGlobal(UpDownMin);
			//Marshal.FreeHGlobal(UpDownMax);
			pi.Dispose();
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
}
