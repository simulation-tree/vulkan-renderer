using System;
using System.Diagnostics;
using Unmanaged;
using Unmanaged.Collections;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct LogicalDevice : IDisposable, IEquatable<LogicalDevice>
    {
        private static readonly FixedString[] deviceExtensions =
        {
            new(VK_KHR_SWAPCHAIN_EXTENSION_NAME)
        };

#if DEBUG
        private static readonly string[] deviceLayers =
        {
            "VK_LAYER_KHRONOS_validation"
        };
#endif

        public readonly PhysicalDevice physicalDevice;

        private readonly VkDevice value;
        private bool valid;

        public readonly VkDevice Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public LogicalDevice(PhysicalDevice physicalDevice, ReadOnlySpan<uint> queueFamilies)
        {
            this.physicalDevice = physicalDevice;
            VkPhysicalDeviceProperties properties = physicalDevice.GetProperties();
            ReadOnlySpan<VkExtensionProperties> availableDeviceExtensions = physicalDevice.GetExtensionProperties();

            float priority = 1f;
            uint queueCount = 0;
            VkDeviceQueueCreateInfo* queueCreateInfos = stackalloc VkDeviceQueueCreateInfo[queueFamilies.Length];
            foreach (uint queueFamily in queueFamilies)
            {
                VkDeviceQueueCreateInfo queueCreateInfo = new();
                queueCreateInfo.queueFamilyIndex = queueFamily;
                queueCreateInfo.queueCount = 1;
                queueCreateInfo.pQueuePriorities = &priority;
                queueCreateInfos[queueCount++] = queueCreateInfo;
            }

            VkPhysicalDeviceFeatures features = new();
            features.samplerAnisotropy = true;

            using UnmanagedList<VkUtf8String> vkDeviceExtensions = UnmanagedList<VkUtf8String>.Create();
            Span<byte> nameBuffer = stackalloc byte[FixedString.MaxLength];
            foreach (FixedString extension in deviceExtensions)
            {
                int length = extension.CopyTo(nameBuffer);
                fixed (byte* ptr = nameBuffer)
                {
                    vkDeviceExtensions.Add(new(ptr, length));
                }
            }

            using VkStringArray deviceExtensionNames = new(vkDeviceExtensions);
            VkDeviceCreateInfo createInfo = new()
            {
                queueCreateInfoCount = queueCount,
                pQueueCreateInfos = queueCreateInfos,
                enabledExtensionCount = deviceExtensionNames.Length,
                ppEnabledExtensionNames = deviceExtensionNames,
                pEnabledFeatures = &features,
#if DEBUG
                enabledLayerCount = (uint)deviceLayers.Length,
                ppEnabledLayerNames = new VkStringArray(deviceLayers)
#endif
            };

            VkResult result = vkCreateDevice(physicalDevice.Value, &createInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create logical device: {result}");
            }

            valid = true;
            vkLoadDevice(value);
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(LogicalDevice));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            vkDestroyDevice(value);
            valid = false;
        }

        /// <summary>
        /// Waits for the device to become idle after any submitted work
        /// on any queue.
        /// </summary>
        public readonly void Wait()
        {
            ThrowIfDisposed();
            vkDeviceWaitIdle(value);
        }

        public readonly VkFormat GetDepthFormat()
        {
            return GetSupportedFormat([VkFormat.D32Sfloat, VkFormat.D32SfloatS8Uint, VkFormat.D24UnormS8Uint], VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
        }

        public readonly VkFormat GetSupportedFormat(ReadOnlySpan<VkFormat> candidates, VkImageTiling tiling, VkFormatFeatureFlags features)
        {
            ThrowIfDisposed();
            foreach (VkFormat format in candidates)
            {
                VkFormatProperties properties;
                vkGetPhysicalDeviceFormatProperties(physicalDevice.Value, format, &properties);

                if (tiling == VkImageTiling.Linear && (properties.linearTilingFeatures & features) == features)
                {
                    return format;
                }
                else if (tiling == VkImageTiling.Optimal && (properties.optimalTilingFeatures & features) == features)
                {
                    return format;
                }
            }

            throw new InvalidOperationException("Failed to find supported format");
        }

        public readonly uint GetMemoryTypeIndex(uint typeFilter, VkMemoryPropertyFlags properties)
        {
            ThrowIfDisposed();
            vkGetPhysicalDeviceMemoryProperties(physicalDevice.Value, out VkPhysicalDeviceMemoryProperties memoryProperties);
            for (uint i = 0; i < memoryProperties.memoryTypeCount; i++)
            {
                VkMemoryType memoryType = memoryProperties.memoryTypes[(int)i];
                if ((typeFilter & (1 << (int)i)) != 0 && (memoryType.propertyFlags & properties) == properties)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("No suitable memory type found");
        }

        public readonly Queue GetQueue(uint family, uint index)
        {
            ThrowIfDisposed();
            return new Queue(this, family, index);
        }

        public readonly VkResult TryAcquireNextImage(Swapchain swapchain, ulong timeout, Semaphore pullSemaphore, Fence fence, out uint imageIndex)
        {
            ThrowIfDisposed();
            return vkAcquireNextImageKHR(value, swapchain.Value, timeout, pullSemaphore.IsDisposed ? VkSemaphore.Null : pullSemaphore.Value, fence.IsDisposed ? VkFence.Null : fence.Value, out imageIndex);
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is LogicalDevice device && Equals(device);
        }

        public readonly bool Equals(LogicalDevice other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(LogicalDevice left, LogicalDevice right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LogicalDevice left, LogicalDevice right)
        {
            return !(left == right);
        }
    }
}
