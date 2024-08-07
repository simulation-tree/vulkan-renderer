using System;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct PhysicalDevice
    {
        private readonly VkPhysicalDevice value;

        public readonly VkPhysicalDevice Value
        {
            get
            {
                return value;
            }
        }

        internal PhysicalDevice(VkPhysicalDevice value)
        {
            this.value = value;
        }

        public readonly VkPhysicalDeviceProperties GetProperties()
        {
            vkGetPhysicalDeviceProperties(value, out VkPhysicalDeviceProperties properties);
            return properties;
        }

        public readonly ReadOnlySpan<VkExtensionProperties> GetExtensionProperties()
        {
            return vkEnumerateDeviceExtensionProperties(value);
        }

        public readonly VkPhysicalDeviceFeatures GetFeatures()
        {
            vkGetPhysicalDeviceFeatures(value, out VkPhysicalDeviceFeatures features);
            return features;
        }

        public readonly VkSurfaceCapabilitiesKHR GetSurfaceCapabilities(Surface surface)
        {
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
            return vkGetPhysicalDeviceSurfaceFormatsKHR(value, surface.Value);
        }

        public readonly ReadOnlySpan<VkPresentModeKHR> GetSurfacePresentModes(Surface surface)
        {
            return vkGetPhysicalDeviceSurfacePresentModesKHR(value, surface.Value);
        }

        public readonly VkPhysicalDeviceLimits GetLimits()
        {
            vkGetPhysicalDeviceProperties(value, out VkPhysicalDeviceProperties properties);
            return properties.limits;
        }

        public readonly ReadOnlySpan<VkQueueFamilyProperties> GetQueueFamilies()
        {
            ReadOnlySpan<VkQueueFamilyProperties> queueFamilies = vkGetPhysicalDeviceQueueFamilyProperties(value);
            return queueFamilies;
        }

        public readonly uint GetGraphicsQueueFamily()
        {
            ReadOnlySpan<VkQueueFamilyProperties> queueFamilies = GetQueueFamilies();
            for (int i = 0; i < queueFamilies.Length; i++)
            {
                VkQueueFamilyProperties queueFamily = queueFamilies[i];
                if ((queueFamily.queueFlags & VkQueueFlags.Graphics) != VkQueueFlags.None)
                {
                    return (uint)i;
                }
            }

            throw new Exception("No graphics queue family found");
        }
    }
}
