using Collections.Generic;
using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPipeline : IDisposable
    {
        public const uint DescriptorSets = 1024;

        public readonly Pipeline pipeline;
        public readonly PipelineLayout pipelineLayout;
        public readonly DescriptorSetLayout setLayout;

        private readonly Array<DescriptorPoolSize> poolSizes;
        private readonly Array<DescriptorSetLayoutBinding> descriptorSetLayoutBindings;
        private readonly List<DescriptorPool> pools;

        public readonly Span<DescriptorSetLayoutBinding> DescriptorBindings => descriptorSetLayoutBindings.AsSpan();

        public CompiledPipeline(Pipeline pipeline, PipelineLayout pipelineLayout, ReadOnlySpan<DescriptorPoolSize> poolSizes, DescriptorSetLayout setLayout, ReadOnlySpan<DescriptorSetLayoutBinding> descriptorSetLayoutBindings)
        {
            this.pipeline = pipeline;
            this.pipelineLayout = pipelineLayout;
            this.poolSizes = new(poolSizes);
            this.setLayout = setLayout;
            this.descriptorSetLayoutBindings = new(descriptorSetLayoutBindings);
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
            descriptorSetLayoutBindings.Dispose();
            setLayout.Dispose();
            poolSizes.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();
        }

        private readonly DescriptorPool CreateNewPool()
        {
            DescriptorPool newPool = new(pipelineLayout.logicalDevice, poolSizes.AsSpan(), DescriptorSets);
            pools.Add(newPool);
            return newPool;
        }

        public readonly DescriptorSet Allocate()
        {
            Span<DescriptorPool> pools = this.pools.AsSpan();
            DescriptorPool pool = pools[pools.Length - 1];
            if (pool.TryAllocate(setLayout, out DescriptorSet descriptorSet))
            {
                return descriptorSet;
            }
            else
            {
                pool = CreateNewPool();
                return pool.Allocate(setLayout);
            }
        }
    }
}