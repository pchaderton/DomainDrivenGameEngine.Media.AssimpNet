using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Assimp;
using DomainDrivenGameEngine.Media.Models;
using AssimpAnimation = Assimp.Animation;
using DomainAnimation = DomainDrivenGameEngine.Media.Models.Animation;
using DomainChannel = DomainDrivenGameEngine.Media.Models.Channel;
using DomainQuaternionKeyFrame = DomainDrivenGameEngine.Media.Models.KeyFrame<System.Numerics.Quaternion>;
using DomainVector3KeyFrame = DomainDrivenGameEngine.Media.Models.KeyFrame<System.Numerics.Vector3>;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace DomainDrivenGameEngine.Media.AssimpNet
{
    /// <summary>
    /// Extension methods for the Assimp <see cref="Scene"/> class.
    /// </summary>
    public static class SceneAnimationParsingExtensions
    {
        /// <summary>
        /// Gets an animation collection from an Assimp scene.
        /// </summary>
        /// <param name="scene">The <see cref="Scene"/> to read animation sets from.</param>
        /// <returns>The resulting <see cref="AnimationCollection"/>.</returns>
        public static AnimationCollection GetAnimationCollection(this Scene scene)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            var animations = new List<DomainAnimation>();
            foreach (var animation in scene.Animations)
            {
                animations.Add(ConvertToDomainAnimation(animation));
            }

            return new AnimationCollection(animations);
        }

        /// <summary>
        /// Converts an <see cref="AssimpAnimation"/> to a <see cref="DomainAnimation"/>.
        /// </summary>
        /// <param name="animation">The <see cref="AssimpAnimation"/> to convert.</param>
        /// <returns>The resulting <see cref="DomainAnimation"/>.</returns>
        private static DomainAnimation ConvertToDomainAnimation(AssimpAnimation animation)
        {
            var ticksPerSecond = animation.TicksPerSecond;
            if (ticksPerSecond <= 0.0)
            {
                ticksPerSecond = 30.0;
            }

            var durationInSeconds = animation.DurationInTicks / ticksPerSecond;

            var channels = new List<DomainChannel>();
            foreach (var channel in animation.NodeAnimationChannels)
            {
                var rotationKeyFrames = channel.RotationKeys
                                               .Select(rk => GetDomainQuaternionKeyFrame(rk, ticksPerSecond))
                                               .ToArray();
                var offsetKeyFrames = channel.PositionKeys
                                             .Select(ok => GetDomainVector3KeyFrame(ok, ticksPerSecond))
                                             .ToArray();
                var scaleKeyFrames = channel.ScalingKeys
                                            .Select(sk => GetDomainVector3KeyFrame(sk, ticksPerSecond))
                                            .ToArray();

                channels.Add(new DomainChannel(channel.NodeName,
                                               new ReadOnlyCollection<DomainQuaternionKeyFrame>(rotationKeyFrames),
                                               new ReadOnlyCollection<DomainVector3KeyFrame>(offsetKeyFrames),
                                               new ReadOnlyCollection<DomainVector3KeyFrame>(scaleKeyFrames)));
            }

            return new DomainAnimation(animation.Name,
                                       new ReadOnlyCollection<DomainChannel>(channels.ToArray()),
                                       durationInSeconds);
        }

        /// <summary>
        /// Converts a <see cref="QuaternionKey"/> to a <see cref="DomainQuaternionKeyFrame"/>.
        /// </summary>
        /// <param name="assimpQuaternionKey">The source <see cref="QuaternionKey"/>.</param>
        /// <param name="ticksPerSecond">The number of ticks per second for the animation.</param>
        /// <returns>The resulting <see cref="DomainQuaternionKeyFrame"/>.</returns>
        private static DomainQuaternionKeyFrame GetDomainQuaternionKeyFrame(QuaternionKey assimpQuaternionKey, double ticksPerSecond)
        {
            var assimpQuaternion = assimpQuaternionKey.Value;

            assimpQuaternion.Normalize();

            var quaternion = new NumericsQuaternion(assimpQuaternion.X, assimpQuaternion.Y, assimpQuaternion.Z, assimpQuaternion.W);

            return new DomainQuaternionKeyFrame(assimpQuaternionKey.Time / ticksPerSecond, quaternion);
        }

        /// <summary>
        /// Converts a <see cref="VectorKey"/> to a <see cref="DomainQuaternionKeyFrame"/>.
        /// </summary>
        /// <param name="assimpVectorKey">The source <see cref="QuaternionKey"/>.</param>
        /// <param name="ticksPerSecond">The number of ticks per second for the animation.</param>
        /// <returns>The resulting <see cref="DomainQuaternionKeyFrame"/>.</returns>
        private static DomainVector3KeyFrame GetDomainVector3KeyFrame(VectorKey assimpVectorKey, double ticksPerSecond)
        {
            var assimpVector3d = assimpVectorKey.Value;

            var vector = new NumericsVector3(assimpVector3d.X, assimpVector3d.Y, assimpVector3d.Z);

            return new DomainVector3KeyFrame(assimpVectorKey.Time / ticksPerSecond, vector);
        }
    }
}
