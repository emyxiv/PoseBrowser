using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using PoseBrowser.Config;
using PoseBrowser.Files;
using PoseBrowser.Input;
using PoseBrowser.IPC;
using PoseBrowser.UI.Controls;

namespace PoseBrowser.UI.Windows;

internal class MainWindow : Window, IDisposable
{
    private readonly ConfigurationService _configurationService;
    private readonly BrioService _brioService;
    private readonly IClientState _clientStateService;
    private readonly ITextureProvider _textureProvidereService;
    private readonly InputService _inputService;
    
    public MainWindow(
        ConfigurationService configService,
        IClientState clientStateService,
        ITextureProvider textureProvider,
        InputService input,
        BrioService brioService) : base($"{PoseBrowser.Name} [{configService.Version}]###pose_browser_main_window",
                                        ImGuiWindowFlags.None)
    {
        Namespace = "pose_browser_main_namespace";

        _configurationService = configService;
        _brioService = brioService;
        _clientStateService = clientStateService;
        _textureProvidereService = textureProvider;
        _inputService = input;

        SizeConstraints = new WindowSizeConstraints
        {
            MaximumSize = new Vector2(2000, 5000),
            MinimumSize = new Vector2(270, 200)
        };
        
        input.AddListener(KeyBindEvents.Interface_TogglePoseBrowserWindow, this.OnMainWindowToggle);
        input.AddListener(KeyBindEvents.Posing_PreviewHovering, this.KeyDownPreviewPoseHovered);
    }

    public void Dispose()
    {
        _inputService.RemoveListener(KeyBindEvents.Interface_TogglePoseBrowserWindow, this.OnMainWindowToggle);
        _inputService.RemoveListener(KeyBindEvents.Posing_PreviewHovering, this.KeyDownPreviewPoseHovered);
    }
    private void OnMainWindowToggle()
    {
        this.IsOpen = !this.IsOpen;
    }
    private void KeyDownPreviewPoseHovered()
    {
        if (!this.IsOpen) return;
        PoseBrowser.Log.Debug("key down");
        PressPreview();
    }
    private void KeyUpPreviewPoseHovered()
    {
        PoseBrowser.Log.Debug("key up");
        ReleasePreview();
    }


    public bool UserPreviewingPoseHovered_previous = false;
    public bool UserPreviewingPoseHovered => InputService.IsKeyBindDown(KeyBindEvents.Posing_PreviewHovering);


    // configurations variables
    private int Columns = 0;
    private bool FilterImagesOnly = false;

    private bool CropImages = true;

