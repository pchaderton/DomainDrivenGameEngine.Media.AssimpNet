using System.Collections.Generic;

namespace DomainDrivenGameEngine.Media.AssimpNet
{
    /// <summary>
    /// Constants for providing which formats Assimp.Net supports.
    /// </summary>
    public static class AssimpNetFormatConstants
    {
        /// <summary>
        /// The extensions Assimp.Net supports.
        /// </summary>
        public static readonly IReadOnlyCollection<string> SupportedExtensions = new string[]
        {
            // .gltf is not supported as it can't be easily loaded via the stream loading the domain requires.
            ".fbx",
            ".dae",
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
        /// Initializes static members of the <see cref="AssimpNetFormatConstants"/> class.
        /// </summary>
        static AssimpNetFormatConstants()
        {
        }
    }
}
