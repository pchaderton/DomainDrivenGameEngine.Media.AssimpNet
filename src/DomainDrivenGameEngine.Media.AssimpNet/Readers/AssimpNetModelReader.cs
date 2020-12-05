using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using Assimp;
using DomainDrivenGameEngine.Media.AssimpNet.Extensions;
using DomainDrivenGameEngine.Media.Models;
using DomainDrivenGameEngine.Media.Readers;
using AssimpBone = Assimp.Bone;
using AssimpMatrix4x4 = Assimp.Matrix4x4;
using AssimpMesh = Assimp.Mesh;
using AssimpNode = Assimp.Node;
using DomainBone = DomainDrivenGameEngine.Media.Models.Bone;
using DomainMesh = DomainDrivenGameEngine.Media.Models.Mesh;
using DomainTexture = DomainDrivenGameEngine.Media.Models.Texture;
using NumericsMatrix4x4 = System.Numerics.Matrix4x4;

namespace DomainDrivenGameEngine.Media.AssimpNet.Readers
{
    /// <summary>
    /// An AssimpNet-based reader for reading models.
    /// </summary>
    public class AssimpNetModelReader : BaseMediaReader<Model>
    {
        /// <summary>
        /// Readers for reading textures from embedded texture data.
        /// </summary>
        private IMediaReader<DomainTexture>[] _textureReaders;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssimpNetModelReader"/> class.
        /// </summary>
        /// <param name="textureReader">Readers for reading textures from embedded texture data.</param>
        public AssimpNetModelReader(IMediaReader<DomainTexture>[] textureReader)
            : base(AssimpNetFormatConstants.SupportedExtensions)
        {
            _textureReaders = textureReader ?? throw new ArgumentNullException(nameof(textureReader));
        }

