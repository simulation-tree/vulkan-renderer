using System;
using Unmanaged;
using Unmanaged.Collections;
using Vortice.Vulkan;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPipeline : IDisposable
    {
        public readonly Pipeline pipeline;
        public readonly PipelineLayout pipelineLayout;
        public readonly DescriptorPool descriptorPool;
        public readonly DescriptorSetLayout setLayout;

        private readonly UnmanagedArray<(byte, VkDescriptorType, VkShaderStageFlags)> bindings;

        public readonly USpan<(byte, VkDescriptorType, VkShaderStageFlags)> Bindings => bindings.AsSpan();

        public CompiledPipeline(Pipeline pipeline, PipelineLayout pipelineLayout, DescriptorPool descriptorPool, DescriptorSetLayout setLayout, USpan<(byte, VkDescriptorType, VkShaderStageFlags)> bindings)
        {
            this.pipeline = pipeline;
            this.pipelineLayout = pipelineLayout;
            this.descriptorPool = descriptorPool;
            this.setLayout = setLayout;
            this.bindings = new(bindings);
        }

        public readonly void Dispose()
        {
            bindings.Dispose();
            setLayout.Dispose();
            descriptorPool.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();
        }
    }
}