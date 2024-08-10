using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct PipelineLayout : IDisposable
    {
        public readonly LogicalDevice logicalDevice;

        private readonly VkPipelineLayout value;
        private bool valid;

        public readonly VkPipelineLayout Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public PipelineLayout(LogicalDevice device)
        {
            this.logicalDevice = device;
            VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new();
            VkResult result = vkCreatePipelineLayout(device.Value, &pipelineLayoutCreateInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create pipeline layout: {result}");
            }

            valid = true;
        }

        public PipelineLayout(LogicalDevice device, DescriptorSetLayout setLayout, ReadOnlySpan<PushConstant> pushConstants) : this(device, [setLayout], pushConstants)
        {
        }

        public PipelineLayout(LogicalDevice device, ReadOnlySpan<DescriptorSetLayout> setLayouts, ReadOnlySpan<PushConstant> pushConstants)
        {
            this.logicalDevice = device;

            VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new();
            if (!setLayouts.IsEmpty)
            {
                VkDescriptorSetLayout* layouts = stackalloc VkDescriptorSetLayout[setLayouts.Length];
                for (int i = 0; i < setLayouts.Length; i++)
                {
                    layouts[i] = setLayouts[i].Value;
                }

                pipelineLayoutCreateInfo.pSetLayouts = layouts;
                pipelineLayoutCreateInfo.setLayoutCount = (uint)setLayouts.Length;
            }

            if (!pushConstants.IsEmpty)
            {
                VkPushConstantRange* constants = stackalloc VkPushConstantRange[pushConstants.Length];
                for (int i = 0; i < pushConstants.Length; i++)
                {
                    PushConstant constant = pushConstants[i];
                    constants[i] = new()
                    {
                        offset = constant.offset,
                        size = constant.size,
                        stageFlags = constant.stage
                    };
                }

                pipelineLayoutCreateInfo.pPushConstantRanges = constants;
                pipelineLayoutCreateInfo.pushConstantRangeCount = (uint)pushConstants.Length;
            }

            VkResult result = vkCreatePipelineLayout(device.Value, &pipelineLayoutCreateInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create pipeline layout: {result}");
            }

            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PipelineLayout));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            vkDestroyPipelineLayout(logicalDevice.Value, value);
            valid = false;
        }

        public readonly struct PushConstant
        {
            public readonly uint offset;
            public readonly uint size;
            public readonly VkShaderStageFlags stage;

            public PushConstant(uint offset, uint size, VkShaderStageFlags stage)
            {
                this.offset = offset;
                this.size = size;
                this.stage = stage;
            }
        }
    }
}
