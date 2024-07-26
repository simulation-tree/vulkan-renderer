using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Surface : IDisposable, IEquatable<Surface>
    {
        public readonly Instance instance;

        private readonly VkSurfaceKHR value;
        private bool valid;

        public readonly VkSurfaceKHR Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public Surface(Instance instance, nint existingValue)
        {
            this.instance = instance;
            value = new((ulong)existingValue);
            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            valid = false;
            vkDestroySurfaceKHR(instance.Value, value);
        }

        public readonly SwapchainCapabilities GetSwapchainInfo(PhysicalDevice physicalDevice)
        {
            ThrowIfDisposed();
            VkSurfaceCapabilitiesKHR capabilities = physicalDevice.GetSurfaceCapabilities(this);
            ReadOnlySpan<VkSurfaceFormatKHR> formats = physicalDevice.GetSurfaceFormats(this);
            ReadOnlySpan<VkPresentModeKHR> presentModes = physicalDevice.GetSurfacePresentModes(this);
            return new(capabilities, formats, presentModes);
        }

        public readonly SwapchainCapabilities GetSwapchainInfo(LogicalDevice device)
        {
            return GetSwapchainInfo(device.physicalDevice);
        }

        /// <summary>
        /// Returns the queue family indices for the graphics and present queues.
        /// </summary>
        public readonly (uint graphics, uint present) GetQueueFamily(LogicalDevice device)
        {
            return GetQueueFamily(device.physicalDevice);
        }

        /// <summary>
        /// Returns the queue family indices for the graphics and present queues.
        /// </summary>
        public readonly (uint graphics, uint present) GetQueueFamily(PhysicalDevice physicalDevice)
        {
            ThrowIfDisposed();
            ReadOnlySpan<VkQueueFamilyProperties> queueFamilies = vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice.Value);
            uint graphicsFamily = VK_QUEUE_FAMILY_IGNORED;
            uint presentFamily = VK_QUEUE_FAMILY_IGNORED;
            for (uint i = 0; i < queueFamilies.Length; i++)
            {
                VkQueueFamilyProperties queueFamily = queueFamilies[(int)i];
                if ((queueFamily.queueFlags & VkQueueFlags.Graphics) != VkQueueFlags.None)
                {
                    graphicsFamily = i;
                    continue;
                }

                vkGetPhysicalDeviceSurfaceSupportKHR(physicalDevice.Value, i, value, out VkBool32 supportsPresenting);
                if (supportsPresenting)
                {
                    presentFamily = i;
                }

                if (graphicsFamily != VK_QUEUE_FAMILY_IGNORED && presentFamily != VK_QUEUE_FAMILY_IGNORED)
                {
                    break;
                }
            }

            return (graphicsFamily, presentFamily);
        }

        public readonly bool TryGetBestSize(LogicalDevice device, out uint width, out uint height)
        {
            ThrowIfDisposed();
            SwapchainCapabilities swapchainInfo = GetSwapchainInfo(device);
            VkSurfaceCapabilitiesKHR capabilities = swapchainInfo.capabilities;
            if (capabilities.currentExtent.width > 0)
            {
                width = capabilities.currentExtent.width;
                height = capabilities.currentExtent.height;
                return true;
            }

            width = default;
            height = default;
            return false;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Surface surface && Equals(surface);
        }

        public readonly bool Equals(Surface other)
        {
            if (IsDisposed && other.IsDisposed)
            {
                return true;
            }

            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(Surface left, Surface right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Surface left, Surface right)
        {
            return !(left == right);
        }
    }
}
