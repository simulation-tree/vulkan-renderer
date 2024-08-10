using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledRenderer : IDisposable
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
    }
}