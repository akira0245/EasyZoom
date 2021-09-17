using System;
using System.Runtime.InteropServices;
using ImGuiNET;
using static EasyZoom.Plugin;
using static EasyZoom.Configuration;

namespace EasyZoom
{
	public class PluginUI
	{
		public bool IsVisible;
		public void Draw()
		{
			if (!IsVisible)
				return;

			if (ImGui.Begin("EasyZoom", ref IsVisible, ImGuiWindowFlags.AlwaysAutoResize))
			{
				ImGui.SliderScalar("FOV", ImGuiDataType.Float, FovCurrent, FovMin, FovMax, $"{Marshal.PtrToStructure<float>(FovCurrent)} ({Marshal.PtrToStructure<float>(FovCurrent) * (180 / Math.PI):F2}°)");
				if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
					Marshal.StructureToPtr(FovDefault, FovCurrent, true);

				if (ImGui.DragScalar("FOV Min", ImGuiDataType.Float, FovMin, 0.005f, ZeroFloat, PiFloat, $"{Marshal.PtrToStructure<float>(FovMin)} ({Marshal.PtrToStructure<float>(FovMin) * (180 / Math.PI):F2}°)"))
				{
					config.FovMin = Marshal.PtrToStructure<float>(FovMin);
					config.Save();
				}
				if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
				{
					Marshal.StructureToPtr(FovMinDefault, FovMin, true);
					config.FovMin = Marshal.PtrToStructure<float>(FovMin);
					config.Save();
				}

				if (ImGui.DragScalar("FOV Max", ImGuiDataType.Float, FovMax, 0.005f, ZeroFloat, PiFloat, $"{Marshal.PtrToStructure<float>(FovMax)} ({Marshal.PtrToStructure<float>(FovMax) * (180 / Math.PI):F2}°)"))
				{
					config.FovMax = Marshal.PtrToStructure<float>(FovMax);
					config.Save();
				}
				if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
				{
					Marshal.StructureToPtr(FovMaxDefault, FovMax, true);
					config.FovMax = Marshal.PtrToStructure<float>(FovMax);
					config.Save();
				}

				ImGui.Spacing();

				ImGui.SliderScalar("Zoom", ImGuiDataType.Float, ZoomCurrent, ZoomMin, ZoomMax, Marshal.PtrToStructure<float>(ZoomCurrent).ToString(), ImGuiSliderFlags.Logarithmic);
				if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
					Marshal.StructureToPtr(ZoomDefault, ZoomCurrent, true);

				if (ImGui.DragScalar("Zoom Min", ImGuiDataType.Float, ZoomMin, 1f, ZeroFloat, MaxFloat, Marshal.PtrToStructure<float>(ZoomMin).ToString(), ImGuiSliderFlags.Logarithmic))
				{
					config.ZoomMin = Marshal.PtrToStructure<float>(ZoomMin);
					config.Save();
				}
				if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
				{
					Marshal.StructureToPtr(ZoomMinDefault, ZoomMin, true);
					config.ZoomMin = Marshal.PtrToStructure<float>(ZoomMin);
					config.Save();
				}


				if (ImGui.DragScalar("Zoom Max", ImGuiDataType.Float, ZoomMax, 1f, ZeroFloat, MaxFloat, Marshal.PtrToStructure<float>(ZoomMax).ToString(), ImGuiSliderFlags.Logarithmic))
				{
					config.ZoomMax = Marshal.PtrToStructure<float>(ZoomMax);
					config.Save();
				}
				if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
				{
					Marshal.StructureToPtr(ZoomMaxDefault, ZoomMax, true);
					config.ZoomMax = Marshal.PtrToStructure<float>(ZoomMax);
					config.Save();
				}

				ImGui.Spacing();
				
				//ImGui.SliderScalar("UpDown", ImGuiDataType.Float, UpDown, ZoomCurrent, ZoomCurrent, Marshal.PtrToStructure<float>(UpDown).ToString());
				//if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
				//	Marshal.StructureToPtr(UpDownDefault, UpDown, true);

				//ImGui.Spacing();


				if (ImGui.Checkbox("Disable camera collision", ref config.NoCollision))
				{
					SetCamNoCollision(config.NoCollision);
					config.Save();
				}

				ImGui.End();
			}
		}
	}
}
