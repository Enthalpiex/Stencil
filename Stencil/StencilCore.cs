using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Stencil
{
    public sealed class ReferenceMeshData
    {
        public Float3[] Vertices { get; private set; }
        public int[] Triangles { get; private set; }

        public ReferenceMeshData(Float3[] vertices, int[] triangles)
        {
            Vertices = vertices ?? new Float3[0];
            Triangles = triangles ?? new int[0];
        }

        public bool HasGeometry
        {
            get { return Vertices.Length > 0 && Triangles.Length > 0; }
        }
    }

    public sealed class ReferenceModelTransform
    {
        public Float3 Position { get; private set; }
        public Float3 RotationEuler { get; private set; }
        public Float3 Scale { get; private set; }

        public ReferenceModelTransform()
        {
            Position = Float3.Zero;
            RotationEuler = Float3.Zero;
            Scale = Float3.One;
        }

        public void SetPosition(Float3 position)
        {
            Position = position;
        }

        public void SetRotation(Float3 rotationEuler)
        {
            RotationEuler = rotationEuler;
        }

        public void SetScale(Float3 scale)
        {
            if (scale.X <= 0f || scale.Y <= 0f || scale.Z <= 0f)
            {
                throw new ArgumentOutOfRangeException("scale", "Scale must be > 0 on all axes.");
            }

            Scale = scale;
        }
    }

    public sealed class ReferenceModel
    {
        public Guid Id { get; private set; }
        public string SourcePath { get; private set; }
        public ReferenceModelFormat Format { get; private set; }
        public float Opacity { get; private set; }
        public bool RenderTextures { get; private set; }
        public bool AffectsPhysics { get; private set; }
        public ReferenceModelTransform Transform { get; private set; }
        public ReferenceMeshData Mesh { get; private set; }
        public ReferenceDisplayMode DisplayMode { get; private set; }
        public Float4 TintColor { get; private set; }
        public ReferenceSliceMode SliceMode { get; private set; }
        public ReferenceSliceSide SliceSide { get; private set; }
        public Float3 SlicePlane1Position { get; private set; }
        public Float3 SlicePlane1RotationEuler { get; private set; }
        public Float3 SlicePlane2Position { get; private set; }
        public Float3 SlicePlane2RotationEuler { get; private set; }

        public ReferenceModel(string sourcePath, ReferenceModelFormat format, ReferenceMeshData mesh)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Path is required.", "sourcePath");
            }

            Id = Guid.NewGuid();
            SourcePath = sourcePath;
            Format = format;
            Opacity = 0.35f;
            RenderTextures = true;
            AffectsPhysics = false;
            Transform = new ReferenceModelTransform();
            Transform.SetScale(new Float3(0.02f, 0.02f, 0.02f));
            Mesh = mesh ?? new ReferenceMeshData(new Float3[0], new int[0]);
            DisplayMode = ReferenceDisplayMode.Mixed;
            TintColor = Float4.White;
            SliceMode = ReferenceSliceMode.All;
            SliceSide = ReferenceSliceSide.Positive;
            var center = CalculateMeshCenter(Mesh);
            SlicePlane1Position = center;
            SlicePlane1RotationEuler = Float3.Zero;
            SlicePlane2Position = center;
            SlicePlane2RotationEuler = new Float3(0f, 180f, 0f);

            if (format == ReferenceModelFormat.Stl)
            {
                Transform.SetRotation(new Float3(-90f, 0f, 0f));
            }
        }

        private static Float3 CalculateMeshCenter(ReferenceMeshData mesh)
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

        public void SetOpacity(float opacity)
        {
            if (opacity < 0f || opacity > 1f)
            {
                throw new ArgumentOutOfRangeException("opacity", "Opacity must be in [0, 1].");
            }

            Opacity = opacity;
        }

        public void SetTextureRendering(bool enabled)
        {
            RenderTextures = enabled;
        }

        public void SetDisplayMode(ReferenceDisplayMode mode)
        {
            DisplayMode = mode;
        }

        public void SetTintColor(Float4 color)
        {
            if (color.X < 0f || color.X > 1f || color.Y < 0f || color.Y > 1f || color.Z < 0f || color.Z > 1f || color.W < 0f || color.W > 1f)
            {
                throw new ArgumentOutOfRangeException("color", "Tint color channels must be in [0, 1].");
            }

            TintColor = color;
        }

        public void SetSliceMode(ReferenceSliceMode mode)
        {
            SliceMode = mode;
        }

        public void SetSliceSide(ReferenceSliceSide side)
        {
            SliceSide = side;
        }

        public void SetSlicePlane1(Float3 position, Float3 rotationEuler)
        {
            SlicePlane1Position = position;
            SlicePlane1RotationEuler = rotationEuler;
        }

        public void SetSlicePlane2(Float3 position, Float3 rotationEuler)
        {
            SlicePlane2Position = position;
            SlicePlane2RotationEuler = rotationEuler;
        }
    }

    public sealed class StlMeshImporter
    {
        public ReferenceMeshData Import(string localPath)
        {
            if (string.IsNullOrWhiteSpace(localPath))
            {
                throw new ArgumentException("Path is required.", "localPath");
            }

            using (var stream = File.OpenRead(localPath))
            {
                if (LooksLikeBinaryStl(stream))
                {
                    stream.Position = 0;
                    try
                    {
                        var binaryMesh = ReadBinary(stream);
                        if (binaryMesh.HasGeometry)
                        {
                            return binaryMesh;
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog.WriteWarning("Binary STL parse attempt failed; trying ASCII parser. " + ex.Message);
                    }
                }
            }

            var asciiMesh = ReadAscii(localPath);
            if (asciiMesh.HasGeometry)
            {
                return asciiMesh;
            }

            using (var stream = File.OpenRead(localPath))
            {
                try
                {
                    return ReadBinary(stream);
                }
                catch (Exception ex)
                {
                    DebugLog.WriteWarning("Binary STL parser fallback failed; using ASCII result. " + ex.Message);
                    return asciiMesh;
                }
            }
        }

        private static bool LooksLikeBinaryStl(Stream stream)
        {
            if (!stream.CanSeek || stream.Length < 84)
            {
                return false;
            }

            stream.Position = 80;
            var countBytes = new byte[4];
            var read = stream.Read(countBytes, 0, 4);
            if (read != 4)
            {
                return false;
            }

            var triCount = BitConverter.ToUInt32(countBytes, 0);
            var expectedLength = 84L + ((long)triCount * 50L);
            if (expectedLength == stream.Length)
            {
                return true;
            }

            return stream.Length > 84;
        }

        private static ReferenceMeshData ReadBinary(Stream stream)
        {
            var vertices = new List<Float3>();
            var triangles = new List<int>();

            using (var reader = new BinaryReader(stream))
            {
                reader.ReadBytes(80);
                var triangleCount = reader.ReadUInt32();
                for (var i = 0; i < triangleCount; i++)
                {
                    reader.ReadSingle();
                    reader.ReadSingle();
                    reader.ReadSingle();

                    for (var v = 0; v < 3; v++)
                    {
                        var x = reader.ReadSingle();
                        var y = reader.ReadSingle();
                        var z = reader.ReadSingle();
                        vertices.Add(new Float3(x, y, z));
                        triangles.Add(vertices.Count - 1);
                    }

                    reader.ReadUInt16();
                }
            }

            return new ReferenceMeshData(vertices.ToArray(), triangles.ToArray());
        }

        private static ReferenceMeshData ReadAscii(string path)
        {
            var vertices = new List<Float3>();
            var triangles = new List<int>();
            var triVertices = new List<Float3>(3);

            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!line.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var vertex = ParseAsciiVertex(line);
                    triVertices.Add(vertex);
                    if (triVertices.Count == 3)
                    {
                        vertices.Add(triVertices[0]);
                        triangles.Add(vertices.Count - 1);
                        vertices.Add(triVertices[1]);
                        triangles.Add(vertices.Count - 1);
                        vertices.Add(triVertices[2]);
                        triangles.Add(vertices.Count - 1);
                        triVertices.Clear();
                    }
                }
            }

            return new ReferenceMeshData(vertices.ToArray(), triangles.ToArray());
        }

        private static Float3 ParseAsciiVertex(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                throw new FormatException("Invalid STL vertex line: " + line);
            }

            return new Float3(
                ParseFloat(parts[1]),
                ParseFloat(parts[2]),
                ParseFloat(parts[3]));
        }

        private static float ParseFloat(string input)
        {
            return float.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }

    public sealed class ReferenceModelImporter
    {
        private static readonly IDictionary<string, ReferenceModelFormat> ExtensionMap =
            new Dictionary<string, ReferenceModelFormat>(StringComparer.OrdinalIgnoreCase)
            {
                { ".obj", ReferenceModelFormat.Obj },
                { ".fbx", ReferenceModelFormat.Fbx },
                { ".dae", ReferenceModelFormat.Dae },
                { ".stl", ReferenceModelFormat.Stl }
            };

        private sealed class ObjMeshImporter
        {
            private struct FaceVertex
            {
                public int VertexIndex;
            }

            public ReferenceMeshData Import(string localPath)
            {
                var rawVertices = new List<Float3>();
                var outVertices = new List<Float3>();
                var outTriangles = new List<int>();

                using (var reader = new StreamReader(localPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (line.StartsWith("v ", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length < 4)
                            {
                                continue;
                            }

                            rawVertices.Add(new Float3(
                                ParseObjFloat(parts[1]),
                                ParseObjFloat(parts[2]),
                                ParseObjFloat(parts[3])));
                            continue;
                        }

                        if (!line.StartsWith("f ", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var faceParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (faceParts.Length < 4)
                        {
                            continue;
                        }

                        var face = new List<FaceVertex>();
                        for (var i = 1; i < faceParts.Length; i++)
                        {
                            var fv = ParseFaceVertex(faceParts[i], rawVertices.Count);
                            if (fv.VertexIndex < 0 || fv.VertexIndex >= rawVertices.Count)
                            {
                                face.Clear();
                                break;
                            }

                            face.Add(fv);
                        }

                        if (face.Count < 3)
                        {
                            continue;
                        }

                        // Triangulate polygon as a fan: (0, i, i+1)
                        for (var i = 1; i + 1 < face.Count; i++)
                        {
                            AddFaceVertex(face[0], rawVertices, outVertices, outTriangles);
                            AddFaceVertex(face[i], rawVertices, outVertices, outTriangles);
                            AddFaceVertex(face[i + 1], rawVertices, outVertices, outTriangles);
                        }
                    }
                }

                return new ReferenceMeshData(outVertices.ToArray(), outTriangles.ToArray());
            }

            private static FaceVertex ParseFaceVertex(string token, int vertexCount)
            {
                var slash = token.IndexOf('/');
                var vertexToken = slash >= 0 ? token.Substring(0, slash) : token;

                int index;
                if (!int.TryParse(vertexToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                {
                    return new FaceVertex { VertexIndex = -1 };
                }

                // OBJ uses 1-based indices; negative indices are relative to the end.
                if (index > 0)
                {
                    index -= 1;
                }
                else if (index < 0)
                {
                    index = vertexCount + index;
                }

                return new FaceVertex { VertexIndex = index };
            }

            private static void AddFaceVertex(
                FaceVertex fv,
                IList<Float3> rawVertices,
                IList<Float3> outVertices,
                IList<int> outTriangles)
            {
                outVertices.Add(rawVertices[fv.VertexIndex]);
                outTriangles.Add(outVertices.Count - 1);
            }

            private static float ParseObjFloat(string value)
            {
                return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        public ReferenceModel Import(string localPath)
        {
            if (string.IsNullOrWhiteSpace(localPath))
            {
                throw new ArgumentException("Path is required.", "localPath");
            }

            if (!File.Exists(localPath))
            {
                throw new FileNotFoundException("Model file not found.", localPath);
            }

            var ext = Path.GetExtension(localPath);
            ReferenceModelFormat format;
            if (!ExtensionMap.TryGetValue(ext, out format))
            {
                throw new NotSupportedException("Unsupported model format: " + ext);
            }

            ReferenceMeshData mesh;
            switch (format)
            {
                case ReferenceModelFormat.Stl:
                    mesh = new StlMeshImporter().Import(localPath);
                    break;
                case ReferenceModelFormat.Obj:
                    mesh = new ObjMeshImporter().Import(localPath);
                    break;
                case ReferenceModelFormat.Fbx:
                case ReferenceModelFormat.Dae:
                    throw new NotSupportedException(
                        "FBX/DAE import is not implemented in this build yet. Please convert to STL or OBJ.");
                default:
                    throw new NotSupportedException("Unsupported model format: " + ext);
            }

            if (!mesh.HasGeometry)
            {
                throw new InvalidDataException("No mesh geometry found in file: " + Path.GetFileName(localPath));
            }

            return new ReferenceModel(localPath, format, mesh);
        }
    }

    public sealed class EditorReferenceModelService
    {
        private readonly List<ReferenceModel> _models;

        public event Action<ReferenceModel> ModelAdded;
        public event Action<Guid> ModelRemoved;
        public event Action<ReferenceModel> ModelUpdated;

        public EditorReferenceModelService()
        {
            _models = new List<ReferenceModel>();
        }

        public IReadOnlyList<ReferenceModel> Models
        {
            get { return _models.AsReadOnly(); }
        }

        public ReferenceModel AddModel(string localPath)
        {
            var importer = new ReferenceModelImporter();
            var model = importer.Import(localPath);
            _models.Add(model);
            if (ModelAdded != null)
            {
                ModelAdded(model);
            }

            return model;
        }

        public bool RemoveModel(Guid id)
        {
            var model = _models.Find(m => m.Id == id);
            if (model == null)
            {
                return false;
            }

            var removed = _models.Remove(model);
            if (removed && ModelRemoved != null)
            {
                ModelRemoved(id);
            }

            return removed;
        }

        public int RemoveAllModels()
        {
            var ids = new List<Guid>();
            for (var i = 0; i < _models.Count; i++)
            {
                ids.Add(_models[i].Id);
            }

            _models.Clear();

            for (var i = 0; i < ids.Count; i++)
            {
                if (ModelRemoved != null)
                {
                    ModelRemoved(ids[i]);
                }
            }

            return ids.Count;
        }

        public void SetOpacity(Guid id, float opacity)
        {
            var model = GetModelOrThrow(id);
            model.SetOpacity(opacity);
            NotifyUpdated(model);
        }

        public void SetTextureRendering(Guid id, bool enabled)
        {
            var model = GetModelOrThrow(id);
            model.SetTextureRendering(enabled);
            NotifyUpdated(model);
        }

        public void Move(Guid id, Float3 position)
        {
            var model = GetModelOrThrow(id);
            model.Transform.SetPosition(position);
            NotifyUpdated(model);
        }

        public void Rotate(Guid id, Float3 rotationEuler)
        {
            var model = GetModelOrThrow(id);
            model.Transform.SetRotation(rotationEuler);
            NotifyUpdated(model);
        }

        public void Scale(Guid id, Float3 scale)
        {
            var model = GetModelOrThrow(id);
            model.Transform.SetScale(scale);
            NotifyUpdated(model);
        }

        public void SetDisplayMode(Guid id, ReferenceDisplayMode mode)
        {
            var model = GetModelOrThrow(id);
            model.SetDisplayMode(mode);
            NotifyUpdated(model);
        }

        public void SetTintColor(Guid id, Float4 color)
        {
            var model = GetModelOrThrow(id);
            model.SetTintColor(color);
            NotifyUpdated(model);
        }

        public void SetSliceMode(Guid id, ReferenceSliceMode mode)
        {
            var model = GetModelOrThrow(id);
            model.SetSliceMode(mode);
            NotifyUpdated(model);
        }

        public void SetSliceSide(Guid id, ReferenceSliceSide side)
        {
            var model = GetModelOrThrow(id);
            model.SetSliceSide(side);
            NotifyUpdated(model);
        }

        public void SetSlicePlane1(Guid id, Float3 position, Float3 rotationEuler)
        {
            var model = GetModelOrThrow(id);
            model.SetSlicePlane1(position, rotationEuler);
            NotifyUpdated(model);
        }

        public void SetSlicePlane2(Guid id, Float3 position, Float3 rotationEuler)
        {
            var model = GetModelOrThrow(id);
            model.SetSlicePlane2(position, rotationEuler);
            NotifyUpdated(model);
        }

        private ReferenceModel GetModelOrThrow(Guid id)
        {
            var model = _models.Find(m => m.Id == id);
            if (model == null)
            {
                throw new KeyNotFoundException("Reference model not found: " + id);
            }

            return model;
        }

        private void NotifyUpdated(ReferenceModel model)
        {
            if (ModelUpdated != null)
            {
                ModelUpdated(model);
            }
        }
    }

    public static class ModAssets
    {
        public const string ToolbarIconPng = "Stencil.png";
        public const string AppIconIco = "Stencil.ico";

        public static string[] GetToolbarIconCandidatePaths()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return new[]
            {
                Path.Combine(baseDir, "GameData", "Stencil", "Resources", ToolbarIconPng),
                Path.Combine(baseDir, "..", "GameData", "Stencil", "Resources", ToolbarIconPng),
                Path.Combine(baseDir, "GameData", "Stencil", ToolbarIconPng),
                Path.Combine(baseDir, "..", "GameData", "Stencil", ToolbarIconPng),
                Path.Combine(baseDir, ToolbarIconPng),
                Path.Combine(GetModelScanFolderPath(), ToolbarIconPng)
            };
        }

        public static string GetModelScanFolderPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var kspRoot = baseDir;

            var folderName = new DirectoryInfo(baseDir).Name;
            if (string.Equals(folderName, "KSP_x64_Data", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderName, "KSP_Data", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.GetParent(baseDir);
                if (parent != null)
                {
                    kspRoot = parent.FullName;
                }
            }

            return Path.Combine(kspRoot, "Stencil");
        }

    }

    public static class DebugLog
    {
        public static void Write(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.Log("[Stencil] " + message);
        }

        public static void WriteWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.LogWarning("[Stencil] " + message);
        }

        public static void WriteError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.LogError("[Stencil] " + message);
        }
    }

    public sealed class StencilIconProvider
    {
        private readonly Type _texture2DType;
        private object _cachedToolbarIcon;

        public StencilIconProvider()
        {
            _texture2DType = Type.GetType("UnityEngine.Texture2D, UnityEngine");
        }

        public bool IsRuntimeAvailable
        {
            get { return _texture2DType != null; }
        }

        public bool TryGetToolbarIcon(out object texture)
        {
            texture = null;
            if (!IsRuntimeAvailable)
            {
                return false;
            }

            if (_cachedToolbarIcon != null)
            {
                texture = _cachedToolbarIcon;
                return true;
            }

            var dbTexture = TryLoadFromGameDatabase();
            if (dbTexture != null)
            {
                _cachedToolbarIcon = dbTexture;
                texture = dbTexture;
                return true;
            }

            var paths = ModAssets.GetToolbarIconCandidatePaths();
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (!File.Exists(path))
                {
                    continue;
                }

                var bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0)
                {
                    continue;
                }

                var tex = Activator.CreateInstance(_texture2DType, new object[] { 2, 2 });
                var loadImage = _texture2DType.GetMethod("LoadImage", new[] { typeof(byte[]) });
                if (loadImage == null)
                {
                    continue;
                }

                var loaded = loadImage.Invoke(tex, new object[] { bytes });
                if (loaded is bool && !((bool)loaded))
                {
                    continue;
                }

                _cachedToolbarIcon = tex;
                texture = tex;
                return true;
            }

            return false;
        }

        private static Texture2D TryLoadFromGameDatabase()
        {
            try
            {
                if (GameDatabase.Instance == null)
                {
                    return null;
                }

                var texture = GameDatabase.Instance.GetTexture("Stencil/Resources/Stencil", false);
                if (texture != null)
                {
                    return texture;
                }

                texture = GameDatabase.Instance.GetTexture("Stencil/Stencil", false);
                if (texture != null)
                {
                    return texture;
                }

                var textureInfo = GameDatabase.Instance.GetTextureInfo("Stencil/Resources/Stencil");
                if (textureInfo != null)
                {
                    return textureInfo.texture;
                }

                textureInfo = GameDatabase.Instance.GetTextureInfo("Stencil/Stencil");
                if (textureInfo != null)
                {
                    return textureInfo.texture;
                }

                return null;
            }
            catch (Exception ex)
            {
                DebugLog.WriteWarning("Could not load toolbar icon from GameDatabase. " + ex.Message);
                return null;
            }
        }
    }

    public sealed class EditorWindowState
    {
        public bool IsVisible { get; private set; }
        public Guid? SelectedModelId { get; private set; }
        public string PendingImportPath { get; set; }
        public FloatRect WindowRect { get; private set; }
        public string LastStatusMessage { get; private set; }

        public EditorWindowState()
        {
            WindowRect = new FloatRect(240f, 120f, 460f, 420f);
            LastStatusMessage = "Ready";
        }

        public void Show()
        {
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public void Toggle()
        {
            IsVisible = !IsVisible;
        }

        public void SelectModel(Guid? modelId)
        {
            SelectedModelId = modelId;
        }

        public void SetWindowRect(FloatRect rect)
        {
            WindowRect = rect;
        }

        public void SetStatus(string message)
        {
            LastStatusMessage = string.IsNullOrWhiteSpace(message) ? "Ready" : message;
        }
    }

    public sealed class EditorModelListItem
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string Format { get; private set; }
        public bool IsSelected { get; private set; }

        public EditorModelListItem(Guid id, string name, string format, bool isSelected)
        {
            Id = id;
            Name = name;
            Format = format;
            IsSelected = isSelected;
        }
    }

    public sealed class EditorWindowSnapshot
    {
        public bool IsVisible { get; private set; }
        public FloatRect WindowRect { get; private set; }
        public string PendingImportPath { get; private set; }
        public string LastStatusMessage { get; private set; }
        public EditorModelListItem[] Models { get; private set; }

        public EditorWindowSnapshot(
            bool isVisible,
            FloatRect windowRect,
            string pendingImportPath,
            string lastStatusMessage,
            EditorModelListItem[] models)
        {
            IsVisible = isVisible;
            WindowRect = windowRect;
            PendingImportPath = pendingImportPath;
            LastStatusMessage = lastStatusMessage;
            Models = models ?? new EditorModelListItem[0];
        }
    }

    public sealed class EditorWindowController
    {
        private readonly EditorReferenceModelService _service;
        private readonly EditorWindowState _state;

        public EditorWindowController(EditorReferenceModelService service, EditorWindowState state)
        {
            if (service == null)
            {
                throw new ArgumentNullException("service");
            }

            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            _service = service;
            _state = state;
        }

        public EditorWindowState State
        {
            get { return _state; }
        }

        public IReadOnlyList<ReferenceModel> Models
        {
            get { return _service.Models; }
        }

        public Guid ImportFromPendingPath()
        {
            var model = _service.AddModel(_state.PendingImportPath);
            _state.SelectModel(model.Id);
            _state.SetStatus("Imported: " + Path.GetFileName(model.SourcePath));
            return model.Id;
        }

        public bool TryImportFromPendingPath(out Guid importedModelId, out string error)
        {
            importedModelId = Guid.Empty;
            error = null;

            try
            {
                importedModelId = ImportFromPendingPath();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _state.SetStatus("Import failed: " + ex.Message);
                DebugLog.WriteError("ImportFromPendingPath failed. " + ex);
                return false;
            }
        }

        public bool RemoveSelected()
        {
            if (!_state.SelectedModelId.HasValue)
            {
                return false;
            }

            var removed = _service.RemoveModel(_state.SelectedModelId.Value);
            if (removed)
            {
                _state.SelectModel(null);
                _state.SetStatus("Selected model removed.");
            }

            return removed;
        }

        public int RemoveAll()
        {
            var removedCount = _service.RemoveAllModels();
            _state.SelectModel(null);
            _state.SetStatus("Removed models: " + removedCount.ToString(CultureInfo.InvariantCulture));
            return removedCount;
        }

        public bool SelectByIndex(int index)
        {
            if (index < 0 || index >= _service.Models.Count)
            {
                return false;
            }

            _state.SelectModel(_service.Models[index].Id);
            return true;
        }

        public bool SelectNextModel()
        {
            if (_service.Models.Count == 0)
            {
                _state.SelectModel(null);
                return false;
            }

            if (!_state.SelectedModelId.HasValue)
            {
                _state.SelectModel(_service.Models[0].Id);
                return true;
            }

            var currentIndex = FindSelectedIndex();
            if (currentIndex < 0)
            {
                _state.SelectModel(_service.Models[0].Id);
                return true;
            }

            var nextIndex = (currentIndex + 1) % _service.Models.Count;
            _state.SelectModel(_service.Models[nextIndex].Id);
            return true;
        }

        public bool SelectPreviousModel()
        {
            if (_service.Models.Count == 0)
            {
                _state.SelectModel(null);
                return false;
            }

            if (!_state.SelectedModelId.HasValue)
            {
                _state.SelectModel(_service.Models[_service.Models.Count - 1].Id);
                return true;
            }

            var currentIndex = FindSelectedIndex();
            if (currentIndex < 0)
            {
                _state.SelectModel(_service.Models[_service.Models.Count - 1].Id);
                return true;
            }

            var prevIndex = (currentIndex - 1 + _service.Models.Count) % _service.Models.Count;
            _state.SelectModel(_service.Models[prevIndex].Id);
            return true;
        }

        public ReferenceModel GetSelectedModelOrNull()
        {
            if (!_state.SelectedModelId.HasValue)
            {
                return null;
            }

            for (var i = 0; i < _service.Models.Count; i++)
            {
                if (_service.Models[i].Id == _state.SelectedModelId.Value)
                {
                    return _service.Models[i];
                }
            }

            return null;
        }

        public EditorWindowSnapshot GetSnapshot()
        {
            var items = new EditorModelListItem[_service.Models.Count];
            for (var i = 0; i < _service.Models.Count; i++)
            {
                var model = _service.Models[i];
                var selected = _state.SelectedModelId.HasValue && _state.SelectedModelId.Value == model.Id;
                items[i] = new EditorModelListItem(
                    model.Id,
                    Path.GetFileName(model.SourcePath),
                    model.Format.ToString(),
                    selected);
            }

            return new EditorWindowSnapshot(
                _state.IsVisible,
                _state.WindowRect,
                _state.PendingImportPath,
                _state.LastStatusMessage,
                items);
        }

        public void SetSelectedOpacity(float opacity)
        {
            var id = GetSelectedId();
            _service.SetOpacity(id, opacity);
            _state.SetStatus("Opacity set to " + opacity.ToString("0.00", CultureInfo.InvariantCulture));
        }

        public void SetSelectedTextureRendering(bool enabled)
        {
            var id = GetSelectedId();
            _service.SetTextureRendering(id, enabled);
            _state.SetStatus(enabled ? "Texture display enabled." : "Texture display disabled.");
        }

        public void SetSelectedDisplayMode(ReferenceDisplayMode mode)
        {
            var id = GetSelectedId();
            _service.SetDisplayMode(id, mode);
            _state.SetStatus("Display mode: " + mode);
        }

        public void SetSelectedTintColor(Float4 color)
        {
            var id = GetSelectedId();
            _service.SetTintColor(id, color);
            _state.SetStatus("Tint color updated.");
        }

        public void SetSelectedSliceMode(ReferenceSliceMode mode)
        {
            var id = GetSelectedId();
            _service.SetSliceMode(id, mode);
            _state.SetStatus("Slice mode: " + mode);
        }

        public void SetSelectedSliceSide(ReferenceSliceSide side)
        {
            var id = GetSelectedId();
            _service.SetSliceSide(id, side);
            _state.SetStatus("Slice side: " + side);
        }

        public void SetSelectedSlicePlane1(Float3 position, Float3 rotationEuler)
        {
            var id = GetSelectedId();
            _service.SetSlicePlane1(id, position, rotationEuler);
            _state.SetStatus("Slice plane A updated.");
        }

        public void SetSelectedSlicePlane2(Float3 position, Float3 rotationEuler)
        {
            var id = GetSelectedId();
            _service.SetSlicePlane2(id, position, rotationEuler);
            _state.SetStatus("Slice plane B updated.");
        }

        public void SetSelectedUniformScale(float scale)
        {
            if (scale <= 0f)
            {
                throw new ArgumentOutOfRangeException("scale", "Scale must be > 0.");
            }

            ScaleSelected(new Float3(scale, scale, scale));
            _state.SetStatus("Uniform scale set to " + scale.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public void MoveSelected(Float3 position)
        {
            var id = GetSelectedId();
            _service.Move(id, position);
            _state.SetStatus("Position updated.");
        }

        public void RotateSelected(Float3 rotationEuler)
        {
            var id = GetSelectedId();
            _service.Rotate(id, rotationEuler);
            _state.SetStatus("Rotation updated.");
        }

        public void ScaleSelected(Float3 scale)
        {
            var id = GetSelectedId();
            _service.Scale(id, scale);
            _state.SetStatus("Scale updated.");
        }

        private Guid GetSelectedId()
        {
            if (!_state.SelectedModelId.HasValue)
            {
                throw new InvalidOperationException("No reference model selected.");
            }

            return _state.SelectedModelId.Value;
        }

        private int FindSelectedIndex()
        {
            if (!_state.SelectedModelId.HasValue)
            {
                return -1;
            }

            for (var i = 0; i < _service.Models.Count; i++)
            {
                if (_service.Models[i].Id == _state.SelectedModelId.Value)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public sealed class KspReferenceModelRuntime : IDisposable
    {
        private readonly EditorReferenceModelService _service;
        private readonly UnityReflectionFacade _unity;
        private readonly Dictionary<Guid, object> _visuals;

        public KspReferenceModelRuntime(EditorReferenceModelService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException("service");
            }

            _service = service;
            _unity = new UnityReflectionFacade();
            _visuals = new Dictionary<Guid, object>();

            _service.ModelAdded += OnModelAdded;
            _service.ModelUpdated += OnModelUpdated;
            _service.ModelRemoved += OnModelRemoved;
        }

        public bool IsRuntimeAvailable
        {
            get { return _unity.IsAvailable; }
        }

        public void Start()
        {
            for (var i = 0; i < _service.Models.Count; i++)
            {
                OnModelAdded(_service.Models[i]);
            }
        }

        public void Dispose()
        {
            _service.ModelAdded -= OnModelAdded;
            _service.ModelUpdated -= OnModelUpdated;
            _service.ModelRemoved -= OnModelRemoved;

            var ids = new List<Guid>(_visuals.Keys);
            for (var i = 0; i < ids.Count; i++)
            {
                RemoveVisual(ids[i]);
            }
        }

        private void OnModelAdded(ReferenceModel model)
        {
            if (!_unity.IsAvailable || model == null || !model.Mesh.HasGeometry)
            {
                return;
            }

            try
            {
                var go = _unity.CreateReferenceObject("Stencil-Reference-" + model.Id, model.Mesh, model.Opacity);
                _visuals[model.Id] = go;
                _unity.ApplyTransform(go, model.Transform);
                _unity.ApplyVisualMode(go, model);
            }
            catch (Exception ex)
            {
                DebugLog.WriteError("Failed to create runtime visual for model " + model.Id + ". " + ex);
            }
        }

        private void OnModelUpdated(ReferenceModel model)
        {
            if (!_unity.IsAvailable || model == null)
            {
                return;
            }

            object go;
            if (!_visuals.TryGetValue(model.Id, out go))
            {
                OnModelAdded(model);
                return;
            }

            try
            {
                _unity.ApplyOpacity(go, model.Opacity);
                _unity.ApplyTransform(go, model.Transform);
                _unity.ApplyVisualMode(go, model);
            }
            catch (Exception ex)
            {
                DebugLog.WriteError("Failed to update runtime visual for model " + model.Id + ". " + ex);
            }
        }

        private void OnModelRemoved(Guid id)
        {
            RemoveVisual(id);
        }

        private void RemoveVisual(Guid id)
        {
            object go;
            if (!_visuals.TryGetValue(id, out go))
            {
                return;
            }

            _unity.Destroy(go);
            _visuals.Remove(id);
        }
    }

    public sealed class UnityReflectionFacade
    {
        private readonly Type _gameObjectType;
        private readonly Type _meshType;
        private readonly Type _meshFilterType;
        private readonly Type _meshRendererType;
        private readonly Type _shaderType;
        private readonly Type _materialType;
        private readonly Type _vector3Type;
        private readonly Type _colorType;
        private readonly Type _unityObjectType;
        private readonly Type _indexFormatType;
        private readonly Type _meshTopologyType;

        public UnityReflectionFacade()
        {
            _gameObjectType = ResolveUnityType("UnityEngine.GameObject");
            _meshType = ResolveUnityType("UnityEngine.Mesh");
            _meshFilterType = ResolveUnityType("UnityEngine.MeshFilter");
            _meshRendererType = ResolveUnityType("UnityEngine.MeshRenderer");
            _shaderType = ResolveUnityType("UnityEngine.Shader");
            _materialType = ResolveUnityType("UnityEngine.Material");
            _vector3Type = ResolveUnityType("UnityEngine.Vector3");
            _colorType = ResolveUnityType("UnityEngine.Color");
            _unityObjectType = ResolveUnityType("UnityEngine.Object");
            _indexFormatType = ResolveUnityType("UnityEngine.Rendering.IndexFormat");
            _meshTopologyType = ResolveUnityType("UnityEngine.MeshTopology");
        }

        private static Type ResolveUnityType(string fullName)
        {
            var type = Type.GetType(fullName + ", UnityEngine.CoreModule");
            if (type != null)
            {
                return type;
            }

            return Type.GetType(fullName + ", UnityEngine");
        }

        public bool IsAvailable
        {
            get
            {
                return _gameObjectType != null &&
                       _meshType != null &&
                       _meshFilterType != null &&
                       _meshRendererType != null &&
                       _shaderType != null &&
                       _materialType != null &&
                       _vector3Type != null &&
                       _colorType != null &&
                       _unityObjectType != null;
            }
        }

        public object CreateReferenceObject(string name, ReferenceMeshData mesh, float opacity)
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException("Unity runtime is not available.");
            }

            var gameObject = Activator.CreateInstance(_gameObjectType, new object[] { name });
            var addComponent = _gameObjectType.GetMethod("AddComponent", new[] { typeof(Type) });

            var meshFilter = addComponent.Invoke(gameObject, new object[] { _meshFilterType });
            var meshRenderer = addComponent.Invoke(gameObject, new object[] { _meshRendererType });

            var unityMesh = Activator.CreateInstance(_meshType);

            var indexFormatProperty = _meshType.GetProperty("indexFormat");
            if (indexFormatProperty != null && _indexFormatType != null && mesh.Vertices.Length > 65535)
            {
                try
                {
                    var uint32Value = Enum.Parse(_indexFormatType, "UInt32");
                    indexFormatProperty.SetValue(unityMesh, uint32Value, null);
                }
                catch (Exception ex)
                {
                    DebugLog.WriteWarning("Index format UInt32 is unavailable; continuing with default format. " + ex.Message);
                }
            }

            var verticesArray = Array.CreateInstance(_vector3Type, mesh.Vertices.Length);
            for (var i = 0; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Vertices[i];
                var unityVector = Activator.CreateInstance(_vector3Type, new object[] { v.X, v.Y, v.Z });
                verticesArray.SetValue(unityVector, i);
            }

            _meshType.GetProperty("vertices").SetValue(unityMesh, verticesArray, null);
            _meshType.GetProperty("triangles").SetValue(unityMesh, mesh.Triangles, null);

            var recalcNormals = _meshType.GetMethod("RecalculateNormals", Type.EmptyTypes);
            if (recalcNormals != null)
            {
                recalcNormals.Invoke(unityMesh, null);
            }

            var recalcBounds = _meshType.GetMethod("RecalculateBounds", Type.EmptyTypes);
            if (recalcBounds != null)
            {
                recalcBounds.Invoke(unityMesh, null);
            }

            _meshFilterType.GetProperty("sharedMesh").SetValue(meshFilter, unityMesh, null);

            var shaderFind = _shaderType.GetMethod("Find", new[] { typeof(string) });
            var shader = shaderFind.Invoke(null, new object[] { "Transparent/Diffuse" });
            if (shader == null)
            {
                shader = shaderFind.Invoke(null, new object[] { "Standard" });
            }

            var material = Activator.CreateInstance(_materialType, new object[] { shader });
            var color = Activator.CreateInstance(_colorType, new object[] { 1f, 1f, 1f, opacity });
            _materialType.GetProperty("color").SetValue(material, color, null);
            _meshRendererType.GetProperty("material").SetValue(meshRenderer, material, null);

            return gameObject;
        }

        public void ApplyTransform(object gameObject, ReferenceModelTransform transform)
        {
            var transformObj = _gameObjectType.GetProperty("transform").GetValue(gameObject, null);
            var unityPos = Activator.CreateInstance(_vector3Type, new object[]
            {
                transform.Position.X,
                transform.Position.Y,
                transform.Position.Z
            });
            var unityEuler = Activator.CreateInstance(_vector3Type, new object[]
            {
                transform.RotationEuler.X,
                transform.RotationEuler.Y,
                transform.RotationEuler.Z
            });
            var unityScale = Activator.CreateInstance(_vector3Type, new object[]
            {
                transform.Scale.X,
                transform.Scale.Y,
                transform.Scale.Z
            });

            var transformType = transformObj.GetType();
            transformType.GetProperty("position").SetValue(transformObj, unityPos, null);
            transformType.GetProperty("eulerAngles").SetValue(transformObj, unityEuler, null);
            transformType.GetProperty("localScale").SetValue(transformObj, unityScale, null);
        }

        public void ApplyOpacity(object gameObject, float opacity)
        {
            var getComponent = _gameObjectType.GetMethod("GetComponent", new[] { typeof(Type) });
            var renderer = getComponent.Invoke(gameObject, new object[] { _meshRendererType });
            if (renderer == null)
            {
                return;
            }

            var material = _meshRendererType.GetProperty("material").GetValue(renderer, null);
            if (material == null)
            {
                return;
            }

            var colorProperty = _materialType.GetProperty("color");
            if (colorProperty == null)
            {
                return;
            }

            var current = colorProperty.GetValue(material, null);
            if (current == null)
            {
                var fallbackColor = Activator.CreateInstance(_colorType, new object[] { 1f, 1f, 1f, opacity });
                colorProperty.SetValue(material, fallbackColor, null);
                return;
            }

            var colorType = current.GetType();
            var r = ReadColorChannel(current, colorType, "r");
            var g = ReadColorChannel(current, colorType, "g");
            var b = ReadColorChannel(current, colorType, "b");

            var updated = Activator.CreateInstance(_colorType, new object[] { r, g, b, opacity });
            colorProperty.SetValue(material, updated, null);
        }

        private static float ReadColorChannel(object colorInstance, Type colorType, string channel)
        {
            var property = colorType.GetProperty(channel);
            if (property != null)
            {
                return Convert.ToSingle(property.GetValue(colorInstance, null), CultureInfo.InvariantCulture);
            }

            var field = colorType.GetField(channel);
            if (field != null)
            {
                return Convert.ToSingle(field.GetValue(colorInstance), CultureInfo.InvariantCulture);
            }

            return 1f;
        }

        public void ApplyVisualMode(object gameObject, ReferenceModel model)
        {
            if (gameObject == null || model == null)
            {
                return;
            }

            var getComponent = _gameObjectType.GetMethod("GetComponent", new[] { typeof(Type) });
            var meshFilter = getComponent.Invoke(gameObject, new object[] { _meshFilterType });
            var renderer = getComponent.Invoke(gameObject, new object[] { _meshRendererType });
            if (meshFilter == null || renderer == null)
            {
                return;
            }

            var mesh = _meshFilterType.GetProperty("sharedMesh").GetValue(meshFilter, null);
            var material = _meshRendererType.GetProperty("material").GetValue(renderer, null);
            if (mesh == null || material == null)
            {
                return;
            }

            var visibleTriangles = BuildSlicedTriangles(model);
            _meshType.GetProperty("triangles").SetValue(mesh, visibleTriangles, null);

            if (model.DisplayMode == ReferenceDisplayMode.OuterShell)
            {
                ApplyMaterialState(material, "Standard", model.TintColor.X, model.TintColor.Y, model.TintColor.Z, model.Opacity);
                return;
            }

            ApplyMaterialState(material, "Transparent/Diffuse", model.TintColor.X, model.TintColor.Y, model.TintColor.Z, model.Opacity);
        }

        private void ApplyMaterialState(object material, string shaderName, float r, float g, float b, float a)
        {
            var shaderFind = _shaderType.GetMethod("Find", new[] { typeof(string) });
            var shader = shaderFind.Invoke(null, new object[] { shaderName });
            if (shader == null)
            {
                shader = shaderFind.Invoke(null, new object[] { "Standard" });
            }

            var shaderProp = _materialType.GetProperty("shader");
            if (shaderProp != null && shader != null)
            {
                shaderProp.SetValue(material, shader, null);
            }

            var colorProperty = _materialType.GetProperty("color");
            if (colorProperty != null)
            {
                var color = Activator.CreateInstance(_colorType, new object[] { r, g, b, a });
                colorProperty.SetValue(material, color, null);
            }
        }

        private static int[] BuildSlicedTriangles(ReferenceModel model)
        {
            var source = model.Mesh.Triangles;
            if (source == null || source.Length < 3 || model.SliceMode == ReferenceSliceMode.All)
            {
                return source ?? new int[0];
            }

            var vertices = model.Mesh.Vertices;
            var result = new List<int>(source.Length);

            var n1 = RotateVector(new Float3(1f, 0f, 0f), model.SlicePlane1RotationEuler);
            var p1 = model.SlicePlane1Position;
            var n2 = RotateVector(new Float3(1f, 0f, 0f), model.SlicePlane2RotationEuler);
            var p2 = model.SlicePlane2Position;

            for (var i = 0; i + 2 < source.Length; i += 3)
            {
                var v1 = vertices[source[i]];
                var v2 = vertices[source[i + 1]];
                var v3 = vertices[source[i + 2]];

                var k1 = KeepVertex(model, v1, p1, n1, p2, n2);
                var k2 = KeepVertex(model, v2, p1, n1, p2, n2);
                var k3 = KeepVertex(model, v3, p1, n1, p2, n2);

                if (k1 && k2 && k3)
                {
                    result.Add(source[i]);
                    result.Add(source[i + 1]);
                    result.Add(source[i + 2]);
                }
            }

            return result.ToArray();
        }

        private static bool KeepVertex(ReferenceModel model, Float3 v, Float3 p1, Float3 n1, Float3 p2, Float3 n2)
        {
            var d1 = Dot(Subtract(v, p1), n1);
            if (model.SliceMode == ReferenceSliceMode.OneSide)
            {
                return model.SliceSide == ReferenceSliceSide.Positive ? d1 >= 0f : d1 <= 0f;
            }

            var d2 = Dot(Subtract(v, p2), n2);
            var insideBetween = d1 >= 0f && d2 >= 0f;
            return model.SliceSide == ReferenceSliceSide.Positive ? insideBetween : !insideBetween;
        }

        private static Float3 RotateVector(Float3 v, Float3 euler)
        {
            var rx = euler.X * Math.PI / 180.0;
            var ry = euler.Y * Math.PI / 180.0;
            var rz = euler.Z * Math.PI / 180.0;

            var x1 = v.X;
            var y1 = (float)(v.Y * Math.Cos(rx) - v.Z * Math.Sin(rx));
            var z1 = (float)(v.Y * Math.Sin(rx) + v.Z * Math.Cos(rx));

            var x2 = (float)(x1 * Math.Cos(ry) + z1 * Math.Sin(ry));
            var y2 = y1;
            var z2 = (float)(-x1 * Math.Sin(ry) + z1 * Math.Cos(ry));

            var x3 = (float)(x2 * Math.Cos(rz) - y2 * Math.Sin(rz));
            var y3 = (float)(x2 * Math.Sin(rz) + y2 * Math.Cos(rz));
            var z3 = z2;

            return new Float3(x3, y3, z3);
        }

        private static Float3 Subtract(Float3 a, Float3 b)
        {
            return new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        private static float Dot(Float3 a, Float3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        private static int[] BuildLineIndices(int[] triangles)
        {
            if (triangles == null || triangles.Length < 3)
            {
                return new int[0];
            }

            var edges = new HashSet<long>();
            var lines = new List<int>(triangles.Length * 2);

            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                AddEdge(triangles[i], triangles[i + 1], edges, lines);
                AddEdge(triangles[i + 1], triangles[i + 2], edges, lines);
                AddEdge(triangles[i + 2], triangles[i], edges, lines);
            }

            return lines.ToArray();
        }

        private static void AddEdge(int a, int b, HashSet<long> edges, List<int> lines)
        {
            var min = Math.Min(a, b);
            var max = Math.Max(a, b);
            var key = ((long)min << 32) | (uint)max;
            if (!edges.Add(key))
            {
                return;
            }

            lines.Add(a);
            lines.Add(b);
        }

        public void Destroy(object unityObject)
        {
            if (!IsAvailable || unityObject == null)
            {
                return;
            }

            var destroy = _unityObjectType.GetMethod("Destroy", new[] { _unityObjectType });
            if (destroy != null)
            {
                destroy.Invoke(null, new[] { unityObject });
            }
        }
    }
}
