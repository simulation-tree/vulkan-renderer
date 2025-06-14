using Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    [SkipLocalsInit]
    public unsafe struct LogicalDevice : IDisposable, IEquatable<LogicalDevice>
    {
#if DEBUG
        private static readonly string[] deviceLayers =
        {
            "VK_LAYER_KHRONOS_validation"
        };
#endif

        public readonly PhysicalDevice physicalDevice;

        internal VkDevice value;

        public readonly bool IsDisposed => value.IsNull;

        public LogicalDevice(PhysicalDevice physicalDevice, Span<uint> queueFamilies, Span<ASCIIText256> deviceExtensions)
        {
            this.physicalDevice = physicalDevice;
            float priority = 1f;
            Span<VkDeviceQueueCreateInfo> queueCreateInfos = stackalloc VkDeviceQueueCreateInfo[queueFamilies.Length];
            for (int i = 0; i < queueFamilies.Length; i++)
            {
                uint queueFamily = queueFamilies[i];
                VkDeviceQueueCreateInfo queueCreateInfo = new();
                queueCreateInfo.queueFamilyIndex = queueFamily;
                queueCreateInfo.queueCount = 1;
                queueCreateInfo.pQueuePriorities = &priority;
                queueCreateInfos[i] = queueCreateInfo;
            }

            VkPhysicalDeviceFeatures features = new();
            features.samplerAnisotropy = true;

            using Array<VkUtf8String> vkDeviceExtensions = new(deviceExtensions.Length);
            Span<byte> nameBuffer = stackalloc byte[ASCIIText256.Capacity];
            nameBuffer.Clear();
            for (int i = 0; i < deviceExtensions.Length; i++)
            {
                ASCIIText256 extension = deviceExtensions[i];
                int length = extension.CopyTo(nameBuffer);
                vkDeviceExtensions[i] = new(nameBuffer.GetPointer(), length);
            }

            using VkStringArray deviceExtensionNames = new(vkDeviceExtensions);
            VkDeviceCreateInfo createInfo = new()
            {
                queueCreateInfoCount = (uint)queueCreateInfos.Length,
                pQueueCreateInfos = queueCreateInfos.GetPointer(),
                enabledExtensionCount = deviceExtensionNames.Length,
                ppEnabledExtensionNames = deviceExtensionNames,
                pEnabledFeatures = &features,
#if DEBUG
                enabledLayerCount = (uint)deviceLayers.Length,
                ppEnabledLayerNames = new VkStringArray(deviceLayers)
#endif
            };

            VkResult result = vkCreateDevice(physicalDevice.value, &createInfo, null, out value);
            ThrowIfUnableToCreate(result);

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
            value = default;
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
            Span<VkFormat> candidates = [VkFormat.D32Sfloat, VkFormat.D32SfloatS8Uint, VkFormat.D24UnormS8Uint];
            return GetSupportedFormat(candidates, VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
        }

        public readonly VkFormat GetSupportedFormat(ReadOnlySpan<VkFormat> candidates, VkImageTiling tiling, VkFormatFeatureFlags features)
        {
            ThrowIfDisposed();

            foreach (VkFormat format in candidates)
            {
                VkFormatProperties properties;
                vkGetPhysicalDeviceFormatProperties(physicalDevice.value, format, &properties);

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

        /// <summary>
        /// Retrieves the index for a suitable memory type based on the given
        /// <paramref name="memoryRequirements"/> and <paramref name="propertyFlags"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public readonly uint GetMemoryTypeIndex(VkMemoryRequirements memoryRequirements, VkMemoryPropertyFlags propertyFlags)
        {
            ThrowIfDisposed();

            vkGetPhysicalDeviceMemoryProperties(physicalDevice.value, out VkPhysicalDeviceMemoryProperties memoryProperties);
            for (int i = 0; i < memoryProperties.memoryTypeCount; i++)
            {
                if ((memoryRequirements.memoryTypeBits & (1 << i)) != 0 && (memoryProperties.memoryTypes[i].propertyFlags & propertyFlags) == propertyFlags)
                {
                    //contains the type and the properties
                    return (uint)i;
                }
            }

            throw new InvalidOperationException("No suitable memory type found");
        }

        public readonly Queue GetQueue(uint family, uint index)
        {
            ThrowIfDisposed();
            return new Queue(this, family, index);
        }

        public readonly VkResult TryAcquireNextImage(Swapchain swapchain, Semaphore pullSemaphore, Fence fence, out uint imageIndex)
        {
            ThrowIfDisposed();
            return vkAcquireNextImageKHR(value, swapchain.value, ulong.MaxValue, pullSemaphore.value, fence.value, out imageIndex);
        }

        public readonly VkResult TryAcquireNextImage(Swapchain swapchain, ulong timeout, Semaphore pullSemaphore, Fence fence, out uint imageIndex)
        {
            ThrowIfDisposed();
            return vkAcquireNextImageKHR(value, swapchain.value, timeout, pullSemaphore.value, fence.value, out imageIndex);
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

        [Conditional("DEBUG")]
        private static void ThrowIfUnableToCreate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Unable to create logical device: {result}");
            }
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