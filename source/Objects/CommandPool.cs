using System;
using System.Diagnostics;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// A command buffers are allocated from.
    /// <para>Not thread safe.</para>
    /// </summary>
    public unsafe struct CommandPool : IDisposable, IEquatable<CommandPool>
    {
        public readonly LogicalDevice logicalDevice;

        private readonly VkCommandPool value;
        private bool valid;

        public readonly VkCommandPool Value
        {
            get
            {

                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public CommandPool(Queue queue, bool allowsOverwrites, bool autoDisposed = false)
        {
            this.logicalDevice = queue.logicalDevice;
            VkCommandPoolCreateFlags flags = default;
            if (!autoDisposed)
            {
                flags |= VkCommandPoolCreateFlags.Transient;
            }

            if (allowsOverwrites)
            {
                flags |= VkCommandPoolCreateFlags.ResetCommandBuffer;
            }

            VkCommandPoolCreateInfo commandPoolCreateInfo = new()
            {
                queueFamilyIndex = queue.familyIndex,
                flags = flags
            };

            VkResult result = vkCreateCommandPool(logicalDevice.Value, &commandPoolCreateInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create command pool: {result}");
            }

            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(CommandPool));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            vkDestroyCommandPool(logicalDevice.Value, value);
            valid = false;
        }

        public readonly CommandBuffer CreateCommandBuffer(bool isPrimary = true)
        {
            ThrowIfDisposed();
            VkCommandBufferAllocateInfo commandBufferAllocateInfo = new()
            {
                commandPool = value,
                level = isPrimary ? VkCommandBufferLevel.Primary : VkCommandBufferLevel.Secondary,
                commandBufferCount = 1
            };

            VkResult result = vkAllocateCommandBuffer(logicalDevice.Value, &commandBufferAllocateInfo, out VkCommandBuffer newBuffer);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to allocate command buffer: {result}");
            }

            return new CommandBuffer(this, newBuffer);
        }

        public readonly void CreateCommandBuffers(USpan<CommandBuffer> buffer, bool isPrimary = true)
        {
            ThrowIfDisposed();
            VkCommandBuffer* newBuffers = stackalloc VkCommandBuffer[(int)buffer.length];
            VkCommandBufferAllocateInfo allocateInfo = new()
            {
                commandPool = value,
                level = isPrimary ? VkCommandBufferLevel.Primary : VkCommandBufferLevel.Secondary,
                commandBufferCount = buffer.length
            };

            VkResult result = vkAllocateCommandBuffers(logicalDevice.Value, &allocateInfo, newBuffers);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to allocate command buffers: {result}");
            }

            for (uint i = 0; i < buffer.length; i++)
            {
                buffer[i] = new CommandBuffer(this, newBuffers[i]);
            }
        }

        public readonly void Reset()
        {
            ThrowIfDisposed();
            vkResetCommandPool(logicalDevice.Value, value, VkCommandPoolResetFlags.None);
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is CommandPool pool && Equals(pool);
        }

        public readonly bool Equals(CommandPool other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(CommandPool left, CommandPool right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandPool left, CommandPool right)
        {
            return !(left == right);
        }
    }
}
