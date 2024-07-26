using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct PhysicalDevice : IDisposable
    {
        private readonly VkPhysicalDevice value;
        private bool valid;

        public readonly VkPhysicalDevice Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        internal PhysicalDevice(VkPhysicalDevice value)
        {
            this.value = value;
            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PhysicalDevice));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            valid = false;
        }

        public readonly VkPhysicalDeviceProperties GetProperties()
        {
            ThrowIfDisposed();
            vkGetPhysicalDeviceProperties(value, out VkPhysicalDeviceProperties properties);
            return properties;
        }

        public readonly ReadOnlySpan<VkExtensionProperties> GetExtensionProperties()
        {
            ThrowIfDisposed();
            return vkEnumerateDeviceExtensionProperties(value);
        }

        public readonly VkPhysicalDeviceFeatures GetFeatures()
        {
            ThrowIfDisposed();
            vkGetPhysicalDeviceFeatures(value, out VkPhysicalDeviceFeatures features);
            return features;
        }

        public readonly VkSurfaceCapabilitiesKHR GetSurfaceCapabilities(Surface surface)
        {
            ThrowIfDisposed();
            VkSurfaceCapabilitiesKHR capabilities = default;
            VkResult result = vkGetPhysicalDeviceSurfaceCapabilitiesKHR(value, surface.Value, &capabilities);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to get physical device surface capabilities: {result}");
            }

            return capabilities;
        }

        public readonly ReadOnlySpan<VkSurfaceFormatKHR> GetSurfaceFormats(Surface surface)
        {
            ThrowIfDisposed();
            return vkGetPhysicalDeviceSurfaceFormatsKHR(value, surface.Value);
        }

        public readonly ReadOnlySpan<VkPresentModeKHR> GetSurfacePresentModes(Surface surface)
        {
            ThrowIfDisposed();
            return vkGetPhysicalDeviceSurfacePresentModesKHR(value, surface.Value);
        }

        public readonly VkPhysicalDeviceLimits GetLimits()
        {
            ThrowIfDisposed();
            vkGetPhysicalDeviceProperties(value, out VkPhysicalDeviceProperties properties);
            return properties.limits;
        }
    }
}
