using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Assimp;
using DomainDrivenGameEngine.Media.Models;
using DomainDrivenGameEngine.Media.Services;
using DomainMesh = DomainDrivenGameEngine.Media.Models.Mesh;
using DomainTexture = DomainDrivenGameEngine.Media.Models.Texture;

namespace DomainDrivenGameEngine.Media.AssimpNet
{
    /// <summary>
    /// An AssimpNet-based source for sourcing models.
    /// </summary>
    public class AssimpNetModelSourceService : BaseMediaSourceService<Model>
    {
        /// <summary>
        /// The extensions this source service supports.
        /// </summary>
        private static readonly IReadOnlyCollection<string> SupportedExtensions = new string[]
        {
            ".fbx",
            ".dae",
            ".gltf",
            ".glb",
            ".blend",
            ".3ds",
            ".ase",
            ".obj",
            ".ifc",
            ".xgl",
            ".zgl",
            ".ply",
            ".dxf",
            ".lwo",
            ".lws",
            ".lxo",
            ".stl",
            ".x",
            ".ac",
            ".ms3d",
            ".cob",
            ".scn",
        };

        /// <summary>
        /// Services for sourcing textures from embedded texture data.
        /// </summary>
        private IMediaSourceService<DomainTexture>[] _textureSourceServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssimpNetModelSourceService"/> class.
        /// </summary>
        /// <param name="textureSourceServices">Services for sourcing textures from embedded texture data.</param>
        public AssimpNetModelSourceService(IMediaSourceService<DomainTexture>[] textureSourceServices)
            : base(SupportedExtensions)
        {
            _textureSourceServices = textureSourceServices ?? throw new ArgumentNullException(nameof(textureSourceServices));
        }

        /// <inheritdoc/>
        public override Model Load(Stream stream, string path, string extension)
        {
            var pathDirectory = Path.GetFullPath(Path.GetDirectoryName(path));
            using (var context = new AssimpContext())
            {
                var postProcessSteps = PostProcessSteps.GenerateSmoothNormals |
                                       PostProcessSteps.CalculateTangentSpace |
                                       PostProcessSteps.Triangulate |
                                       PostProcessSteps.FlipUVs;

                var scene = context.ImportFileFromStream(stream, postProcessSteps, Path.GetExtension(path));

                var embeddedTextures = scene.Textures.Select(ConvertToDomainTexture).ToList();

                var meshes = new List<DomainMesh>();
                foreach (var sceneMesh in scene.Meshes)
                {
                    var positions = sceneMesh.Vertices;
                    var normals = sceneMesh.Normals;
                    var tangents = sceneMesh.Tangents;
                    var colors = sceneMesh.HasVertexColors(0) ? sceneMesh.VertexColorChannels[0] : null;
                    var textureCoordinates = sceneMesh.HasTextureCoords(0) ? sceneMesh.TextureCoordinateChannels[0] : null;

                    var vertices = new List<Vertex>();
                    for (var i = 0; i < positions.Count; i++)
                    {
                        var position = positions[i];
                        var normal = normals[i];
                        var tangent = tangents[i];
                        var color = colors?[i];
                        var textureCoordinate = textureCoordinates?[i];

                        vertices.Add(new Vertex(new Vector3(position.X, position.Y, position.Z),
                                                new Vector3(normal.X, normal.Y, normal.Z),
                                                new Vector3(tangent.X, tangent.Y, tangent.Z),
                                                color != null ? new VertexColor(color.Value.R, color.Value.G, color.Value.B, color.Value.A) : new VertexColor(1.0f, 1.0f, 1.0f, 1.0f),
                                                textureCoordinate != null ? new Vector2(textureCoordinate.Value.X, textureCoordinate.Value.Y) : Vector2.Zero));
                    }

                    var indices = sceneMesh.GetUnsignedIndices();

                    var material = sceneMesh.MaterialIndex >= 0
                        ? scene.Materials.ElementAtOrDefault(sceneMesh.MaterialIndex)
                        : null;

                    var textures = material.GetAllMaterialTextures();

                    var texturePaths = textures?.Where(t => !string.IsNullOrWhiteSpace(t.FilePath) && !t.FilePath.StartsWith("*"))
                                               .Select(t => t.FilePath)
                                               .ToList();

                    var embeddedTextureIndices = textures?.Where(t => !string.IsNullOrWhiteSpace(t.FilePath) && t.FilePath.StartsWith("*"))
                                                         .Select(t => (uint?)uint.Parse(t.FilePath.Remove(0, 1)))
                                                         .ToList();

                    meshes.Add(new DomainMesh(vertices,
                                              indices,
                                              texturePaths,
                                              embeddedTextureIndices: embeddedTextureIndices));
                }

                return new Model(meshes, embeddedTextures);
            }
        }

        /// <summary>
        /// Converts a texture embedded in the model to a domain texture.
        /// </summary>
        /// <param name="embeddedTexture">The embedded texture to convert.</param>
        /// <returns>The resulting domain texture.</returns>
        private DomainTexture ConvertToDomainTexture(EmbeddedTexture embeddedTexture)
        {
            if (embeddedTexture.IsCompressed)
            {
                var extension = $".{embeddedTexture.CompressedFormatHint}";
                var sourceService = _textureSourceServices.FirstOrDefault(s => s.IsExtensionSupported(extension));
                if (sourceService == null)
                {
                    throw new Exception($"No available source service for loading embedded texture with extension '{extension}'.");
                }

                using (var memoryStream = new MemoryStream(embeddedTexture.CompressedData))
                {
                    return sourceService.Load(memoryStream, string.Empty, extension);
                }
            }

            var bytes = new byte[embeddedTexture.Width * embeddedTexture.Height * 4];
            int index = 0;
            foreach (var texel in embeddedTexture.NonCompressedData)
            {
                bytes[index] = texel.R;
                bytes[index + 1] = texel.G;
                bytes[index + 2] = texel.B;
                bytes[index + 3] = texel.A;
                index += 4;
            }

            return new DomainTexture(embeddedTexture.Width, embeddedTexture.Height, PixelFormat.Rgba8, bytes);
        }
    }
}
