using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// A command buffers are allocated from.
    /// <para>Not thread safe.</para>
    /// </summary>
    [SkipLocalsInit]
    public unsafe struct CommandPool : IDisposable, IEquatable<CommandPool>
    {
        public readonly LogicalDevice logicalDevice;

        internal VkCommandPool value;

        public readonly bool IsDisposed => value.IsNull;

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

            VkResult result = vkCreateCommandPool(logicalDevice.value, &commandPoolCreateInfo, null, out value);
            ThrowIfFailedToCreatePool(result);
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

            vkDestroyCommandPool(logicalDevice.value, value);
            value = default;
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

            VkResult result = vkAllocateCommandBuffer(logicalDevice.value, &commandBufferAllocateInfo, out VkCommandBuffer newBuffer);
            ThrowIfFailedToCreateBuffer(result);

            return new CommandBuffer(this, newBuffer);
        }

        public readonly void CreateCommandBuffers(Span<CommandBuffer> buffer, bool isPrimary = true)
        {
            ThrowIfDisposed();

            Span<VkCommandBuffer> newBuffers = stackalloc VkCommandBuffer[buffer.Length];
            VkCommandBufferAllocateInfo allocateInfo = new()
            {
                commandPool = value,
                level = isPrimary ? VkCommandBufferLevel.Primary : VkCommandBufferLevel.Secondary,
                commandBufferCount = (uint)buffer.Length
            };

            VkResult result = vkAllocateCommandBuffers(logicalDevice.value, &allocateInfo, newBuffers.GetPointer());
            ThrowIfFailedToCreateBuffer(result);

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = new CommandBuffer(this, newBuffers[i]);
            }
        }

        public readonly void Reset()
        {
            ThrowIfDisposed();

            vkResetCommandPool(logicalDevice.value, value, VkCommandPoolResetFlags.None);
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
            return value.GetHashCode();
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToCreatePool(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create command pool: {result}");
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToCreateBuffer(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create command buffer: {result}");
            }
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