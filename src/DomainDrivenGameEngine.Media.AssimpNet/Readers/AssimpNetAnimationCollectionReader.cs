using System.IO;
using Assimp;
using DomainDrivenGameEngine.Media.AssimpNet.Extensions;
using DomainDrivenGameEngine.Media.Models;
using DomainDrivenGameEngine.Media.Readers;

namespace DomainDrivenGameEngine.Media.AssimpNet.Readers
{
    /// <summary>
    /// An AssimpNet-based reader for reading animation collections.
    /// </summary>
    public class AssimpNetAnimationCollectionReader : BaseMediaReader<AnimationCollection>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssimpNetAnimationCollectionReader"/> class.
        /// </summary>
        public AssimpNetAnimationCollectionReader()
            : base(AssimpNetFormatConstants.SupportedExtensions)
        {
        }

        /// <inheritdoc/>
        public override AnimationCollection Read(Stream stream, string path, string extension)
        {
            using (var context = new AssimpContext())
            {
                var scene = context.ImportFileFromStream(stream, PostProcessSteps.None, extension);

                return scene.GetAnimationCollection();
            }
        }
    }
}
