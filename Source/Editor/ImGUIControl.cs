using ImGuiNET;
using System.Numerics;
using System.Runtime.InteropServices;
namespace Editor
{
    public enum FileButtonAction
    {
        None = 0,
        Click,
        DoubleClick

    }
    
    public class ImGUICtl
    {
        static double last_click_time = 0.0;
        public static FileButtonAction FolderButton(string id, string title, string label, uint textureid, float width, bool IsSelect = false)
        {
            FileButtonAction rtl = FileButtonAction.None;
            var location = ImGui.GetCursorPos();
            var textSize = ImGui.CalcTextSize(title);
            var controlSize = new Vector2(width, width + textSize.Y + ImGui.GetStyle().ItemSpacing.Y * 2);

            ImGui.SetCursorPos(location + ImGui.GetStyle().FramePadding);
            ImGui.Image((nint)textureid, new Vector2(width - ImGui.GetStyle().FramePadding.X * 2, width - ImGui.GetStyle().FramePadding.Y * 2));
            ImGui.SetCursorPosX(location.X + (width - textSize.X) / 2);


            if (string.IsNullOrEmpty(label) == false)
            {
                var texSize = ImGui.CalcTextSize(label);
                var textHeight = ImGui.GetCursorPosY() - texSize.Y - ImGui.GetStyle().FramePadding.Y * 3;

                Vector2 Left = new Vector2(location.X + ImGui.GetStyle().FramePadding.Y * 2, textHeight);
                Vector2 Right = Left + texSize;
                ImGui.SetCursorPos(Left);
                ImGui.Text(label);
                var color = ImGui.GetColorU32(ImGuiCol.Header);
                RenderFrame(Left, Right, color, false, 0);
            }
            ImGui.SetCursorPosX(location.X + (width - textSize.X) / 2);
            ImGui.Text(title);
            ImGui.SetCursorPos(location);
            unsafe
            {
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, *ImGui.GetStyleColorVec4(ImGuiCol.Button));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, *ImGui.GetStyleColorVec4(ImGuiCol.Button));
            }
            if (IsSelect == false)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            }
            if (ImGui.Button("##" + id + "_button" + title, controlSize))
            {
                
                if (IsSelect == true && ImGui.GetTime() - last_click_time < ImGui.GetIO().MouseDoubleClickTime)
                {
                    rtl = FileButtonAction.DoubleClick;
                }
                else
                {
                    rtl = FileButtonAction.Click;
                }
                last_click_time = ImGui.GetTime();
            }
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();
            if (IsSelect == false)
            {
                ImGui.PopStyleColor();
            }
            return rtl;
        }
        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl, EntryPoint = "igRenderFrame")]
        public static extern void RenderFrame(Vector2 Min, Vector2 Max, uint Color, bool Border, float Rounding);
    }


}
