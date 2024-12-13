using Collections;
using System;
using Unmanaged;
using Vortice.Vulkan;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPipeline : IDisposable
    {
        public const uint DescriptorSets = 1024;

        public readonly Pipeline pipeline;
        public readonly PipelineLayout pipelineLayout;
        public readonly DescriptorSetLayout setLayout;

        private readonly Array<(VkDescriptorType, uint)> poolTypes;
        private readonly Array<(byte, VkDescriptorType, VkShaderStageFlags)> bindings;
        private readonly List<DescriptorPool> pools;

        public readonly USpan<(byte, VkDescriptorType, VkShaderStageFlags)> Bindings => bindings.AsSpan();

        public CompiledPipeline(Pipeline pipeline, PipelineLayout pipelineLayout, USpan<(VkDescriptorType, uint)> poolTypes, DescriptorSetLayout setLayout, USpan<(byte, VkDescriptorType, VkShaderStageFlags)> bindings)
        {
            this.pipeline = pipeline;
            this.pipelineLayout = pipelineLayout;
            this.poolTypes = new(poolTypes);
            this.setLayout = setLayout;
            this.bindings = new(bindings);
            pools = new();
            CreateNewPool();
        }

        public readonly void Dispose()
        {
            foreach (DescriptorPool pool in pools)
            {
                pool.Dispose();
            }

            pools.Dispose();
            bindings.Dispose();
            setLayout.Dispose();
            poolTypes.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();
        }

        private readonly void CreateNewPool()
        {
            pools.Add(new DescriptorPool(pipelineLayout.logicalDevice, poolTypes.AsSpan(), DescriptorSets));
        }

        public readonly DescriptorSet Allocate()
        {
            DescriptorPool lastPool = pools[pools.Count - 1];
            if (lastPool.TryAllocate(setLayout, out DescriptorSet descriptorSet))
            {
                return descriptorSet;
            }
            else
            {
                CreateNewPool();
                lastPool = pools[pools.Count - 1];
                return lastPool.Allocate(setLayout);
            }
        }
    }
}