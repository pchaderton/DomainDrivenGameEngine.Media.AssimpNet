using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Assimp;
using DomainDrivenGameEngine.Media.Models;
using DomainDrivenGameEngine.Media.Services;
using DomainMesh = DomainDrivenGameEngine.Media.Models.Mesh;

namespace DomainDrivenGameEngine.Media.AssimpNet
{
    /// <summary>
    /// An AssimpNet-based source for sourcing models.
    /// </summary>
    public class AssimpNetModelSourceService : BaseStreamMediaSourceService<Model>
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
        /// Initializes a new instance of the <see cref="AssimpNetModelSourceService"/> class.
        /// </summary>
        /// <param name="fileStreamService">The <see cref="IFileStreamService"/> to use for streaming file data.</param>
        public AssimpNetModelSourceService(IFileStreamService fileStreamService)
            : base(SupportedExtensions, fileStreamService)
        {
        }

        /// <inheritdoc/>
        public override Model Load(Stream stream, string path)
        {
            using (var context = new AssimpContext())
            {
                var postProcessSteps = PostProcessSteps.GenerateSmoothNormals |
                                       PostProcessSteps.CalculateTangentSpace |
                                       PostProcessSteps.Triangulate;

                var scene = context.ImportFileFromStream(stream, postProcessSteps, Path.GetExtension(path));

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
                                                color != null ? new VertexColor(color.Value.R, color.Value.G, color.Value.B, color.Value.A) : default,
                                                textureCoordinate != null ? new Vector2(textureCoordinate.Value.X, textureCoordinate.Value.Y) : Vector2.Zero));
                    }

                    var indices = sceneMesh.GetUnsignedIndices();

                    var material = sceneMesh.MaterialIndex >= 0
                        ? scene.Materials.ElementAtOrDefault(sceneMesh.MaterialIndex)
                        : null;

                    var textures = material.GetAllMaterialTextures()
                                           ?.Select(t => t.FilePath)
                                           .Where(t => !string.IsNullOrWhiteSpace(t))
                                           .ToList();

                    meshes.Add(new DomainMesh(vertices,
                                              indices,
                                              textures));
                }

                return new Model(meshes);
            }
        }
    }
}
