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

        public PipelineLayout(LogicalDevice logicalDevice)
        {
            this.logicalDevice = logicalDevice;
            VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new();
            VkResult result = vkCreatePipelineLayout(logicalDevice.Value, &pipelineLayoutCreateInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create pipeline layout: {result}");
            }

            valid = true;
        }

        public PipelineLayout(LogicalDevice device, ReadOnlySpan<DescriptorSetLayout> setLayouts)
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
    }
}
