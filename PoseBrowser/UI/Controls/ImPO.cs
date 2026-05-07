using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Utility;

namespace PoseBrowser.UI.Controls;

internal class ImPO
{
    public static bool IconButtonTooltip(FontAwesomeIcon icon, string tooltip, Vector2 size = default, string hiddenLabel = "", Vector4? iconColor = null) {
		bool accepting = IconButton(icon, size, hiddenLabel, iconColor);
		Tooltip(tooltip);
		return accepting;
	}
    public static bool IconButton(FontAwesomeIcon icon, Vector2 size = default, string hiddenLabel = "", Vector4? iconColor = null) {
		ImGui.PushFont(UiBuilder.IconFont);
        if(iconColor.HasValue) ImGui.PushStyleColor(ImGuiCol.Text, iconColor.Value);
		bool accepting = ImGui.Button((icon.ToIconString() ?? "")+"##"+ hiddenLabel, size);
        if(iconColor.HasValue) ImGui.PopStyleColor();
		ImGui.PopFont();
		return accepting;
	}
    public static bool IconButtonToggle(FontAwesomeIcon icon, ref bool value, string tooltip = "", Vector2 size = default, string hiddenLabel = "") {
		if (value) ImGui.PushStyleColor(ImGuiCol.Text, VisibleCheckmarkColor());
		var used = IconButton(icon, size, hiddenLabel);
		if (value) ImGui.PopStyleColor();
		Tooltip(tooltip);
		if (used)
			value = !value;

		return used;
	}
    public static bool IconButtonHoldConfirm(FontAwesomeIcon icon, string tooltip, bool isHoldingKey, Vector2 size = default, string hiddenLabel = "") {
		if (!isHoldingKey) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().DisabledAlpha);
		bool accepting = IconButton(icon, size, hiddenLabel);
		if (!isHoldingKey) ImGui.PopStyleVar();

		Tooltip(tooltip);

		return accepting && isHoldingKey;
	}
	public static bool IconButtonHoldConfirm(FontAwesomeIcon icon, string tooltip, Vector2 size = default, string hiddenLabel = "") =>
		IconButtonHoldConfirm(icon, tooltip, ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift, size, hiddenLabel);

    public static Vector4 VisibleCheckmarkColor() {
		var currentCol = ImGui.GetStyle().Colors[(int)ImGuiCol.CheckMark];
		float h = 0f;
		float s = 0f;
		float v = 0f;
		ImGui.ColorConvertRGBtoHSV(currentCol.X, currentCol.Y, currentCol.Z, ref h, ref s, ref v);
		s = 0.55f;
		v = Math.Clamp(v * 1.25f, 0.0f, 1.0f);
		ImGui.ColorConvertHSVtoRGB(h, s, v, ref currentCol.X, ref currentCol.Y, ref currentCol.Z);
		return currentCol;
	}
    public static void Tooltip(string text) {
		if (!text.IsNullOrWhitespace() && ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
			ImGui.TextUnformatted(text);
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}
}
