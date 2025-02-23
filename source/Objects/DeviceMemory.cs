using System;
using System.Diagnostics;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct DeviceMemory : IDisposable, IEquatable<DeviceMemory>
    {
        public readonly LogicalDevice logicalDevice;
        public readonly ulong size;

        private readonly VkDeviceMemory value;
        private bool valid;

        public readonly VkDeviceMemory Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public DeviceMemory(Buffer buffer, VkMemoryPropertyFlags memoryFlags)
        {
            this.logicalDevice = buffer.logicalDevice;
            vkGetBufferMemoryRequirements(logicalDevice.Value, buffer.Value, out VkMemoryRequirements memRequirements);
            size = memRequirements.size;

            int memoryTypeIndex = -1;
            vkGetPhysicalDeviceMemoryProperties(logicalDevice.physicalDevice.Value, out VkPhysicalDeviceMemoryProperties memProperties);
            for (int i = 0; i < memProperties.memoryTypeCount; i++)
            {
                int x = 1 << i;
                bool containsProperty = (memProperties.memoryTypes[i].propertyFlags & memoryFlags) == memoryFlags;
                bool containsType = (memRequirements.memoryTypeBits & x) == x;
                if (containsType && containsProperty)
                {
                    memoryTypeIndex = i;
                    break;
                }
            }

            if (memoryTypeIndex == -1)
            {
                throw new InvalidOperationException("No suitable memory found");
            }

            VkMemoryAllocateInfo allocInfo = new();
            allocInfo.allocationSize = memRequirements.size;
            allocInfo.memoryTypeIndex = (uint)memoryTypeIndex;

            VkResult result = vkAllocateMemory(logicalDevice.Value, &allocInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Unable to allocate memory");
            }

            result = vkBindBufferMemory(logicalDevice.Value, buffer.Value, value, 0);
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Unable to bind buffer memory");
            }

            valid = true;
        }

        public DeviceMemory(Image image, VkMemoryPropertyFlags memoryFlags)
        {
            this.logicalDevice = image.logicalDevice;
            vkGetImageMemoryRequirements(logicalDevice.Value, image.Value, out VkMemoryRequirements requirements);

            VkMemoryAllocateInfo allocInfo = new();
            allocInfo.allocationSize = requirements.size;
            allocInfo.memoryTypeIndex = logicalDevice.GetMemoryTypeIndex(requirements.memoryTypeBits, memoryFlags);

            VkResult result = vkAllocateMemory(logicalDevice.Value, &allocInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Unable to allocate memory");
            }

            result = vkBindImageMemory(logicalDevice.Value, image.Value, value, 0);
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Unable to bind image memory");
            }

            size = requirements.size;
            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(DeviceMemory));
            }
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfUnableToMap(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Unable to map memory");
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkFreeMemory(logicalDevice.Value, value);
            valid = false;
        }

        public readonly Allocation Map()
        {
            ThrowIfDisposed();

            void* data;
            VkResult result = vkMapMemory(logicalDevice.Value, value, 0, size, 0, &data);
            ThrowIfUnableToMap(result);

            return new(data);
        }

        public readonly void Unmap()
        {
            ThrowIfDisposed();

            vkUnmapMemory(logicalDevice.Value, value);
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is DeviceMemory memory && Equals(memory);
        }

        public readonly bool Equals(DeviceMemory other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(DeviceMemory left, DeviceMemory right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DeviceMemory left, DeviceMemory right)
        {
            return !(left == right);
        }
    }
}