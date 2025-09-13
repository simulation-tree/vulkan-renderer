using Collections.Generic;
using Rendering;
using System;
using System.Diagnostics;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Library : IDisposable, IEquatable<Library>
    {
        public readonly VkVersion version;

        private Text name;

        public readonly bool IsDisposed => name.IsDisposed;
        public readonly ReadOnlySpan<char> Name => name.AsSpan();

        /// <summary>
        /// Initializes the vulkan library.
        /// </summary>
        public Library() : this(default)
        {
        }

        /// <summary>
        /// Initializes the vulkan library.
        /// </summary>
        public Library(ReadOnlySpan<char> libraryName)
        {
            name = new(libraryName);
            VkResult result = vkInitialize(libraryName.Length == 0 ? null : libraryName.ToString());
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to initialize graphics library: {result}");
            }

            version = vkEnumerateInstanceVersion();
            if (version < VkVersion.Version_1_1)
            {
                throw new PlatformNotSupportedException("Vulkan 1.1 or above is required");
            }
        }

        public void Dispose()
        {
            name.Dispose();
            name = default;
        }

        public readonly Instance CreateInstance(ReadOnlySpan<char> applicationName, ReadOnlySpan<char> engineName, ReadOnlySpan<DestinationExtension> extensions)
        {
            return new(this, applicationName, engineName, extensions);
        }

        public readonly Instance CreateInstance(string applicationName, string engineName, ReadOnlySpan<DestinationExtension> extensions)
        {
            return CreateInstance(applicationName.AsSpan(), engineName.AsSpan(), extensions);
        }

        /// <summary>
        /// Retrieves a new list containing names of all available global layers.
        /// </summary>
        public readonly Array<ASCIIText256> GetGlobalLayers()
        {
            uint count = 0;
            VkResult result = vkEnumerateInstanceLayerProperties(&count, null);
            ThrowIfFailedToEnumerateInstanceLayerProperties(result);

            if (count > 0)
            {
                VkLayerProperties* properties = stackalloc VkLayerProperties[(int)count];
                result = vkEnumerateInstanceLayerProperties(&count, properties);
                ThrowIfFailedToEnumerateInstanceLayerProperties(result);

                Array<ASCIIText256> availableInstanceLayers = new((int)count);
                for (int i = 0; i < count; i++)
                {
                    availableInstanceLayers[i] = new(properties[i].layerName);
                }

                return availableInstanceLayers;
            }
            else
            {
                return new();
            }
        }

        /// <summary>
        /// Retrieves a new list containing names of all available global extensions.
        /// </summary>
        public readonly Array<ASCIIText256> GetGlobalExtensions()
        {
            uint count = 0;
            VkResult result = vkEnumerateInstanceExtensionProperties(&count, null);
            ThrowIfFailedToEnumerateInstanceExtensionProperties(result);

            if (count > 0)
            {
                VkExtensionProperties* extensionProperties = stackalloc VkExtensionProperties[(int)count];
                result = vkEnumerateInstanceExtensionProperties(&count, extensionProperties);
                ThrowIfFailedToEnumerateInstanceExtensionProperties(result);

                Array<ASCIIText256> availableInstanceExtensions = new((int)count);
                for (int i = 0; i < count; i++)
                {
                    availableInstanceExtensions[i] = new(extensionProperties[i].extensionName);
                }

                return availableInstanceExtensions;
            }
            else
            {
                return new();
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Library library && Equals(library);
        }

        public readonly bool Equals(Library other)
        {
            return version.Equals(other.version) && name.Equals(other.name);
        }

        public readonly override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + version.GetHashCode();
            hash = hash * 31 + name.GetHashCode();
            return hash;
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToEnumerateInstanceLayerProperties(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate instance layer properties: {result}");
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToEnumerateInstanceExtensionProperties(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to enumerate instance extension properties: {result}");
            }
        }

        public static bool operator ==(Library left, Library right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Library left, Library right)
        {
            return !(left == right);
        }
    }
}