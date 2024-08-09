using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPipeline : IDisposable
    {
        public readonly Pipeline pipeline;
        public readonly DescriptorPool descriptorPool;

        public CompiledPipeline(Pipeline pipeline, DescriptorPool descriptorPool)
        {
            this.pipeline = pipeline;
            this.descriptorPool = descriptorPool;
        }

        public readonly void Dispose()
        {
            descriptorPool.Dispose();
            pipeline.Dispose();
        }
    }
}