        /// <inheritdoc/>
        public override Model Read(Stream stream, string path, string extension)
        {
            using (var context = new AssimpContext())
            {
                var postProcessSteps = PostProcessSteps.GenerateSmoothNormals |
                                       PostProcessSteps.CalculateTangentSpace |
                                       PostProcessSteps.Triangulate |
                                       PostProcessSteps.FlipUVs;

                var scene = context.ImportFileFromStream(stream, postProcessSteps, extension);

                var embeddedTextures = scene.Textures.Select(ConvertToDomainTexture).ToArray();

                var boneIndexByName = GetBoneIndexByNameLookup(scene.RootNode);

                var meshes = new List<DomainMesh>();
                foreach (var sceneMesh in scene.Meshes)
                {
                    var vertices = GetMeshVertices(sceneMesh, boneIndexByName);

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

                    var meshTextures = material.GetAllMaterialTextures()
                                               ?.Where(t => !string.IsNullOrWhiteSpace(t.FilePath))
                                               .Select(t => GetMeshTexture(t))
                                               .ToArray()
                                               ?? new MeshTexture[0];

                    meshes.Add(new DomainMesh(new ReadOnlyCollection<Vertex>(vertices),
                                              new ReadOnlyCollection<uint>(indices),
                                              new ReadOnlyCollection<MeshTexture>(meshTextures)));
                }

                var skeletonRoot = GetSkeletonNode(scene.RootNode, isRootNode: true);

                return new Model(new ReadOnlyCollection<DomainMesh>(meshes.ToArray()),
                                 new ReadOnlyCollection<DomainTexture>(embeddedTextures),
                                 skeletonRoot,
                                 scene.GetAnimationCollection(),
                                 stream);
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
                var textureReader = _textureReaders.FirstOrDefault(s => s.IsExtensionSupported(extension));
                if (textureReader == null)
                {
                    throw new Exception($"No available reader for loading embedded texture with extension '{extension}'.");
                }

                // Don't automatically dispose of the stream here, as it may be necessary for the final loading
                // process, at which point the stream will be disposed of afterwards.
                var memoryStream = new MemoryStream(embeddedTexture.CompressedData);

                return textureReader.Read(memoryStream, string.Empty, extension);
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

            return new DomainTexture(embeddedTexture.Width, embeddedTexture.Height, TextureFormat.Rgba32, new ReadOnlyCollection<byte>(bytes));
        }

        /// <summary>
        /// Gets a pre-order bone index by name lookup.
        /// </summary>
        /// <param name="node">The <see cref="AssimpNode"/> to get the lookup from.</param>
        /// <returns>A dictionary lookup of name to index.</returns>
        private Dictionary<string, uint> GetBoneIndexByNameLookup(AssimpNode node)
        {
            var nodeStack = new Stack<AssimpNode>();

            nodeStack.Push(node);

            uint index = 0;
            var result = new Dictionary<string, uint>();
            while (nodeStack.Count > 0)
            {
                var currentNode = nodeStack.Pop();

                result.Add(currentNode.Name, index++);

                foreach (var childNode in currentNode.Children.Reverse())
                {
                    nodeStack.Push(childNode);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets vertex data from a given <see cref="AssimpMesh"/>.
        /// </summary>
        /// <param name="sceneMesh">The <see cref="AssimpMesh"/> to get vertex information from.</param>
        /// <param name="boneIndexByName">A lookup of bone index by name information.</param>
        /// <returns>An output list of vertices.</returns>
        private Vertex[] GetMeshVertices(AssimpMesh sceneMesh, Dictionary<string, uint> boneIndexByName)
        {
            var positions = sceneMesh.Vertices;
            var normals = sceneMesh.Normals;
            var tangents = sceneMesh.Tangents;
            var colors = sceneMesh.HasVertexColors(0) ? sceneMesh.VertexColorChannels[0] : null;
            var textureCoordinates = sceneMesh.HasTextureCoords(0) ? sceneMesh.TextureCoordinateChannels[0] : null;
            var boneWeightsByVertexIndex = GetBoneWeightsByVertexIndex(boneIndexByName, sceneMesh);

            var vertices = new List<Vertex>();
            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                var normal = normals[i];
                var tangent = tangents[i];
                var color = colors?[i];
                var textureCoordinate = textureCoordinates?[i];
                TryGetBoneIndicesAndWeights(boneWeightsByVertexIndex, i, out var boneIndices, out var boneWeights);

                vertices.Add(new Vertex(new Vector3(position.X, position.Y, position.Z),
                                        new Vector3(normal.X, normal.Y, normal.Z),
                                        new Vector3(tangent.X, tangent.Y, tangent.Z),
                                        color != null ? new Color(color.Value.R, color.Value.G, color.Value.B, color.Value.A) : new Color(1.0f, 1.0f, 1.0f, 1.0f),
                                        textureCoordinate != null ? new Vector2(textureCoordinate.Value.X, textureCoordinate.Value.Y) : Vector2.Zero,
                                        new ReadOnlyCollection<uint>(boneIndices),
                                        new ReadOnlyCollection<float>(boneWeights)));
            }

            return vertices.ToArray();
        }

        /// <summary>
        /// Gets a lookup of bone indices and weights by vertex index.
        /// </summary>
        /// <param name="boneIndexByName">A lookup of bone name to index.</param>
        /// <param name="sceneMesh">The scene mesh to get weight information from.</param>
        /// <returns>A dictionary lookup of bone indices and weights by vertex index.</returns>
        private Dictionary<int, List<KeyValuePair<uint, float>>> GetBoneWeightsByVertexIndex(Dictionary<string, uint> boneIndexByName,
                                                                                             AssimpMesh sceneMesh)
        {
            return sceneMesh.Bones
                            .SelectMany(bone => bone.VertexWeights.Select(weight => new Tuple<AssimpBone, VertexWeight>(bone, weight)))
                            .GroupBy(tuple => tuple.Item2.VertexID, tuple => new KeyValuePair<uint, float>(boneIndexByName[tuple.Item1.Name], tuple.Item2.Weight))
                            .ToDictionary(group => group.Key, group => group.ToList());
        }

        /// <summary>
        /// Tries to get bone indices and weights for a given vertex.
        /// </summary>
        /// <param name="boneWeightsByVertexIndex">A dictionary lookup of bone indices and weights by vertex index.</param>
        /// <param name="vertexIndex">The index of the vertex to try and get indices and weights for.</param>
        /// <param name="boneIndices">The output indices for the vertex.</param>
        /// <param name="boneWeights">The output weights for the vertex.</param>
        /// <returns><c>true</c> if bone indices and weights could be found for a vertex.</returns>
        private bool TryGetBoneIndicesAndWeights(Dictionary<int, List<KeyValuePair<uint, float>>> boneWeightsByVertexIndex,
                                                 int vertexIndex,
                                                 out uint[] boneIndices,
                                                 out float[] boneWeights)
        {
            boneIndices = null;
            boneWeights = null;
            if (boneWeightsByVertexIndex.TryGetValue(vertexIndex, out var boneWeightKvps))
            {
                boneIndices = boneWeightKvps.Select(kvp => kvp.Key).ToArray();
                boneWeights = boneWeightKvps.Select(kvp => kvp.Value).ToArray();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a <see cref="DomainBone"/> for a given <see cref="AssimpNode"/>.
        /// </summary>
        /// <param name="node">The <see cref="AssimpNode"/> to get a skeleton node from.</param>
        /// <param name="isRootNode">A value indicating if this is the root skeleton node.</param>
        /// <returns>The resulting <see cref="DomainBone"/>.</returns>
        private DomainBone GetSkeletonNode(AssimpNode node, bool isRootNode)
        {
            var childrenBones = node.Children.Select(child => GetSkeletonNode(child, false)).ToArray();

            return new DomainBone(GetNumericsMatrix4x4(node.Transform),
                                  node.Name,
                                  new ReadOnlyCollection<DomainBone>(childrenBones),
                                  computeWorldToBindMatrices: isRootNode);
        }

        /// <summary>
        /// Converts an Assimp <see cref="AssimpMatrix4x4"/> to a numerics <see cref="NumericsMatrix4x4"/>.
        /// </summary>
        /// <param name="input">The input <see cref="AssimpMatrix4x4"/>.</param>
        /// <returns>The output <see cref="NumericsMatrix4x4"/>.</returns>
        private NumericsMatrix4x4 GetNumericsMatrix4x4(AssimpMatrix4x4 input)
        {
            return new NumericsMatrix4x4(input[1, 1],
                                         input[1, 2],
                                         input[1, 3],
                                         input[1, 4],
                                         input[2, 1],
                                         input[2, 2],
                                         input[2, 3],
                                         input[2, 4],
                                         input[3, 1],
                                         input[3, 2],
                                         input[3, 3],
                                         input[3, 4],
                                         input[4, 1],
                                         input[4, 2],
                                         input[4, 3],
                                         input[4, 4]);
        }

        /// <summary>
        /// Converts a <see cref="TextureSlot"/> into a <see cref="MeshTexture"/>.
        /// </summary>
        /// <param name="textureSlot">The source <see cref="TextureSlot"/>.</param>
        /// <returns>The resulting <see cref="MeshTexture"/>.</returns>
        private MeshTexture GetMeshTexture(TextureSlot textureSlot)
        {
            var usageType = GetTextureUsageType(textureSlot.TextureType);
            if (textureSlot.FilePath.StartsWith('*'))
            {
                var embeddedTextureIndex = uint.Parse(textureSlot.FilePath.Remove(0, 1));
                return new MeshTexture(embeddedTextureIndex: embeddedTextureIndex, usageType: usageType);
            }

            return new MeshTexture(path: textureSlot.FilePath, usageType: usageType);
        }

        /// <summary>
        /// Converts a <see cref="TextureType"/> into a <see cref="TextureUsageType"/>.
        /// </summary>
        /// <param name="textureType">The source <see cref="TextureType"/>.</param>
        /// <returns>The resulting <see cref="TextureUsageType"/>.</returns>
        private TextureUsageType GetTextureUsageType(TextureType textureType)
        {
            switch (textureType)
            {
                case TextureType.Diffuse:
                    return TextureUsageType.Diffuse;
                case TextureType.Specular:
                    return TextureUsageType.Specular;
                case TextureType.Ambient:
                    return TextureUsageType.Ambient;
                case TextureType.Emissive:
                    return TextureUsageType.Emission;
                case TextureType.Height:
                    return TextureUsageType.Height;
                case TextureType.Normals:
                    return TextureUsageType.Normal;
                default:
                    return TextureUsageType.Unknown;
            }
        }
    }
}
