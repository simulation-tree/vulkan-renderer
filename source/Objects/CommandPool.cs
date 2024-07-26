using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
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

        public CommandPool(Queue queue)
        {
            this.logicalDevice = queue.logicalDevice;
            VkCommandPoolCreateInfo commandPoolCreateInfo = new()
            {
                queueFamilyIndex = queue.familyIndex,
                flags = VkCommandPoolCreateFlags.Transient | VkCommandPoolCreateFlags.ResetCommandBuffer
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

        public readonly CommandBuffer CreateCommandBuffer()
        {
            ThrowIfDisposed();
            return new CommandBuffer(this);
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
