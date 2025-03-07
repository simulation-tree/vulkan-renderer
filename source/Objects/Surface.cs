using System;
using System.Diagnostics;
using Unmanaged;
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

        public Surface(Instance instance, MemoryAddress existingValue)
        {
            this.instance = instance;
            value = new((ulong)existingValue.Address);
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
            USpan<VkSurfaceFormatKHR> formats = physicalDevice.GetSurfaceFormats(this);
            USpan<VkPresentModeKHR> presentModes = physicalDevice.GetSurfacePresentModes(this);
            return new(capabilities, formats, presentModes);
        }

        public readonly bool TryGetBestSize(PhysicalDevice device, out uint width, out uint height)
        {
            ThrowIfDisposed();

            SwapchainCapabilities swapchainInfo = GetSwapchainInfo(device);
            VkSurfaceCapabilitiesKHR capabilities = swapchainInfo.capabilities;
            if (capabilities.currentExtent.width == uint.MaxValue)
            {
                width = default;
                height = default;
                return false;
            }

            width = capabilities.currentExtent.width;
            height = capabilities.currentExtent.height;
            return true;
        }

        public readonly (uint minWidth, uint maxWidth, uint minHeight, uint maxHeight) GetSizeRange(PhysicalDevice device)
        {
            ThrowIfDisposed();

            SwapchainCapabilities swapchainInfo = GetSwapchainInfo(device);
            VkSurfaceCapabilitiesKHR capabilities = swapchainInfo.capabilities;
            return (capabilities.minImageExtent.width, capabilities.maxImageExtent.width, capabilities.minImageExtent.height, capabilities.maxImageExtent.height);
        }

        public readonly (uint min, uint max) GetImageCountRange(PhysicalDevice device)
        {
            ThrowIfDisposed();

            SwapchainCapabilities swapchainInfo = GetSwapchainInfo(device);
            VkSurfaceCapabilitiesKHR capabilities = swapchainInfo.capabilities;
            return (capabilities.minImageCount, capabilities.maxImageCount);
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