    internal bool UseAsync = true;
    internal Regex PosesExts = new(@"^\.(pose|cmp)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    internal Regex ImagesExts = new(@"^\.(jpg|jpeg|png|gif)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Temp variables
    private Vector2 ThumbSize2D => _configurationService.Configuration.Appearance.BrowserThumbSize;
    private float ThumbSize => ThumbSize2D.X;
    private List<BrowserPoseFile> BrowserPoseFiles = new();

    private BrowserPoseFile? FileInFocus = null;
    private BrowserPoseFile? FileInPreview = null;
    private Dictionary<int, string> DisplayImages = new();
    private int SelectedImageIndex = 0;
    private bool IsHolding = false;
    private string Search = "";
    private Regex ShortPath = new(@"^$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private Vector2 ImageModalSize = default;

    private static DateTime? _lastRunTime = null;
    private static TimeSpan _timeSpan = TimeSpan.FromMinutes(5); // Change this to your desired time span

    public static void MustRefresh(Action closure, bool force = false)
    {
        if (force || _lastRunTime == null)
        {
            _lastRunTime = DateTime.Now;
            closure.Invoke();
            return;
        }

        TimeSpan timeSinceLastRun = DateTime.Now - _lastRunTime.Value;
        if (timeSinceLastRun <= _timeSpan)
            return;

        _lastRunTime = DateTime.Now;
        closure.Invoke();
    }

    // Toggle visibility
    // public void Toggle() {
    // 	//if (Visible) ClearImageCache();
    // 	Visible = !Visible;
    // }

    public void ClearImageCache()
    {
        PoseBrowser.Log.Verbose($"Clear Pose Browser images");
        DisposeImageModal();
        //BrowserPoseFiles.ForEach(f => f.DisposeImage());
        BrowserPoseFiles.Clear();
        // Services.ImageStorage.Dispose();
        FileInFocus = null;
        FileInPreview = null;
    }
    /*public static void OnGposeToggle(ActorGposeState gposeState) {
        if (gposeState == ActorGposeState.OFF) {
            ClearImageCache();
        }
    }*/

    public override bool DrawConditions()
    {
        return _clientStateService.IsGPosing;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        Sync();
    }


    // Draw window
    public override void Draw()
    {
        
        // todo: check if in gpose
        if (!_clientStateService.IsGPosing)
            return;


        // if (!ImGui.Begin("Pose Browser", ref Visible)) {
        // 	ImGui.End();
        // 	//if (BrowserPoseFiles.Any())
        // 	//	ClearImageCache();
        // 	return;
        // }

        // PoseBrowser.Log.Debug($"UserPreviewingPoseHovered: {UserPreviewingPoseHovered_previous} => {UserPreviewingPoseHovered}");
        if (UserPreviewingPoseHovered_previous != UserPreviewingPoseHovered && !UserPreviewingPoseHovered)
            KeyUpPreviewPoseHovered(); 
        UserPreviewingPoseHovered_previous = UserPreviewingPoseHovered;

        DrawImageModal();

        var files = BrowserPoseFiles;
        if (!string.IsNullOrWhiteSpace(Search))
            files = files.Where(f => f.Path.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToList();


        DrawToolBar(files.Count);
        ImGui.Spacing();

        ImGui.BeginChildFrame(76, ImGui.GetContentRegionAvail());
        bool anyHovered = false;
        int col = 1;
        try
        {
            foreach (var file in files)
            {

                if (FilterImagesOnly && file.ImagePath == null) continue;

                var ishovering = FileInFocus == file;
                float borderSize = ImGui.GetStyle().FramePadding.X;
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, borderSize);

                var imageTex = _configurationService.Configuration.Appearance.BrowserEnableImages &&
                               file.ImagePath != null
                                   ? _textureProvidereService.GetFromFile(file.ImagePath)
                                   : null;

                if (imageTex != null)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg]);
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(borderSize));
                    var textureWrap = imageTex.GetWrapOrEmpty();

                    if (CropImages)
                    {
                        (var uv0, var uv1) = CropRatioImage(textureWrap);
                        ImGui.ImageButton(textureWrap.ImGuiHandle, ThumbSize2D, uv0, uv1);
                    }
                    else
                        ImGui.ImageButton(textureWrap.ImGuiHandle, ScaleThumbImage(textureWrap));

                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, borderSize);
                    ImGui.PushStyleColor(ImGuiCol.Border,
                                         ImGui.GetStyle()
                                              .Colors[
                                                  ishovering ? (int)ImGuiCol.ButtonHovered : (int)ImGuiCol.WindowBg]);
                    ;
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg]);
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg]);

                    ImGui.Button($"{file.Name}##ContextMenu##{file.Path}", ThumbSize2D + (new Vector2(borderSize * 2)));

                    ImGui.PopStyleColor(3);
                    ImGui.PopStyleVar();
                }

                //ImGui.PopStyleColor();
                ImGui.PopStyleVar(1);

                file.IsVisible = ImGui.IsItemVisible();

                if (ImGui.IsItemHovered())
                {
                    FileInFocus = file;
                    anyHovered |= true;
                }

                var fileExt = Path.GetExtension(file.Path);
                string fileType;
                if (fileExt == ".pose")
                    fileType = "Anamnesis pose";
                else if (fileExt == ".cmp")
                    fileType = "Concept Matrix pose";
                else
                    fileType = "Unknown file type";

                if (ImGui.BeginPopupContextItem($"PoseBrowser##ContextMenu##{file.Path}",
                                                ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.AnyPopupId))
                {
                    ImGui.Text(file.Name);
                    ImGui.Separator();
                    ImGui.Text(fileType);

                    if (ImGui.Selectable($"{ShortPath.Replace(file.Path, "").TrimStart(new char[] { '\\', '/' })}"))
                        ImGui.SetClipboardText(Path.GetDirectoryName(file.Path));

                    if (imageTex != null)
                        ImGui.Text($"Image Size: {imageTex.GetWrapOrEmpty().Width}*{imageTex.GetWrapOrEmpty().Height}");


                    if (ImGui.Selectable($"Apply to target"))
                        ImportPose(file.Path,
                                   ImportPoseFlags.SaveTempAfter | ImportPoseFlags.ResetPreview | ImportPoseFlags.Face |
                                   ImportPoseFlags.Body);
                    if (ImGui.Selectable($"Apply body to target"))
                        ImportPose(file.Path,
                                   ImportPoseFlags.SaveTempAfter | ImportPoseFlags.ResetPreview | ImportPoseFlags.Body);
                    if (ImGui.Selectable($"Apply expression to target"))
                        ImportPose(file.Path,
                                   ImportPoseFlags.SaveTempAfter | ImportPoseFlags.ResetPreview | ImportPoseFlags.Face);

                    ImGui.EndPopup();
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    var imageFilesPaths = FindExtraImages(file);
                    if (imageFilesPaths != null)
                    {
                        DisplayImages = imageFilesPaths.Select((ifp, k) => new { ifp, k })
                                                       .ToDictionary(x => x.k, x => x.ifp);
                        PoseBrowser.Log.Debug(
                            $"Found {DisplayImages.Count} images:\n{string.Join("\n", DisplayImages)}");
                    }

                }

                // TODO: display discreet name in the image instead of tooltip

                // Restore the cursor to the same line to be able to calculate available region
                if (Columns == 0 || col < Columns)
                {
                    col++;
                    ImGui.SameLine();
                }
                else
                    col = 1;

                if (Columns == 0 && ImGui.GetContentRegionAvail().X < ThumbSize2D.X)
                    ImGui.Text(""); // Newline() seems buggy, so wrap with Text's natural line break
            }


            if (!anyHovered)
                FileInFocus = null;

            if (FileInFocus != FileInPreview && FileInPreview != null)
                RestoreTempPose();
            if (IsHolding && FileInFocus != null && FileInPreview == null)
                PressPreview();
        }
        catch (Exception e)
        {
            PoseBrowser.Log.Debug(e,"Suppressed error during file loop");
        }
        ImGui.EndChildFrame();

        // ImGui.End();
    }


    // private static bool DisplayImagesBusy = false;
    // private static Task? AsyncOpenImage = null;
    // private static Dictionary<string, TextureWrap?> DisplayImagesTex = new();
    private void DrawImageModal()
    {

        if (DisplayImages.Count == 0) return;

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new Vector2(0.5f));
        if (ImGui.BeginPopup($"##PoseBrowser##ImageDisplay##1",
                             ImGuiWindowFlags.Modal | ImGuiWindowFlags.Popup | ImGuiWindowFlags.AlwaysAutoResize))
        {

            var isImageClickedLeft = false;
            var isImageClickedRight = false;
            var clickedOutside = !ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);


            if (DisplayImages.TryGetValue(SelectedImageIndex, out var imagePath))
            {
                var texture = _textureProvidereService.GetFromFile(imagePath);

                var textWrap = texture.GetWrapOrEmpty();

                //PluginLog.Debug($"display image {imagePath}");
                // if (ImageModalSize == default) 
                ImageModalSize = ScaleImageIfBigger(texture.GetWrapOrEmpty(), ImGui.GetIO().DisplaySize * 0.75f);
                var mouseWheel = ImGui.GetIO().MouseWheel;

                if (ImGui.IsWindowHovered() && mouseWheel != 0 && ImGui.GetIO().KeyCtrl)
                {
                    var resizeMult = mouseWheel > 0 ? 1.1f : 0.9f;
                    ImageModalSize *= resizeMult;
                }

                ImGui.TextWrapped(
                    $"Image {(SelectedImageIndex + 1)} ({texture.GetWrapOrEmpty().Width}*{texture.GetWrapOrEmpty().Height}) [{ImageModalSize.X}*{ImageModalSize.Y}] {imagePath ?? "Image Loading..."}");
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(imagePath);

                // PoseBrowser.Log.Debug($"file {texture.GetWrapOrDefault()?.Height} exists: {File.Exists(imagePath)} path: {imagePath}");
                ImGui.Image(texture.GetWrapOrEmpty().ImGuiHandle, ImageModalSize);
                isImageClickedLeft = ImGui.IsItemClicked(ImGuiMouseButton.Left);
                isImageClickedRight = ImGui.IsItemClicked(ImGuiMouseButton.Right);

            }

            if (DisplayImages.Count > 1)
            {
                if (ImPO.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowLeft,
                                    default) || isImageClickedLeft)
                {
                    ImageModalSize = default;
                    SelectedImageIndex++;
                    if (SelectedImageIndex >= DisplayImages.Count)
                        SelectedImageIndex = 0;
                }

                ImGui.SameLine();

                ImGui.Text($"{SelectedImageIndex + 1}/{DisplayImages.Count}");
                ImGui.SameLine();

                if (ImPO.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowRight,
                                    default) || isImageClickedRight)
                {
                    ImageModalSize = default;
                    SelectedImageIndex--;
                    if (SelectedImageIndex < 0)
                        SelectedImageIndex = DisplayImages.Count - 1;
                }

                ImGui.SameLine();
            }

            if (ImGui.Button(
                    "Close",default) ||
                (clickedOutside && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
                                    ImGui.IsMouseClicked(ImGuiMouseButton.Right))))
            {
                DisposeImageModal();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        ImGui.OpenPopup($"##PoseBrowser##ImageDisplay##1");
    }

    private void DisposeImageModal()
    {
        SelectedImageIndex = 0;
        ImageModalSize = default;
        DisplayImages.Clear();
    }

    private void DrawToolBar(int hits)
    {

        Vector4? syncColor = _currentlySyncing ? (_currentlySyncingImages ? new(0.8f, 0.8f, 0.4f, 1f) : new(0.8f, 0.4f, 0.4f, 1f)) : null;   
        if (ImPO.IconButtonTooltip(Dalamud.Interface.FontAwesomeIcon.Sync, "Refresh poses and images", default,
                                   $"SyncButton##PoseBrowser"))
            Sync();

        ImGui.SameLine(0, ImGui.GetFontSize());
        ImGui.Text($"({hits})");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 7);
        ImGui.InputTextWithHint("##Browser##Search", "Search", ref Search, 100, ImGuiInputTextFlags.AutoSelectAll);

        // images
        ImGui.SameLine(0, ImGui.GetFontSize());
        ImPO.IconButtonToggle(Dalamud.Interface.FontAwesomeIcon.Image, ref FilterImagesOnly, "Images Only", default,
                              $"Images Only##PoseBrowser");


        // Temporary disable stream image loading as loading seems more resource hungry than keep them all loaded.
        //ImGui.SameLine();
        //if (ImPO.IconButtonToggle(Dalamud.Interface.FontAwesomeIcon.Stream, ref StreamImageLoading, "Stream image load (No image preload)", default, $"Preload Images##PoseBrowser"))
        //	Sync();

        // size/columns
        ImGui.SameLine(0, ImGui.GetFontSize());
        ImPO.IconButtonToggle(Dalamud.Interface.FontAwesomeIcon.CropAlt, ref CropImages, "Crop Images", default,
                              $"CropImages##PoseBrowser");
        if (!CropImages)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 5);
            ImGui.InputInt($"##Columns##PoseBrowser", ref Columns, 1, 2);
            ImPO.Tooltip("Number of Images before a linebreak\n0: Auto");
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 5);
        var thumbsize = ThumbSize;
        if (ImGui.SliderFloat("##Browser##ThumbSize", ref thumbsize, 50, 1000))
        {
            var prevSize = _configurationService.Configuration.Appearance.BrowserThumbSize;
            _configurationService.Configuration.Appearance.BrowserThumbSize =
                new(ThumbSize, prevSize.Y / (prevSize.X / ThumbSize));
        }

        ImPO.Tooltip("Thumb size");
        var mouseWheel = ImGui.GetIO().MouseWheel;
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.NoPopupHierarchy) &&
            mouseWheel != 0 && ImGui.GetIO().KeyCtrl)
        {
            thumbsize += mouseWheel * 5f;

            var prevSize = _configurationService.Configuration.Appearance.BrowserThumbSize;
            _configurationService.Configuration.Appearance.BrowserThumbSize =
                new(thumbsize, prevSize.Y / (prevSize.X / thumbsize));
        }

        // add/clear library
        ImGui.SameLine(0, ImGui.GetFontSize());
        if (ImPO.IconButton(Dalamud.Interface.FontAwesomeIcon.FolderPlus))
        {
            UIManager.Instance.FileDialogManager.OpenFolderDialog(
                "Add pose library path",
                (selected, path) =>
                {
                    if (!selected) return;
                    _configurationService.Configuration.Filesystem.BrowserLibraryPaths.Add(path);
                    Sync();
                },
                _configurationService.Configuration.Filesystem.BrowserLibraryPaths.Any()
                    ? _configurationService.Configuration.Filesystem.BrowserLibraryPaths.Last()
                    : null
            );
        }

        var libList = string.Join("\n", _configurationService.Configuration.Filesystem.BrowserLibraryPaths);
        ImPO.Tooltip(
            $"{_configurationService.Configuration.Filesystem.BrowserLibraryPaths.Count} saved pose librarie(s):\n{libList}");

        ImGui.SameLine();
        if (ImPO.IconButtonHoldConfirm(Dalamud.Interface.FontAwesomeIcon.FolderMinus,
                                       $"Delete all {_configurationService.Configuration.Filesystem.BrowserLibraryPaths.Count} saved pose librarie(s):\n{libList}"))
        {
            _configurationService.Configuration.Filesystem.BrowserLibraryPaths.Clear();
            ClearImageCache();
        }

        ImGui.SameLine();



        var browserEnableImages = _configurationService.Configuration.Appearance.BrowserEnableImages;
        if (ImPO.IconButtonToggle(Dalamud.Interface.FontAwesomeIcon.Image, ref browserEnableImages,
                                  "Display images\nWARNING: this may consume a log of VRAM and maybe crash the game.",
                                  default, $"BrowserEnableImages##PoseBrowser"))
        {
            _configurationService.Configuration.Appearance.BrowserEnableImages = browserEnableImages;
            MustRefresh(SyncImageFiles, true);
        }

        ImGui.SameLine();
        var browserThumbSize = _configurationService.Configuration.Appearance.BrowserThumbSize;
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 5);
        if (ImGui.DragFloat2("##BrowserThumbSize##PoseBrowser", ref browserThumbSize, 5, 50, 1000, "%.0f"))
        {
            _configurationService.Configuration.Appearance.BrowserThumbSize = browserThumbSize;
        }
        // ImGui.SameLine();
        // if(ImPO.IconButtonHoldConfirm(Dalamud.Interface.FontAwesomeIcon.Recycle, $"Drop {Services.ImageStorage.Count} cached images")) {
        // 	Services.ImageStorage.Clear();
        // }
        ImGui.SameLine();
        if(ImPO.IconButton(Dalamud.Interface.FontAwesomeIcon.Cog,default,"##Settings##PoseBrowser"))
            UIManager.Instance.ToggleSettingsWindow();
    }

    private void SyncImageFiles()
        => Task.Run(SyncImageFilesAsync);
    

    private bool _currentlySyncingImages = false;
    private void SyncImageFilesAsync()
    {
        if (_currentlySyncingImages) return;

        _currentlySyncingImages = true;
        try
        {
            BrowserPoseFiles.ForEach(FindImage);
        }
        catch (Exception e)
        {
            PoseBrowser.Log.Debug(e, "Suppressed error during file loop");
        } finally
        {
            _currentlySyncingImages = false;
        }
    }
        

    private void Sync()
        => Task.Run(SyncAsync);
    
    private bool _currentlySyncing = false;
	private void SyncAsync() {
        if (_currentlySyncing) return;
		if (!_configurationService.Configuration.Filesystem.BrowserLibraryPaths.Any(Directory.Exists)) return;

        ClearImageCache(); // todo: find a better way to sync

        _currentlySyncing = true;
        
        
		// Todo: check if already loaded and prevets it
		//ClearImageCache();
		ShortPath = new("^(" + String.Join("|", _configurationService.Configuration.Filesystem.BrowserLibraryPaths.Select(p => Regex.Escape(p))) + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled);


		List<FileInfo> tempPosesFound = new();
		foreach (var path in _configurationService.Configuration.Filesystem.BrowserLibraryPaths) {
			var pathItems = from d in new DirectoryInfo(path)
					.EnumerateFiles("*", SearchOption.AllDirectories)
					.Where(file => PosesExts.IsMatch(file.Extension))
							select d;
			tempPosesFound = tempPosesFound.Concat(pathItems).ToList();
		}
		var posesFound = tempPosesFound.OrderBy(f => f.FullName);

		foreach (var item in posesFound) {

			if (string.IsNullOrEmpty(item.Name) || (item.Name[0] == '.')) continue;
			// TODO: verify if the file is valid

			BrowserPoseFile entry = new(item.FullName, Path.GetFileNameWithoutExtension(item.Name));
			BrowserPoseFiles.Add(entry);
		}
        if(_configurationService.Configuration.Appearance.BrowserEnableImages) MustRefresh(SyncImageFiles, true);
        _currentlySyncing = false;
    }

	[Flags]
	enum ImportPoseFlags {
		None = 0,
		Face = 1,
		Body = 2,
		SaveTempBefore = 4,
		SaveTempAfter = 8,
		ResetPreview = 16,
	}
	private void ImportPose(string path, ImportPoseFlags flags)
    {
        if (!_brioService.IsBrioAvailable) return;
        if (!_clientStateService.IsGPosing) return;
        
        if (flags.HasFlag(ImportPoseFlags.ResetPreview))
            FileInPreview = null;

        _brioService.ImportPoseTarget(path);
	}

	public bool PressPreview() {
		if (FileInFocus == null) return false;

		IsHolding = true;
		FileInPreview = FileInFocus;
		var flags = ImportPoseFlags.SaveTempBefore;
		if (!ImGui.GetIO().KeyShift) flags |= ImportPoseFlags.Body;
		if (!ImGui.GetIO().KeyCtrl) flags |= ImportPoseFlags.Face;
		ImportPose(FileInFocus.Path, flags);
		return true;
	}

	public bool ReleasePreview() {
		IsHolding = false;
		FileInPreview = null;
		if (FileInFocus == null) return false;

		return RestoreTempPose();
	}
	public  bool RestoreTempPose() {
		FileInPreview = null;

        return _brioService.IsBrioAvailable && _brioService.UndoTarget();
    }


	private (Vector2, Vector2) CropRatioImage(IDalamudTextureWrap image) {

        
		float left = 0, top = 0, right = 1, bottom = 1;

		float sourceAspectRatio = (float)image.Width / image.Height;
		float targetAspectRatio = (float)ThumbSize2D.X / ThumbSize2D.Y;

		if (sourceAspectRatio > targetAspectRatio) {
			float excedingRatioH = Math.Abs(targetAspectRatio - sourceAspectRatio) / sourceAspectRatio;
			float excedingRatioHHalf = excedingRatioH / 2;

			left = excedingRatioHHalf;
			right = 1 - excedingRatioHHalf;
		} else if (sourceAspectRatio < targetAspectRatio) {
			float excedingRatioW = Math.Abs(targetAspectRatio - sourceAspectRatio) * sourceAspectRatio;
			float excedingRatioWHalf = excedingRatioW / 2;

			top = excedingRatioWHalf;
			bottom = 1 - excedingRatioWHalf;
		}

		var uv0 = new Vector2(left, top);
		var uv1 = new Vector2(right, bottom);
		return (uv0, uv1);
	}
	private Vector2 ScaleThumbImage(IDalamudTextureWrap image) =>
		ScaleImage(image, ThumbSize2D, true, false);
	private Vector2 ScaleImage(IDalamudTextureWrap image, Vector2 targetSize, bool resizeWidth = true, bool resizeHeight = true)
    {

		var ratioX = targetSize.X / image.Width;
		var ratioY = targetSize.Y / image.Height;
		float ratio = default;
		if (resizeWidth && resizeHeight)
			ratio = (float)Math.Min((double)ratioX, (double)ratioY);
		else if (resizeWidth && !resizeHeight)
			ratio = ratioY;
		else if (!resizeWidth && resizeHeight)
			ratio = ratioX;
		else
			return new(image.Width, image.Height);

		return new(
			image.Width * ratio,
			image.Height * ratio
		);
	}
	private Vector2 ScaleImageIfBigger(IDalamudTextureWrap image, Vector2 maxSize) {
		if (image.Width > maxSize.X || image.Height > maxSize.Y)
			return ScaleImage(image, maxSize);
		else
			return new Vector2(image.Width, image.Height);
	}
    
    public void FindImage(BrowserPoseFile browserPoseFile) {
		// Add embedded image if exists
		browserPoseFile.ImagePath = browserPoseFile.FindEmbeddedImage();
		if (browserPoseFile.ImagePath != null) return;

		var imageFile = FindExtraImages(browserPoseFile);
		if (imageFile != null)
			browserPoseFile.ImagePath = imageFile.FirstOrDefault();
	}
	public IEnumerable<string>? FindExtraImages(BrowserPoseFile browserPoseFile) {

		var embedString = browserPoseFile.FindEmbeddedImage();
		//if (embedString != null)

		// Try finding related images close to the pose file
		// TODO: improve algo for better relevance
		var dir = System.IO.Path.GetDirectoryName(browserPoseFile.Path);
		if (dir == null) return null;
		try {
			var dirInfo = new DirectoryInfo(dir)?
				.EnumerateFiles("*", SearchOption.TopDirectoryOnly)?
				.Where(file => ImagesExts.IsMatch(file.Extension)).Select(f=>f.FullName);
			if (embedString != null) dirInfo = dirInfo!.Prepend(embedString);
			if (dirInfo != null) return dirInfo;

			return new DirectoryInfo(dir).Parent?
				.EnumerateFiles("*", SearchOption.TopDirectoryOnly)?
				.Where(file => ImagesExts.IsMatch(file.Extension)).Select(f => f.FullName);

		} catch(Exception) {
			return null;
		}

	}
}
internal class BrowserPoseFile {
	public string Path { get; set; }
	public string Name { get; set; }
	public string? ImagePath { get; set; } = null;
	public bool IsVisible { get; set; } = false;

	public BrowserPoseFile(string path, string name) {
		Path = path;
		Name = name;
        // ImagePath = imagePath;
		// if(PoseBrowser.Configuration?.Appearance.BrowserEnableImages ?? false) this.FindImage(); // TODO: async this
	}

	public string? FindEmbeddedImage() {
		if (System.IO.Path.GetFileNameWithoutExtension(this.Path) == ".pose" && File.ReadLines(this.Path).Any(line => line.Contains("\"Base64Image\""))) {

			var content = File.ReadAllText(this.Path);
			var pose = JsonSerializer.Deserialize<PoseFile>(content);
			if (pose?.Base64Image != null) {
				return pose.Base64Image;
			}
		}
		return null;
	}

   
}
