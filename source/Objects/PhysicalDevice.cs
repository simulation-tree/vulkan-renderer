using System;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public readonly struct PhysicalDevice : IEquatable<PhysicalDevice>
    {
        private readonly VkPhysicalDevice value;

        public readonly VkPhysicalDevice Value => value;

        internal PhysicalDevice(VkPhysicalDevice value)
        {
            this.value = value;
        }

        /// <summary>
        /// The general properties of the physical device.
        /// </summary>
        public readonly VkPhysicalDeviceProperties GetProperties()
        {
            vkGetPhysicalDeviceProperties(value, out VkPhysicalDeviceProperties properties);
            return properties;
        }

        /// <summary>
        /// All available extensions of the physical device.
        /// </summary>
        public readonly ReadOnlySpan<VkExtensionProperties> GetExtensions()
        {
            return vkEnumerateDeviceExtensionProperties(value);
        }

        /// <summary>
        /// All available features of the physical device.
        /// </summary>
        public readonly VkPhysicalDeviceFeatures GetFeatures()
        {
            vkGetPhysicalDeviceFeatures(value, out VkPhysicalDeviceFeatures features);
            return features;
        }

        public unsafe readonly VkSurfaceCapabilitiesKHR GetSurfaceCapabilities(Surface surface)
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

        public readonly ReadOnlySpan<VkQueueFamilyProperties> GetAllQueueFamilies()
        {
            return vkGetPhysicalDeviceQueueFamilyProperties(value);
        }

        /// <summary>
        /// Returns the queue family indices for the graphics and present queues.
        /// </summary>
        public readonly (uint graphics, uint present) GetQueueFamilies(Surface surface)
        {
            ReadOnlySpan<VkQueueFamilyProperties> queueFamilies = GetAllQueueFamilies();
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

                vkGetPhysicalDeviceSurfaceSupportKHR(value, i, surface.Value, out VkBool32 supportsPresenting);
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

        public readonly bool TryGetGraphicsQueueFamily(out uint graphicsFamily)
        {
            ReadOnlySpan<VkQueueFamilyProperties> queueFamilies = GetAllQueueFamilies();
            for (uint i = 0; i < queueFamilies.Length; i++)
            {
                VkQueueFamilyProperties queueFamily = queueFamilies[(int)i];
                if ((queueFamily.queueFlags & VkQueueFlags.Graphics) != VkQueueFlags.None)
                {
                    graphicsFamily = i;
                    return true;
                }
            }

            graphicsFamily = default;
            return false;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is PhysicalDevice device && Equals(device);
        }

        public readonly bool Equals(PhysicalDevice other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(PhysicalDevice left, PhysicalDevice right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhysicalDevice left, PhysicalDevice right)
        {
            return !(left == right);
        }
    }
}