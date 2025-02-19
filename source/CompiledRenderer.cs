using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledRenderer : IDisposable, IEquatable<CompiledRenderer>
    {
        public readonly DescriptorSet descriptorSet;

        public CompiledRenderer(DescriptorSet descriptorSet)
        {
            this.descriptorSet = descriptorSet;
        }

        public readonly void Dispose()
        {
            descriptorSet.Dispose();
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is CompiledRenderer renderer && Equals(renderer);
        }

        public readonly bool Equals(CompiledRenderer other)
        {
            return descriptorSet.Equals(other.descriptorSet);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(descriptorSet);
        }

        public static bool operator ==(CompiledRenderer left, CompiledRenderer right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CompiledRenderer left, CompiledRenderer right)
        {
            return !(left == right);
        }
    }
}