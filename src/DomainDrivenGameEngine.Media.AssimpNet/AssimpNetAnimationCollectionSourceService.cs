using System.IO;
using Assimp;
using DomainDrivenGameEngine.Media.Models;
using DomainDrivenGameEngine.Media.Services;

namespace DomainDrivenGameEngine.Media.AssimpNet
{
    /// <summary>
    /// An AssimpNet-based source for sourcing animation collections.
    /// </summary>
    public class AssimpNetAnimationCollectionSourceService : BaseMediaSourceService<AnimationCollection>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssimpNetAnimationCollectionSourceService"/> class.
        /// </summary>
        public AssimpNetAnimationCollectionSourceService()
            : base(AssimpNetFormatConstants.SupportedExtensions)
        {
        }

        /// <inheritdoc/>
        public override AnimationCollection Load(Stream stream, string path, string extension)
        {
            using (var context = new AssimpContext())
            {
                var scene = context.ImportFileFromStream(stream, PostProcessSteps.None, extension);

                return scene.GetAnimationCollection();
            }
        }
    }
}
