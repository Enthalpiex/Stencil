using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Stencil
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public sealed class StencilEditorAddon : MonoBehaviour
    {
        private const string InputLockId = "StencilPanelLock";

        private EditorReferenceModelService _service;
        private EditorWindowState _state;
        private EditorWindowController _controller;
        private KspReferenceModelRuntime _runtime;
        private Rect _windowRect;
        private int _windowId;
        private ApplicationLauncherButton _appButton;
        private StencilIconProvider _iconProvider;

        private string _pathInput;
        private Rect _pickerRect;
        private int _pickerWindowId;
        private bool _isPickerVisible;
        private string[] _pickerFiles;
        private Vector2 _pickerScroll;

        private Rect _sliceRect;
        private int _sliceWindowId;
        private bool _isSliceVisible;
        private Vector2 _sliceScroll;

        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _textFieldStyle;

        private const float OpacityStep = 0.05f;
        private const float ScaleStep = 0.001f;
        private const float SlicePositionStep = 1f;
        private const float SliceRotationStep = 15f;

        private Guid? _selectedForInputs;
        private string _opacityInput;
        private string _scaleInput;
        private string _moveStepInput;
        private string _rotateStepInput;
        private string _colorHexInput;
        private Vector2 _mainScroll;
        private string _titleText;

        private string _sliceApx;
        private string _sliceApy;
        private string _sliceApz;
        private string _sliceArx;
        private string _sliceAry;
        private string _sliceArz;
        private string _sliceBpx;
        private string _sliceBpy;
        private string _sliceBpz;
        private string _sliceBrx;
        private string _sliceBry;
        private string _sliceBrz;

        private bool _inputLocked;

        private static string T(string key, params object[] args)
        {
            return StencilI18n.Tr(key, args);
        }

        private void Start()
        {
            _service = new EditorReferenceModelService();
            _state = new EditorWindowState();
            _controller = new EditorWindowController(_service, _state);
            _runtime = new KspReferenceModelRuntime(_service);
            _runtime.Start();
            _iconProvider = new StencilIconProvider();

            _windowRect = new Rect(260f, 120f, 620f, 620f);
            _windowId = Guid.NewGuid().GetHashCode();
            _pickerRect = new Rect(900f, 120f, 560f, 460f);
            _pickerWindowId = Guid.NewGuid().GetHashCode();
            _sliceRect = new Rect(900f, 600f, 560f, 520f);
            _sliceWindowId = Guid.NewGuid().GetHashCode();
            _pathInput = string.Empty;
            _pickerFiles = new string[0];
            _state.Hide();

            _opacityInput = "0.35";
            _scaleInput = "0.02";
            _moveStepInput = "0.25";
            _rotateStepInput = "15";
            _colorHexInput = "#FFFFFF";
            _titleText = "Stencil v" + GetDisplayVersion() + " Beta";

            _sliceApx = "0";
            _sliceApy = "0";
            _sliceApz = "0";
            _sliceArx = "0";
            _sliceAry = "0";
            _sliceArz = "0";
            _sliceBpx = "0";
            _sliceBpy = "0";
            _sliceBpz = "0";
            _sliceBrx = "0";
            _sliceBry = "180";
            _sliceBrz = "0";
        }

        private void Update()
        {
            EnsureAppButton();
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (_state.IsVisible)
            {
                _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "Stencil", _windowStyle);
                _state.SetWindowRect(new FloatRect(_windowRect.x, _windowRect.y, _windowRect.width, _windowRect.height));
            }

            if (_isPickerVisible)
            {
                _pickerRect = GUILayout.Window(_pickerWindowId, _pickerRect, DrawPickerWindow, T("window.chooseModel"), _windowStyle);
            }

            if (_isSliceVisible)
            {
                _sliceRect = GUILayout.Window(_sliceWindowId, _sliceRect, DrawSliceWindow, "Slice", _windowStyle);
            }

            UpdatePanelInputLockFromGui();
        }

        private void OnDestroy()
        {
            if (_appButton != null && ApplicationLauncher.Instance != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(_appButton);
                _appButton = null;
            }

            RemovePanelInputLock();

            if (_runtime != null)
            {
                _runtime.Dispose();
                _runtime = null;
            }
        }

        private void EnsureAppButton()
        {
            if (_appButton != null)
            {
                return;
            }

            if (ApplicationLauncher.Instance == null || !ApplicationLauncher.Ready)
            {
                return;
            }

            Texture iconTexture = null;
            object iconObj;
            if (_iconProvider != null && _iconProvider.TryGetToolbarIcon(out iconObj))
            {
                iconTexture = iconObj as Texture;
            }

            if (iconTexture == null)
            {
                var fallback = new Texture2D(2, 2);
                fallback.SetPixel(0, 0, Color.white);
                fallback.SetPixel(1, 0, Color.white);
                fallback.SetPixel(0, 1, Color.white);
                fallback.SetPixel(1, 1, Color.white);
                fallback.Apply();
                iconTexture = fallback;
            }

            _appButton = ApplicationLauncher.Instance.AddModApplication(
                OnAppButtonOn,
                OnAppButtonOff,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
                iconTexture);
        }

        private void OnAppButtonOn()
        {
            _state.Show();
        }

        private void OnAppButtonOff()
        {
            _state.Hide();
            _isPickerVisible = false;
            _isSliceVisible = false;
        }

        private void DrawWindow(int id)
        {
            EnsureStyles();

            GUILayout.BeginVertical();
            _mainScroll = GUILayout.BeginScrollView(_mainScroll, GUILayout.ExpandHeight(true));

            GUILayout.Label(_titleText, _labelStyle);

            GUILayout.Label(T("label.importFormats"), _labelStyle);
            GUILayout.Label(string.IsNullOrWhiteSpace(_pathInput) ? T("label.noFileSelected") : Path.GetFileName(_pathInput), _labelStyle);

            if (GUILayout.Button(T("button.chooseFile"), _buttonStyle))
            {
                RefreshPickerFiles();
                _isPickerVisible = true;
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(_state.PendingImportPath);
            if (GUILayout.Button(T("button.import"), _buttonStyle))
            {
                Guid importedId;
                string error;
                var ok = _controller.TryImportFromPendingPath(out importedId, out error);
            }
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUILayout.Label(T("label.models"), _labelStyle);
            var snapshot = _controller.GetSnapshot();
            for (var i = 0; i < snapshot.Models.Length; i++)
            {
                var item = snapshot.Models[i];
                var caption = item.IsSelected ? "> " + item.Name : item.Name;
                if (GUILayout.Button(caption, _buttonStyle))
                {
                    _controller.SelectByIndex(i);
                }
            }

            GUILayout.Space(8f);
            if (GUILayout.Button(T("button.removeSelected"), _buttonStyle))
            {
                _controller.RemoveSelected();
            }

            if (GUILayout.Button(T("button.removeAll"), _buttonStyle))
            {
                _controller.RemoveAll();
            }

            var selected = _controller.GetSelectedModelOrNull();
            if (selected != null)
            {
                EnsureSelectedInputs(selected);

                GUILayout.Space(8f);
                GUILayout.Label("Opacity", _labelStyle);
                DrawNumberAdjustRow(
                    ref _opacityInput,
                    OpacityStep,
                    0f,
                    1f,
                    functionValue => _controller.SetSelectedOpacity(functionValue),
                    "0.00");

                GUILayout.Space(4f);
                GUILayout.Label("Scale (uniform)", _labelStyle);
                DrawNumberAdjustRow(
                    ref _scaleInput,
                    ScaleStep,
                    0.001f,
                    100f,
                    functionValue => _controller.SetSelectedUniformScale(functionValue),
                    "0.###");

                GUILayout.Space(4f);
                GUILayout.Label("Move step", _labelStyle);
                _moveStepInput = DrawLargeTextField(_moveStepInput);
                GUILayout.Label("Position X:" + selected.Transform.Position.X.ToString("0.###", CultureInfo.InvariantCulture) +
                                " Y:" + selected.Transform.Position.Y.ToString("0.###", CultureInfo.InvariantCulture) +
                                " Z:" + selected.Transform.Position.Z.ToString("0.###", CultureInfo.InvariantCulture), _labelStyle);
                DrawTransformNudgeButtons(true);

                GUILayout.Space(4f);
                GUILayout.Label("Rotate step (deg)", _labelStyle);
                _rotateStepInput = DrawLargeTextField(_rotateStepInput);
                GUILayout.Label("Rotation X:" + selected.Transform.RotationEuler.X.ToString("0.###", CultureInfo.InvariantCulture) +
                                " Y:" + selected.Transform.RotationEuler.Y.ToString("0.###", CultureInfo.InvariantCulture) +
                                " Z:" + selected.Transform.RotationEuler.Z.ToString("0.###", CultureInfo.InvariantCulture), _labelStyle);
                DrawTransformNudgeButtons(false);

                GUILayout.Space(4f);
                GUILayout.Label("Quick Align", _labelStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Front", _buttonStyle)) { SetSelectedRotationAbsolute(new Float3(0f, 0f, 0f)); }
                if (GUILayout.Button("Back", _buttonStyle)) { SetSelectedRotationAbsolute(new Float3(0f, 180f, 0f)); }
                if (GUILayout.Button("Left", _buttonStyle)) { SetSelectedRotationAbsolute(new Float3(0f, -90f, 0f)); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Right", _buttonStyle)) { SetSelectedRotationAbsolute(new Float3(0f, 90f, 0f)); }
                if (GUILayout.Button("Top", _buttonStyle)) { SetSelectedRotationAbsolute(new Float3(-90f, 0f, 0f)); }
                if (GUILayout.Button("Bottom", _buttonStyle)) { SetSelectedRotationAbsolute(new Float3(90f, 0f, 0f)); }
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                GUILayout.Label("Display Mode", _labelStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Outer", _buttonStyle)) { _controller.SetSelectedDisplayMode(ReferenceDisplayMode.OuterShell); }
                if (GUILayout.Button("Mixed", _buttonStyle)) { _controller.SetSelectedDisplayMode(ReferenceDisplayMode.Mixed); }
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                GUILayout.Label("Color (#RRGGBB or #RRGGBBAA)", _labelStyle);
                _colorHexInput = DrawLargeTextField(_colorHexInput ?? "#FFFFFF");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Color", _buttonStyle))
                {
                    ApplySelectedColorFromHex();
                }
                if (GUILayout.Button("White", _buttonStyle)) { ApplyPresetColor("#FFFFFF"); }
                if (GUILayout.Button("Red", _buttonStyle)) { ApplyPresetColor("#FF4040"); }
                if (GUILayout.Button("Green", _buttonStyle)) { ApplyPresetColor("#40FF40"); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Blue", _buttonStyle)) { ApplyPresetColor("#4FA3FF"); }
                if (GUILayout.Button("Yellow", _buttonStyle)) { ApplyPresetColor("#FFD54A"); }
                if (GUILayout.Button("Cyan", _buttonStyle)) { ApplyPresetColor("#40FFFF"); }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Slice Window", _buttonStyle))
                {
                    _isSliceVisible = !_isSliceVisible;
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label(T("label.status", _state.LastStatusMessage), _labelStyle);

            if (GUILayout.Button(T("button.close"), _buttonStyle))
            {
                _state.Hide();
                _isPickerVisible = false;
                _isSliceVisible = false;
                if (_appButton != null)
                {
                    _appButton.SetFalse(false);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawPickerWindow(int id)
        {
            EnsureStyles();

            GUILayout.BeginVertical();

            var folder = ModAssets.GetModelScanFolderPath();
            GUILayout.Label(T("label.folder", folder), _labelStyle);
            GUILayout.Label(T("label.tipPutFiles"), _labelStyle);

            if (GUILayout.Button(T("button.refresh"), _buttonStyle))
            {
                RefreshPickerFiles();
            }

            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll, GUILayout.Height(280f));
            if (_pickerFiles.Length == 0)
            {
                GUILayout.Label(T("label.noSupportedFiles"), _labelStyle);
            }
            else
            {
                for (var i = 0; i < _pickerFiles.Length; i++)
                {
                    var filePath = _pickerFiles[i];
                    if (GUILayout.Button(Path.GetFileName(filePath), _buttonStyle))
                    {
                        _pathInput = filePath;
                        _state.PendingImportPath = filePath;
                        _state.SetStatus(T("status.selected", Path.GetFileName(filePath)));
                        _isPickerVisible = false;
                    }
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button(T("button.close"), _buttonStyle))
            {
                _isPickerVisible = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawSliceWindow(int id)
        {
            EnsureStyles();

            GUILayout.BeginVertical();
            _sliceScroll = GUILayout.BeginScrollView(_sliceScroll, GUILayout.Height(Mathf.Min(Screen.height * 0.55f, 460f)));

            var selected = _controller.GetSelectedModelOrNull();
            if (selected == null)
            {
                GUILayout.Label(T("label.selectModelFirst"), _labelStyle);
                GUILayout.EndScrollView();
                if (GUILayout.Button(T("button.close"), _buttonStyle))
                {
                    _isSliceVisible = false;
                }
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            EnsureSelectedInputs(selected);

            GUILayout.Label("Mode", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All", _buttonStyle)) { _controller.SetSelectedSliceMode(ReferenceSliceMode.All); }
            if (GUILayout.Button("Half", _buttonStyle)) { _controller.SetSelectedSliceMode(ReferenceSliceMode.OneSide); }
            if (GUILayout.Button("Between", _buttonStyle)) { _controller.SetSelectedSliceMode(ReferenceSliceMode.Between); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Keep + (Inside)", _buttonStyle)) { _controller.SetSelectedSliceSide(ReferenceSliceSide.Positive); }
            if (GUILayout.Button("Keep - (Outside)", _buttonStyle)) { _controller.SetSelectedSliceSide(ReferenceSliceSide.Negative); }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Position step: 1", _labelStyle);
            GUILayout.Label("Rotation step: 15", _labelStyle);

            GUILayout.Space(6f);
            DrawSinglePlaneControls("Plane A", true, ref _sliceApx, ref _sliceApy, ref _sliceApz, ref _sliceArx, ref _sliceAry, ref _sliceArz);

            GUILayout.Space(8f);
            DrawSinglePlaneControls("Plane B", false, ref _sliceBpx, ref _sliceBpy, ref _sliceBpz, ref _sliceBrx, ref _sliceBry, ref _sliceBrz);

            GUILayout.EndScrollView();

            if (GUILayout.Button(T("button.close"), _buttonStyle))
            {
                _isSliceVisible = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawSinglePlaneControls(string title, bool planeA, ref string px, ref string py, ref string pz, ref string rx, ref string ry, ref string rz)
        {
            GUILayout.Label(title + " Position", _labelStyle);
            DrawVector3Inputs(ref px, ref py, ref pz);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("X-", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, -SlicePositionStep, 0f, 0f, 0f, 0f, 0f); }
            if (GUILayout.Button("X+", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, SlicePositionStep, 0f, 0f, 0f, 0f, 0f); }
            if (GUILayout.Button("Y-", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, -SlicePositionStep, 0f, 0f, 0f, 0f); }
            if (GUILayout.Button("Y+", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, SlicePositionStep, 0f, 0f, 0f, 0f); }
            if (GUILayout.Button("Z-", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, 0f, -SlicePositionStep, 0f, 0f, 0f); }
            if (GUILayout.Button("Z+", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, 0f, SlicePositionStep, 0f, 0f, 0f); }
            GUILayout.EndHorizontal();

            GUILayout.Label(title + " Rotation", _labelStyle);
            DrawVector3Inputs(ref rx, ref ry, ref rz);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("X-", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, 0f, 0f, -SliceRotationStep, 0f, 0f); }
            if (GUILayout.Button("X+", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, 0f, 0f, SliceRotationStep, 0f, 0f); }
            if (GUILayout.Button("Y-", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, 0f, 0f, 0f, -SliceRotationStep, 0f); }
            if (GUILayout.Button("Y+", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, 0f, 0f, 0f, SliceRotationStep, 0f); }
            if (GUILayout.Button("Z-", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, 0f, 0f, 0f, 0f, -SliceRotationStep); }
            if (GUILayout.Button("Z+", _buttonStyle)) { NudgePlane(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, 0f, 0f, 0f, 0f, 0f, SliceRotationStep); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Front", _buttonStyle)) { SetPlaneRotation(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, new Float3(0f, 0f, 0f)); }
            if (GUILayout.Button("Back", _buttonStyle)) { SetPlaneRotation(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, new Float3(0f, 180f, 0f)); }
            if (GUILayout.Button("Left", _buttonStyle)) { SetPlaneRotation(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, new Float3(0f, -90f, 0f)); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Right", _buttonStyle)) { SetPlaneRotation(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, new Float3(0f, 90f, 0f)); }
            if (GUILayout.Button("Top", _buttonStyle)) { SetPlaneRotation(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, new Float3(-90f, 0f, 0f)); }
            if (GUILayout.Button("Bottom", _buttonStyle)) { SetPlaneRotation(planeA, ref px, ref py, ref pz, ref rx, ref ry, ref rz, new Float3(90f, 0f, 0f)); }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Apply " + title, _buttonStyle))
            {
                ApplyPlane(planeA, px, py, pz, rx, ry, rz);
            }
        }

        private void ApplyHalfCutPreset(string preset)
        {
            var selected = _controller.GetSelectedModelOrNull();
            if (selected == null)
            {
                return;
            }

            var center = GetMeshCenterLocal(selected.Mesh);
            Float3 rotation;
            switch (preset)
            {
                case "Front":
                    rotation = new Float3(0f, -90f, 0f);
                    break;
                case "Back":
                    rotation = new Float3(0f, 90f, 0f);
                    break;
                case "Left":
                    rotation = new Float3(0f, 180f, 0f);
                    break;
                case "Right":
                    rotation = new Float3(0f, 0f, 0f);
                    break;
                case "Up":
                    rotation = new Float3(0f, 0f, -90f);
                    break;
                case "Down":
                    rotation = new Float3(0f, 0f, 90f);
                    break;
                default:
                    rotation = new Float3(0f, 0f, 0f);
                    break;
            }

            _controller.SetSelectedSliceMode(ReferenceSliceMode.OneSide);
            _controller.SetSelectedSliceSide(ReferenceSliceSide.Positive);
            _controller.SetSelectedSlicePlane1(center, rotation);

            _sliceApx = center.X.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceApy = center.Y.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceApz = center.Z.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceArx = rotation.X.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceAry = rotation.Y.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceArz = rotation.Z.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static Float3 GetMeshCenterLocal(ReferenceMeshData mesh)
        {
            if (mesh == null || mesh.Vertices == null || mesh.Vertices.Length == 0)
            {
                return Float3.Zero;
            }

            var minX = mesh.Vertices[0].X;
            var minY = mesh.Vertices[0].Y;
            var minZ = mesh.Vertices[0].Z;
            var maxX = minX;
            var maxY = minY;
            var maxZ = minZ;

            for (var i = 1; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Vertices[i];
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Z < minZ) minZ = v.Z;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
                if (v.Z > maxZ) maxZ = v.Z;
            }

            return new Float3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
        }

        private void EnsureSelectedInputs(ReferenceModel selected)
        {
            if (selected == null)
            {
                _selectedForInputs = null;
                return;
            }

            if (_selectedForInputs.HasValue && _selectedForInputs.Value == selected.Id)
            {
                return;
            }

            _selectedForInputs = selected.Id;
            _opacityInput = selected.Opacity.ToString("0.00", CultureInfo.InvariantCulture);
            _scaleInput = selected.Transform.Scale.X.ToString("0.###", CultureInfo.InvariantCulture);
            _colorHexInput = ToHexColor(selected.TintColor);

            _sliceApx = selected.SlicePlane1Position.X.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceApy = selected.SlicePlane1Position.Y.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceApz = selected.SlicePlane1Position.Z.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceArx = selected.SlicePlane1RotationEuler.X.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceAry = selected.SlicePlane1RotationEuler.Y.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceArz = selected.SlicePlane1RotationEuler.Z.ToString("0.###", CultureInfo.InvariantCulture);

            _sliceBpx = selected.SlicePlane2Position.X.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceBpy = selected.SlicePlane2Position.Y.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceBpz = selected.SlicePlane2Position.Z.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceBrx = selected.SlicePlane2RotationEuler.X.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceBry = selected.SlicePlane2RotationEuler.Y.ToString("0.###", CultureInfo.InvariantCulture);
            _sliceBrz = selected.SlicePlane2RotationEuler.Z.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void ApplyPresetColor(string hex)
        {
            _colorHexInput = hex;
            ApplySelectedColorFromHex();
        }

        private void ApplySelectedColorFromHex()
        {
            Float4 color;
            if (!TryParseHexColor(_colorHexInput, out color))
            {
                _state.SetStatus(T("status.invalidColor"));
                return;
            }

            _controller.SetSelectedTintColor(color);
            _colorHexInput = ToHexColor(color);
        }

        private static bool TryParseHexColor(string hex, out Float4 color)
        {
            color = Float4.White;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            var text = hex.Trim();
            if (text.StartsWith("#"))
            {
                text = text.Substring(1);
            }

            if (text.Length != 6 && text.Length != 8)
            {
                return false;
            }

            int value;
            if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            if (text.Length == 6)
            {
                var r = ((value >> 16) & 0xFF) / 255f;
                var g = ((value >> 8) & 0xFF) / 255f;
                var b = (value & 0xFF) / 255f;
                color = new Float4(r, g, b, 1f);
                return true;
            }

            var rr = ((value >> 24) & 0xFF) / 255f;
            var gg = ((value >> 16) & 0xFF) / 255f;
            var bb = ((value >> 8) & 0xFF) / 255f;
            var aa = (value & 0xFF) / 255f;
            color = new Float4(rr, gg, bb, aa);
            return true;
        }

        private static string ToHexColor(Float4 color)
        {
            var r = Mathf.Clamp(Mathf.RoundToInt(color.X * 255f), 0, 255);
            var g = Mathf.Clamp(Mathf.RoundToInt(color.Y * 255f), 0, 255);
            var b = Mathf.Clamp(Mathf.RoundToInt(color.Z * 255f), 0, 255);
            var a = Mathf.Clamp(Mathf.RoundToInt(color.W * 255f), 0, 255);
            if (a >= 255)
            {
                return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", r, g, b);
            }

            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", r, g, b, a);
        }

        private static float ParseFloatOrDefault(string text, float fallback)
        {
            float parsed;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private void DrawNumberAdjustRow(ref string valueText, float step, float min, float max, Action<float> applyValue, string format)
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("-", _buttonStyle))
            {
                var current = ParseFloatOrDefault(valueText, min);
                var next = Mathf.Clamp(current - step, min, max);
                valueText = next.ToString(format, CultureInfo.InvariantCulture);
                applyValue(next);
            }

            valueText = DrawLargeTextField(valueText);

            if (GUILayout.Button("+", _buttonStyle))
            {
                var current = ParseFloatOrDefault(valueText, min);
                var next = Mathf.Clamp(current + step, min, max);
                valueText = next.ToString(format, CultureInfo.InvariantCulture);
                applyValue(next);
            }

            if (GUILayout.Button("Apply", _buttonStyle))
            {
                var parsed = ParseFloatOrDefault(valueText, min);
                var clamped = Mathf.Clamp(parsed, min, max);
                valueText = clamped.ToString(format, CultureInfo.InvariantCulture);
                applyValue(clamped);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTransformNudgeButtons(bool move)
        {
            float step;
            if (move)
            {
                step = ParseFloatOrDefault(_moveStepInput, 0.25f);
                step = Mathf.Max(0.001f, step);
            }
            else
            {
                step = ParseFloatOrDefault(_rotateStepInput, 15f);
                step = Mathf.Max(0.1f, step);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("X-", _buttonStyle)) { NudgeSelected(move, -step, 0f, 0f); }
            if (GUILayout.Button("X+", _buttonStyle)) { NudgeSelected(move, step, 0f, 0f); }
            if (GUILayout.Button("Y-", _buttonStyle)) { NudgeSelected(move, 0f, -step, 0f); }
            if (GUILayout.Button("Y+", _buttonStyle)) { NudgeSelected(move, 0f, step, 0f); }
            if (GUILayout.Button("Z-", _buttonStyle)) { NudgeSelected(move, 0f, 0f, -step); }
            if (GUILayout.Button("Z+", _buttonStyle)) { NudgeSelected(move, 0f, 0f, step); }
            GUILayout.EndHorizontal();
        }

        private void NudgeSelected(bool move, float x, float y, float z)
        {
            var selected = _controller.GetSelectedModelOrNull();
            if (selected == null)
            {
                return;
            }

            if (move)
            {
                var p = selected.Transform.Position;
                _controller.MoveSelected(new Float3(p.X + x, p.Y + y, p.Z + z));
            }
            else
            {
                var r = selected.Transform.RotationEuler;
                _controller.RotateSelected(new Float3(r.X + x, r.Y + y, r.Z + z));
            }
        }

        private void SetSelectedRotationAbsolute(Float3 euler)
        {
            if (_controller.GetSelectedModelOrNull() == null)
            {
                return;
            }

            _controller.RotateSelected(euler);
        }

        private void NudgePlane(bool planeA, ref string px, ref string py, ref string pz, ref string rx, ref string ry, ref string rz, float dx, float dy, float dz, float drx, float dry, float drz)
        {
            var p = ParseVector3(px, py, pz);
            var r = ParseVector3(rx, ry, rz);
            p = new Float3(p.X + dx, p.Y + dy, p.Z + dz);
            r = new Float3(r.X + drx, r.Y + dry, r.Z + drz);

            px = p.X.ToString("0.###", CultureInfo.InvariantCulture);
            py = p.Y.ToString("0.###", CultureInfo.InvariantCulture);
            pz = p.Z.ToString("0.###", CultureInfo.InvariantCulture);
            rx = r.X.ToString("0.###", CultureInfo.InvariantCulture);
            ry = r.Y.ToString("0.###", CultureInfo.InvariantCulture);
            rz = r.Z.ToString("0.###", CultureInfo.InvariantCulture);

            ApplyPlane(planeA, px, py, pz, rx, ry, rz);
        }

        private void ApplyPlane(bool planeA, string px, string py, string pz, string rx, string ry, string rz)
        {
            var p = ParseVector3(px, py, pz);
            var r = ParseVector3(rx, ry, rz);
            if (planeA)
            {
                _controller.SetSelectedSlicePlane1(p, r);
            }
            else
            {
                _controller.SetSelectedSlicePlane2(p, r);
            }
        }

        private void DrawVector3Inputs(ref string x, ref string y, ref string z)
        {
            GUILayout.BeginHorizontal();
            x = DrawLargeTextField(x);
            y = DrawLargeTextField(y);
            z = DrawLargeTextField(z);
            GUILayout.EndHorizontal();
        }

        private static Float3 ParseVector3(string x, string y, string z)
        {
            return new Float3(
                ParseFloatOrDefault(x, 0f),
                ParseFloatOrDefault(y, 0f),
                ParseFloatOrDefault(z, 0f));
        }

        private void SetPlaneRotation(bool planeA, ref string px, ref string py, ref string pz, ref string rx, ref string ry, ref string rz, Float3 euler)
        {
            rx = euler.X.ToString("0.###", CultureInfo.InvariantCulture);
            ry = euler.Y.ToString("0.###", CultureInfo.InvariantCulture);
            rz = euler.Z.ToString("0.###", CultureInfo.InvariantCulture);
            ApplyPlane(planeA, px, py, pz, rx, ry, rz);
        }

        private string DrawLargeTextField(string value)
        {
            return GUILayout.TextField(value ?? string.Empty, _textFieldStyle, GUILayout.Height(_buttonStyle.fixedHeight));
        }

        private static string GetDisplayVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v == null)
            {
                return "0.0.0";
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = Math.Max(24, GUI.skin.label.fontSize * 2);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.fontSize = _labelStyle.fontSize;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = Math.Max(24, GUI.skin.button.fontSize * 2);
            _buttonStyle.fixedHeight = 42f;

            _textFieldStyle = new GUIStyle(GUI.skin.textField);
            _textFieldStyle.fontSize = _buttonStyle.fontSize;
            _textFieldStyle.fixedHeight = _buttonStyle.fixedHeight;
        }

        private void RefreshPickerFiles()
        {
            var folder = ModAssets.GetModelScanFolderPath();
            Directory.CreateDirectory(folder);

            var patterns = new[] { "*.stl", "*.obj" };
            var files = new List<string>();
            for (var i = 0; i < patterns.Length; i++)
            {
                var found = Directory.GetFiles(folder, patterns[i], SearchOption.TopDirectoryOnly);
                if (found != null && found.Length > 0)
                {
                    files.AddRange(found);
                }
            }

            _pickerFiles = files.ToArray();
            if (_pickerFiles.Length == 0)
            {
                _state.SetStatus(T("status.noFilesFound", folder));
            }
            else
            {
                _state.SetStatus(T("status.filesFound", _pickerFiles.Length.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private void UpdatePanelInputLockFromGui()
        {
            var shouldLock = false;
            if (_state.IsVisible && Event.current != null)
            {
                var point = Event.current.mousePosition;
                shouldLock = _windowRect.Contains(point) ||
                             (_isPickerVisible && _pickerRect.Contains(point)) ||
                             (_isSliceVisible && _sliceRect.Contains(point)) ||
                             GUIUtility.hotControl != 0;
            }

            if (shouldLock)
            {
                ApplyPanelInputLock();
            }
            else
            {
                RemovePanelInputLock();
            }
        }

        private void ApplyPanelInputLock()
        {
            if (_inputLocked)
            {
                return;
            }

            try
            {
                var controlTypesType = Type.GetType("ControlTypes, Assembly-CSharp");
                var inputLockType = Type.GetType("InputLockManager, Assembly-CSharp");
                if (controlTypesType == null || inputLockType == null)
                {
                    return;
                }

                long mask = 0;
                mask |= TryGetControlTypesValue(controlTypesType, "EDITOR_LOCK");
                mask |= TryGetControlTypesValue(controlTypesType, "EDITOR_ICON_HOVER");
                mask |= TryGetControlTypesValue(controlTypesType, "EDITOR_PAD_PICK_PLACE");
                mask |= TryGetControlTypesValue(controlTypesType, "EDITOR_PAD_PICK_COPY");
                mask |= TryGetControlTypesValue(controlTypesType, "EDITOR_GIZMO_TOOLS");
                mask |= TryGetControlTypesValue(controlTypesType, "CAMERACONTROLS");

                if (mask == 0)
                {
                    mask = TryGetControlTypesValue(controlTypesType, "ALLBUTCAMERAS");
                }

                if (mask == 0)
                {
                    return;
                }

                var flags = Enum.ToObject(controlTypesType, mask);
                var setLock = inputLockType.GetMethod("SetControlLock", new[] { controlTypesType, typeof(string) });
                if (setLock != null)
                {
                    setLock.Invoke(null, new[] { flags, (object)InputLockId });
                    _inputLocked = true;
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteWarning("Failed to apply editor input lock. " + ex.Message);
            }
        }

        private void RemovePanelInputLock()
        {
            if (!_inputLocked)
            {
                return;
            }

            try
            {
                var inputLockType = Type.GetType("InputLockManager, Assembly-CSharp");
                if (inputLockType == null)
                {
                    _inputLocked = false;
                    return;
                }

                var removeLock = inputLockType.GetMethod("RemoveControlLock", new[] { typeof(string) });
                if (removeLock != null)
                {
                    removeLock.Invoke(null, new object[] { InputLockId });
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteWarning("Failed to remove editor input lock. " + ex.Message);
            }
            finally
            {
                _inputLocked = false;
            }
        }

        private static long TryGetControlTypesValue(Type enumType, string name)
        {
            try
            {
                if (!Enum.IsDefined(enumType, name))
                {
                    return 0;
                }

                var value = Enum.Parse(enumType, name);
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                DebugLog.WriteWarning("ControlTypes reflection lookup failed for '" + name + "'. " + ex.Message);
                return 0;
            }
        }
    }
}
